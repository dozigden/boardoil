import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const defaultCandidateFiles = [
  "LICENSE",
  "LICENSE.md",
  "LICENSE.txt",
  "LICENCE",
  "LICENCE.md",
  "LICENCE.txt",
  "COPYING",
  "NOTICE"
];

const bundledAssetLicences = [
  {
    packageName: "Montserrat Font",
    version: "2024",
    declaredLicence: "SIL Open Font License 1.1",
    sourceFile: "src/assets/fonts/montserrat/OFL-1.1.txt",
    outputFile: "OFL-Montserrat.txt"
  }
];

function getPathParts(packageName) {
  return packageName.split("/");
}

function sanitisePackageName(packageName) {
  return packageName
    .replace(/^@/, "")
    .replace(/\//g, "-")
    .replace(/[^a-zA-Z0-9.-]+/g, "-")
    .toLowerCase();
}

function getOutputFileName(packageName) {
  return `npm-${sanitisePackageName(packageName)}.txt`;
}

async function fileExists(filePath) {
  try {
    const stats = await fs.stat(filePath);
    return stats.isFile();
  } catch {
    return false;
  }
}

async function findLicenceFile(packageRoot, candidateFiles) {
  for (const candidateFile of candidateFiles) {
    const candidatePath = path.join(packageRoot, candidateFile);
    if (await fileExists(candidatePath)) {
      return candidatePath;
    }
  }

  return null;
}

async function removeFileIfPresent(filePath) {
  try {
    await fs.unlink(filePath);
  } catch (error) {
    if (error && error.code !== "ENOENT") {
      throw error;
    }
  }
}

async function loadRuntimeDependencies(projectRoot) {
  const packageJsonPath = path.join(projectRoot, "package.json");
  const packageJson = JSON.parse(await fs.readFile(packageJsonPath, "utf8"));
  return Object.keys(packageJson.dependencies ?? {}).sort((left, right) =>
    left.localeCompare(right)
  );
}

async function loadPreviouslyManagedOutputFiles(licencesOutputDirectory) {
  const manifestPath = path.join(licencesOutputDirectory, "MANIFEST.json");
  try {
    const existingManifest = JSON.parse(await fs.readFile(manifestPath, "utf8"));
    return new Set(
      (existingManifest.copiedLicences ?? [])
        .map((entry) => entry.outputFile)
        .filter((outputFile) => typeof outputFile === "string")
    );
  } catch (error) {
    if (error && error.code === "ENOENT") {
      return new Set();
    }

    throw error;
  }
}

function getManifestPublicOutputFileName(outputFile) {
  return path.basename(outputFile);
}

async function loadPreviouslyManagedPublicFiles(publicManifestPath) {
  try {
    const existingManifest = JSON.parse(await fs.readFile(publicManifestPath, "utf8"));
    const previouslyManagedFiles = new Set(["manifest.json"]);
    for (const copiedLicence of existingManifest.copiedLicences ?? []) {
      if (typeof copiedLicence.outputFile === "string") {
        previouslyManagedFiles.add(getManifestPublicOutputFileName(copiedLicence.outputFile));
      }
    }

    return previouslyManagedFiles;
  } catch (error) {
    if (error && error.code === "ENOENT") {
      return new Set(["manifest.json"]);
    }

    throw error;
  }
}

async function main() {
  const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
  const projectRoot = path.resolve(scriptDirectory, "..");
  const nodeModulesRoot = path.join(projectRoot, "node_modules");
  const licencesOutputDirectory = path.join(projectRoot, "THIRD_PARTY_LICENSES");
  const manualLicencesDirectory = path.join(projectRoot, "THIRD_PARTY_LICENSES_MANUAL");
  const publicLicencesDirectory = path.join(projectRoot, "public", "third-party-licenses");
  const strictMode = process.argv.includes("--strict");
  const generatedAtUtc = new Date().toISOString();
  const runtimeDependencies = await loadRuntimeDependencies(projectRoot);

  await fs.mkdir(licencesOutputDirectory, { recursive: true });
  await fs.mkdir(manualLicencesDirectory, { recursive: true });
  await fs.mkdir(publicLicencesDirectory, { recursive: true });

  const copiedLicences = [];
  const unresolvedPackages = [];
  const previouslyManagedOutputFiles = await loadPreviouslyManagedOutputFiles(
    licencesOutputDirectory
  );
  const currentlyManagedOutputFiles = new Set();

  for (const packageName of runtimeDependencies) {
    const packageRoot = path.join(nodeModulesRoot, ...getPathParts(packageName));
    const packageJsonPath = path.join(packageRoot, "package.json");
    const outputFile = getOutputFileName(packageName);
    const outputPath = path.join(licencesOutputDirectory, outputFile);
    const manualSourcePath = path.join(manualLicencesDirectory, outputFile);
    currentlyManagedOutputFiles.add(path.relative(projectRoot, outputPath));

    let packageJson;
    try {
      packageJson = JSON.parse(await fs.readFile(packageJsonPath, "utf8"));
    } catch (error) {
      await removeFileIfPresent(outputPath);
      unresolvedPackages.push({
        packageName,
        reason: `Missing package metadata at ${path.relative(projectRoot, packageJsonPath)}`
      });
      continue;
    }

    const sourcePath = await findLicenceFile(packageRoot, defaultCandidateFiles);

    const hasManualLicence = await fileExists(manualSourcePath);

    if (!sourcePath && hasManualLicence) {
      await fs.copyFile(manualSourcePath, outputPath);
      copiedLicences.push({
        packageName,
        version: packageJson.version ?? "unknown",
        declaredLicence: packageJson.license ?? "unknown",
        sourceType: "manual",
        sourceFile: path.relative(projectRoot, manualSourcePath),
        outputFile: path.relative(projectRoot, outputPath)
      });
      continue;
    }

    if (!sourcePath) {
      await removeFileIfPresent(outputPath);
      unresolvedPackages.push({
        packageName,
        version: packageJson.version ?? "unknown",
        declaredLicence: packageJson.license ?? "unknown",
        reason:
          "No standalone licence file found in installed package and no manual override was provided",
        expectedSourceFile: path.relative(projectRoot, manualSourcePath),
        resolutionHint: "Add a manual licence file at"
      });
      continue;
    }

    await fs.copyFile(sourcePath, outputPath);
    copiedLicences.push({
      packageName,
      version: packageJson.version ?? "unknown",
      declaredLicence: packageJson.license ?? "unknown",
      sourceType: "package",
      sourceFile: path.relative(projectRoot, sourcePath),
      outputFile: path.relative(projectRoot, outputPath)
    });
  }

  for (const bundledAsset of bundledAssetLicences) {
    const sourcePath = path.join(projectRoot, bundledAsset.sourceFile);
    const outputPath = path.join(licencesOutputDirectory, bundledAsset.outputFile);
    currentlyManagedOutputFiles.add(path.relative(projectRoot, outputPath));

    if (!(await fileExists(sourcePath))) {
      await removeFileIfPresent(outputPath);
      unresolvedPackages.push({
        packageName: bundledAsset.packageName,
        version: bundledAsset.version,
        declaredLicence: bundledAsset.declaredLicence,
        reason: "Bundled asset licence source file was not found",
        expectedSourceFile: path.relative(projectRoot, sourcePath),
        resolutionHint: "Add or restore the bundled asset licence file at"
      });
      continue;
    }

    await fs.copyFile(sourcePath, outputPath);
    copiedLicences.push({
      packageName: bundledAsset.packageName,
      version: bundledAsset.version,
      declaredLicence: bundledAsset.declaredLicence,
      sourceType: "asset",
      sourceFile: bundledAsset.sourceFile,
      outputFile: path.relative(projectRoot, outputPath)
    });
  }

  for (const previousOutputFile of previouslyManagedOutputFiles) {
    if (!currentlyManagedOutputFiles.has(previousOutputFile)) {
      await removeFileIfPresent(path.join(projectRoot, previousOutputFile));
    }
  }

  const manifest = {
    generatedAtUtc,
    packageSource: "package.json dependencies + bundled assets",
    copiedLicences,
    unresolvedPackages
  };

  await fs.writeFile(
    path.join(licencesOutputDirectory, "MANIFEST.json"),
    `${JSON.stringify(manifest, null, 2)}\n`,
    "utf8"
  );

  const unresolvedReportLines = [
    "# Unresolved Third-Party Licence Sources",
    "",
    "This report is generated by `scripts/sync-third-party-licences.mjs`.",
    ""
  ];

  if (unresolvedPackages.length === 0) {
    unresolvedReportLines.push(
      "All configured dependencies and bundled assets had a licence file copied from package, manual, or asset sources."
    );
  } else {
    for (const unresolvedPackage of unresolvedPackages) {
      unresolvedReportLines.push(
        `- \`${unresolvedPackage.packageName}\` ` +
          `(version: \`${unresolvedPackage.version ?? "unknown"}\`, ` +
          `declared licence: \`${unresolvedPackage.declaredLicence ?? "unknown"}\`): ` +
          `${unresolvedPackage.reason}`
      );
      if (unresolvedPackage.expectedSourceFile) {
        unresolvedReportLines.push(
          `  ${unresolvedPackage.resolutionHint ?? "Expected source file"}: \`${unresolvedPackage.expectedSourceFile}\``
        );
      }
    }
  }

  unresolvedReportLines.push("");

  await fs.writeFile(
    path.join(licencesOutputDirectory, "UNRESOLVED.md"),
    unresolvedReportLines.join("\n"),
    "utf8"
  );

  const publicManifestPath = path.join(publicLicencesDirectory, "manifest.json");
  const previouslyManagedPublicFiles = await loadPreviouslyManagedPublicFiles(publicManifestPath);
  const currentlyManagedPublicFiles = new Set(["manifest.json"]);

  for (const copiedLicence of copiedLicences) {
    const sourcePath = path.join(projectRoot, copiedLicence.outputFile);
    const outputFileName = getManifestPublicOutputFileName(copiedLicence.outputFile);
    const outputPath = path.join(publicLicencesDirectory, outputFileName);

    await fs.copyFile(sourcePath, outputPath);
    currentlyManagedPublicFiles.add(outputFileName);
  }

  await fs.writeFile(publicManifestPath, `${JSON.stringify(manifest, null, 2)}\n`, "utf8");

  for (const previousPublicFile of previouslyManagedPublicFiles) {
    if (!currentlyManagedPublicFiles.has(previousPublicFile)) {
      await removeFileIfPresent(path.join(publicLicencesDirectory, previousPublicFile));
    }
  }

  console.log(`Copied ${copiedLicences.length} third-party licence file(s).`);

  if (unresolvedPackages.length > 0) {
    console.warn(`Could not source ${unresolvedPackages.length} package licence file(s).`);
    console.warn("See BoardOil.Web/THIRD_PARTY_LICENSES/UNRESOLVED.md for details.");

    if (strictMode) {
      process.exitCode = 1;
    }
  }
}

await main();
