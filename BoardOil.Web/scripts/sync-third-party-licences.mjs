import fs from "node:fs/promises";
import os from "node:os";
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

const nugetDependencySources = [
  {
    sourceName: "BoardOil.Api",
    lockFile: "../BoardOil.Api/packages.lock.json"
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

function sanitiseVersion(version) {
  return version.replace(/[^a-zA-Z0-9.+-]+/g, "-").toLowerCase();
}

function getNpmOutputFileName(packageName) {
  return `npm-${sanitisePackageName(packageName)}.txt`;
}

function getNuGetOutputFileName(packageName, version) {
  return `nuget-${sanitisePackageName(packageName)}-${sanitiseVersion(version)}.txt`;
}

async function fileExists(filePath) {
  try {
    const stats = await fs.stat(filePath);
    return stats.isFile();
  } catch {
    return false;
  }
}

async function directoryExists(directoryPath) {
  try {
    const stats = await fs.stat(directoryPath);
    return stats.isDirectory();
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

async function loadRuntimeNpmDependencies(projectRoot) {
  const packageJsonPath = path.join(projectRoot, "package.json");
  const packageJson = JSON.parse(await fs.readFile(packageJsonPath, "utf8"));
  return Object.keys(packageJson.dependencies ?? {}).sort((left, right) =>
    left.localeCompare(right)
  );
}

async function loadRuntimeNuGetDependencies(projectRoot) {
  const packagesByKey = new Map();
  const sourceIssues = [];

  for (const dependencySource of nugetDependencySources) {
    const lockFilePath = path.resolve(projectRoot, dependencySource.lockFile);

    let lockFile;
    try {
      lockFile = JSON.parse(await fs.readFile(lockFilePath, "utf8"));
    } catch (error) {
      sourceIssues.push({
        sourceName: dependencySource.sourceName,
        reason: `Could not read lock file at ${path.relative(projectRoot, lockFilePath)}`,
        expectedSourceFile: path.relative(projectRoot, lockFilePath),
        resolutionHint: "Run dotnet restore to generate lock files, or add a manual licence file at"
      });
      continue;
    }

    if (!lockFile.dependencies || typeof lockFile.dependencies !== "object") {
      sourceIssues.push({
        sourceName: dependencySource.sourceName,
        reason: `Lock file is missing dependency metadata at ${path.relative(projectRoot, lockFilePath)}`,
        expectedSourceFile: path.relative(projectRoot, lockFilePath),
        resolutionHint: "Regenerate the lock file, or add a manual licence file at"
      });
      continue;
    }

    for (const [framework, frameworkDependencies] of Object.entries(lockFile.dependencies)) {
      if (!frameworkDependencies || typeof frameworkDependencies !== "object") {
        continue;
      }

      for (const [packageName, packageMetadata] of Object.entries(frameworkDependencies)) {
        if (!packageMetadata || typeof packageMetadata !== "object") {
          continue;
        }

        const dependencyType =
          typeof packageMetadata.type === "string" ? packageMetadata.type.toLowerCase() : "";

        if (dependencyType && dependencyType !== "direct" && dependencyType !== "transitive") {
          continue;
        }

        const version =
          typeof packageMetadata.resolved === "string" ? packageMetadata.resolved : null;

        if (!version) {
          continue;
        }

        const key = `${packageName}|${version}`;
        const existing = packagesByKey.get(key);

        if (existing) {
          existing.frameworks.add(framework);
          continue;
        }

        packagesByKey.set(key, {
          packageName,
          version,
          lockFile: path.relative(projectRoot, lockFilePath),
          frameworks: new Set([framework])
        });
      }
    }
  }

  const packages = [...packagesByKey.values()]
    .map(packageEntry => ({
      ...packageEntry,
      frameworks: [...packageEntry.frameworks].sort((left, right) => left.localeCompare(right))
    }))
    .sort((left, right) => {
      const byName = left.packageName.localeCompare(right.packageName);
      if (byName !== 0) {
        return byName;
      }

      return left.version.localeCompare(right.version);
    });

  return { packages, sourceIssues };
}

async function loadPreviouslyManagedOutputFiles(licencesOutputDirectory) {
  const manifestPath = path.join(licencesOutputDirectory, "MANIFEST.json");
  try {
    const existingManifest = JSON.parse(await fs.readFile(manifestPath, "utf8"));
    return new Set(
      (existingManifest.copiedLicences ?? [])
        .map(entry => entry.outputFile)
        .filter(outputFile => typeof outputFile === "string")
    );
  } catch (error) {
    if (error && error.code === "ENOENT") {
      return new Set();
    }

    throw error;
  }
}

async function loadExistingManifest(manifestPath) {
  try {
    return JSON.parse(await fs.readFile(manifestPath, "utf8"));
  } catch (error) {
    if (error && error.code === "ENOENT") {
      return null;
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

function decodeXmlEntities(value) {
  return value
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'")
    .replace(/&amp;/g, "&");
}

function parseXmlAttributes(rawAttributes) {
  const attributes = {};
  const attributePattern = /([A-Za-z0-9_.:-]+)="([^"]*)"/g;
  let match = attributePattern.exec(rawAttributes);
  while (match) {
    attributes[match[1]] = decodeXmlEntities(match[2]);
    match = attributePattern.exec(rawAttributes);
  }

  return attributes;
}

function readFirstTag(xmlText, tagName) {
  const pattern = new RegExp(`<${tagName}(\\s+[^>]*)?>([\\s\\S]*?)<\\/${tagName}>`, "i");
  const match = xmlText.match(pattern);
  if (!match) {
    return null;
  }

  return {
    attributes: parseXmlAttributes(match[1] ?? ""),
    text: decodeXmlEntities(match[2].trim())
  };
}

function parseNuspecMetadata(nuspecText) {
  const id = readFirstTag(nuspecText, "id")?.text ?? null;
  const version = readFirstTag(nuspecText, "version")?.text ?? null;
  const developmentDependency =
    (readFirstTag(nuspecText, "developmentDependency")?.text ?? "").toLowerCase() === "true";
  const licenceTag = readFirstTag(nuspecText, "license");
  const licenceUrl = readFirstTag(nuspecText, "licenseUrl")?.text ?? null;
  const copyright = readFirstTag(nuspecText, "copyright")?.text ?? null;

  return {
    id,
    version,
    developmentDependency,
    licenceType: licenceTag?.attributes.type?.toLowerCase() ?? null,
    licenceValue: licenceTag?.text ?? null,
    licenceUrl,
    copyright
  };
}

function getNuGetPackageRoot(packageName, version) {
  return path.join(os.homedir(), ".nuget", "packages", packageName.toLowerCase(), version.toLowerCase());
}

function toPosixPath(filePath) {
  return filePath.split(path.sep).join("/");
}

function getNuGetManifestSourceFile(packageName, version, packageRoot, sourcePath) {
  const relativePath = path.relative(packageRoot, sourcePath);
  const safeRelativePath =
    relativePath.startsWith("..") || path.isAbsolute(relativePath) ? path.basename(sourcePath) : relativePath;
  return `~/.nuget/packages/${packageName.toLowerCase()}/${version.toLowerCase()}/${toPosixPath(
    safeRelativePath
  )}`;
}

async function findNuspecFile(packageRoot, packageName) {
  const expectedPath = path.join(packageRoot, `${packageName.toLowerCase()}.nuspec`);
  if (await fileExists(expectedPath)) {
    return expectedPath;
  }

  try {
    const packageFiles = await fs.readdir(packageRoot);
    const nuspecFile = packageFiles.find(fileName => fileName.toLowerCase().endsWith(".nuspec"));
    if (!nuspecFile) {
      return null;
    }

    return path.join(packageRoot, nuspecFile);
  } catch {
    return null;
  }
}

function getLicenceUrlFromExpression(licenceExpression, fallbackUrl) {
  if (fallbackUrl) {
    return fallbackUrl;
  }

  if (!licenceExpression) {
    return null;
  }

  if (/^[A-Za-z0-9.+-]+$/.test(licenceExpression)) {
    return `https://licenses.nuget.org/${encodeURIComponent(licenceExpression)}`;
  }

  return null;
}

function buildNuGetExpressionNotice({ packageName, version, licenceExpression, licenceUrl, copyright }) {
  const lines = [
    `NuGet Package: ${packageName}`,
    `Version: ${version}`,
    `Declared Licence Expression: ${licenceExpression}`
  ];

  if (licenceUrl) {
    lines.push(`Licence Reference: ${licenceUrl}`);
  }

  if (copyright) {
    lines.push(`Copyright: ${copyright}`);
  }

  lines.push("");
  lines.push(
    "This package declares a licence expression in its NuGet metadata. Refer to the linked licence text for obligations."
  );
  lines.push("");

  return lines.join("\n");
}

function buildNuGetLicenceUrlNotice({ packageName, version, licenceUrl, copyright }) {
  const lines = [
    `NuGet Package: ${packageName}`,
    `Version: ${version}`,
    `Declared Licence URL: ${licenceUrl}`
  ];

  if (copyright) {
    lines.push(`Copyright: ${copyright}`);
  }

  lines.push("");
  lines.push(
    "This package declares its licence via URL metadata. Review the linked licence text for obligations."
  );
  lines.push("");

  return lines.join("\n");
}

async function main() {
  const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
  const projectRoot = path.resolve(scriptDirectory, "..");
  const nodeModulesRoot = path.join(projectRoot, "node_modules");
  const licencesOutputDirectory = path.join(projectRoot, "THIRD_PARTY_LICENSES");
  const manualLicencesDirectory = path.join(projectRoot, "THIRD_PARTY_LICENSES_MANUAL");
  const nugetManualLicencesDirectory = path.join(projectRoot, "THIRD_PARTY_LICENSES_MANUAL_NUGET");
  const publicLicencesDirectory = path.join(projectRoot, "public", "third-party-licenses");
  const strictMode = process.argv.includes("--strict");
  const publishPublicMirror = process.argv.includes("--publish-public");
  const runtimeNpmDependencies = await loadRuntimeNpmDependencies(projectRoot);
  const runtimeNugetDependencies = await loadRuntimeNuGetDependencies(projectRoot);

  await fs.mkdir(licencesOutputDirectory, { recursive: true });
  await fs.mkdir(manualLicencesDirectory, { recursive: true });
  await fs.mkdir(nugetManualLicencesDirectory, { recursive: true });
  if (publishPublicMirror) {
    await fs.mkdir(publicLicencesDirectory, { recursive: true });
  }

  const copiedLicences = [];
  const unresolvedPackages = [];
  const previouslyManagedOutputFiles = await loadPreviouslyManagedOutputFiles(
    licencesOutputDirectory
  );
  const currentlyManagedOutputFiles = new Set();

  for (const packageName of runtimeNpmDependencies) {
    const packageRoot = path.join(nodeModulesRoot, ...getPathParts(packageName));
    const packageJsonPath = path.join(packageRoot, "package.json");
    const outputFile = getNpmOutputFileName(packageName);
    const outputPath = path.join(licencesOutputDirectory, outputFile);
    const manualSourcePath = path.join(manualLicencesDirectory, outputFile);
    currentlyManagedOutputFiles.add(path.relative(projectRoot, outputPath));

    let packageJson;
    try {
      packageJson = JSON.parse(await fs.readFile(packageJsonPath, "utf8"));
    } catch {
      await removeFileIfPresent(outputPath);
      unresolvedPackages.push({
        ecosystem: "npm",
        packageName,
        version: "unknown",
        declaredLicence: "unknown",
        reason: `Missing package metadata at ${path.relative(projectRoot, packageJsonPath)}`
      });
      continue;
    }

    const sourcePath = await findLicenceFile(packageRoot, defaultCandidateFiles);

    const hasManualLicence = await fileExists(manualSourcePath);

    if (!sourcePath && hasManualLicence) {
      await fs.copyFile(manualSourcePath, outputPath);
      copiedLicences.push({
        ecosystem: "npm",
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
        ecosystem: "npm",
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
      ecosystem: "npm",
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
        ecosystem: "asset",
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
      ecosystem: "asset",
      packageName: bundledAsset.packageName,
      version: bundledAsset.version,
      declaredLicence: bundledAsset.declaredLicence,
      sourceType: "asset",
      sourceFile: bundledAsset.sourceFile,
      outputFile: path.relative(projectRoot, outputPath)
    });
  }

  for (const sourceIssue of runtimeNugetDependencies.sourceIssues) {
    unresolvedPackages.push({
      ecosystem: "nuget",
      packageName: `NuGet dependency source (${sourceIssue.sourceName})`,
      version: "unknown",
      declaredLicence: "unknown",
      reason: sourceIssue.reason,
      expectedSourceFile: sourceIssue.expectedSourceFile,
      resolutionHint: sourceIssue.resolutionHint
    });
  }

  for (const nugetPackage of runtimeNugetDependencies.packages) {
    const outputFile = getNuGetOutputFileName(nugetPackage.packageName, nugetPackage.version);
    const outputPath = path.join(licencesOutputDirectory, outputFile);
    const manualSourcePath = path.join(nugetManualLicencesDirectory, outputFile);
    currentlyManagedOutputFiles.add(path.relative(projectRoot, outputPath));

    const hasManualLicence = await fileExists(manualSourcePath);
    const packageRoot = getNuGetPackageRoot(nugetPackage.packageName, nugetPackage.version);

    if (hasManualLicence) {
      await fs.copyFile(manualSourcePath, outputPath);
      copiedLicences.push({
        ecosystem: "nuget",
        packageName: nugetPackage.packageName,
        version: nugetPackage.version,
        declaredLicence: "manual override",
        sourceType: "manual",
        sourceFile: path.relative(projectRoot, manualSourcePath),
        outputFile: path.relative(projectRoot, outputPath)
      });
      continue;
    }

    if (!(await directoryExists(packageRoot))) {
      await removeFileIfPresent(outputPath);
      unresolvedPackages.push({
        ecosystem: "nuget",
        packageName: nugetPackage.packageName,
        version: nugetPackage.version,
        declaredLicence: "unknown",
        reason: `Package was not found in local NuGet cache at ${packageRoot}`,
        expectedSourceFile: path.relative(projectRoot, manualSourcePath),
        resolutionHint:
          "Run dotnet restore so package metadata exists locally, or add a manual licence file at"
      });
      continue;
    }

    const nuspecPath = await findNuspecFile(packageRoot, nugetPackage.packageName);

    if (!nuspecPath) {
      await removeFileIfPresent(outputPath);
      unresolvedPackages.push({
        ecosystem: "nuget",
        packageName: nugetPackage.packageName,
        version: nugetPackage.version,
        declaredLicence: "unknown",
        reason: `Could not find a .nuspec file in ${packageRoot}`,
        expectedSourceFile: path.relative(projectRoot, manualSourcePath),
        resolutionHint: "Add a manual licence file at"
      });
      continue;
    }

    let nuspecText;
    try {
      nuspecText = await fs.readFile(nuspecPath, "utf8");
    } catch {
      await removeFileIfPresent(outputPath);
      unresolvedPackages.push({
        ecosystem: "nuget",
        packageName: nugetPackage.packageName,
        version: nugetPackage.version,
        declaredLicence: "unknown",
        reason: `Could not read NuGet metadata at ${nuspecPath}`,
        expectedSourceFile: path.relative(projectRoot, manualSourcePath),
        resolutionHint: "Add a manual licence file at"
      });
      continue;
    }

    const nuspecMetadata = parseNuspecMetadata(nuspecText);

    if (nuspecMetadata.developmentDependency) {
      await removeFileIfPresent(outputPath);
      continue;
    }

    const declaredLicence = nuspecMetadata.licenceValue ?? nuspecMetadata.licenceUrl ?? "unknown";
    const nugetManifestNuspecPath = getNuGetManifestSourceFile(
      nugetPackage.packageName,
      nugetPackage.version,
      packageRoot,
      nuspecPath
    );

    if (nuspecMetadata.licenceType === "file" && nuspecMetadata.licenceValue) {
      const relativeLicencePath = nuspecMetadata.licenceValue.replace(/\\/g, "/");
      const sourcePath = path.join(packageRoot, ...relativeLicencePath.split("/").filter(Boolean));

      if (!(await fileExists(sourcePath))) {
        await removeFileIfPresent(outputPath);
        unresolvedPackages.push({
          ecosystem: "nuget",
          packageName: nugetPackage.packageName,
          version: nugetPackage.version,
          declaredLicence,
          reason: `NuGet metadata points to missing licence file: ${relativeLicencePath}`,
          expectedSourceFile: path.relative(projectRoot, manualSourcePath),
          resolutionHint: "Add a manual licence file at"
        });
        continue;
      }

      await fs.copyFile(sourcePath, outputPath);
      copiedLicences.push({
        ecosystem: "nuget",
        packageName: nugetPackage.packageName,
        version: nugetPackage.version,
        declaredLicence,
        sourceType: "nuget-package-file",
        sourceFile: getNuGetManifestSourceFile(
          nugetPackage.packageName,
          nugetPackage.version,
          packageRoot,
          sourcePath
        ),
        outputFile: path.relative(projectRoot, outputPath)
      });
      continue;
    }

    if (nuspecMetadata.licenceType === "expression" && nuspecMetadata.licenceValue) {
      const licenceUrl = getLicenceUrlFromExpression(
        nuspecMetadata.licenceValue,
        nuspecMetadata.licenceUrl
      );
      const notice = buildNuGetExpressionNotice({
        packageName: nugetPackage.packageName,
        version: nugetPackage.version,
        licenceExpression: nuspecMetadata.licenceValue,
        licenceUrl,
        copyright: nuspecMetadata.copyright
      });

      await fs.writeFile(outputPath, notice, "utf8");
      copiedLicences.push({
        ecosystem: "nuget",
        packageName: nugetPackage.packageName,
        version: nugetPackage.version,
        declaredLicence,
        sourceType: "nuget-license-expression",
        sourceFile: nugetManifestNuspecPath,
        outputFile: path.relative(projectRoot, outputPath)
      });
      continue;
    }

    const sourcePath = await findLicenceFile(packageRoot, defaultCandidateFiles);
    if (sourcePath) {
      await fs.copyFile(sourcePath, outputPath);
      copiedLicences.push({
        ecosystem: "nuget",
        packageName: nugetPackage.packageName,
        version: nugetPackage.version,
        declaredLicence,
        sourceType: "package",
        sourceFile: getNuGetManifestSourceFile(
          nugetPackage.packageName,
          nugetPackage.version,
          packageRoot,
          sourcePath
        ),
        outputFile: path.relative(projectRoot, outputPath)
      });
      continue;
    }

    if (nuspecMetadata.licenceUrl) {
      const notice = buildNuGetLicenceUrlNotice({
        packageName: nugetPackage.packageName,
        version: nugetPackage.version,
        licenceUrl: nuspecMetadata.licenceUrl,
        copyright: nuspecMetadata.copyright
      });

      await fs.writeFile(outputPath, notice, "utf8");
      copiedLicences.push({
        ecosystem: "nuget",
        packageName: nugetPackage.packageName,
        version: nugetPackage.version,
        declaredLicence,
        sourceType: "nuget-license-url",
        sourceFile: nugetManifestNuspecPath,
        outputFile: path.relative(projectRoot, outputPath)
      });
      continue;
    }

    await removeFileIfPresent(outputPath);
    unresolvedPackages.push({
      ecosystem: "nuget",
      packageName: nugetPackage.packageName,
      version: nugetPackage.version,
      declaredLicence,
      reason: "NuGet metadata did not include a licence expression, licence file, or recognised licence file name",
      expectedSourceFile: path.relative(projectRoot, manualSourcePath),
      resolutionHint: "Add a manual licence file at"
    });
  }

  copiedLicences.sort((left, right) => {
    const byEcosystem = (left.ecosystem ?? "unknown").localeCompare(right.ecosystem ?? "unknown");
    if (byEcosystem !== 0) {
      return byEcosystem;
    }

    const byName = left.packageName.localeCompare(right.packageName);
    if (byName !== 0) {
      return byName;
    }

    return left.version.localeCompare(right.version);
  });

  unresolvedPackages.sort((left, right) => {
    const byEcosystem = (left.ecosystem ?? "unknown").localeCompare(right.ecosystem ?? "unknown");
    if (byEcosystem !== 0) {
      return byEcosystem;
    }

    const byName = left.packageName.localeCompare(right.packageName);
    if (byName !== 0) {
      return byName;
    }

    return (left.version ?? "unknown").localeCompare(right.version ?? "unknown");
  });

  for (const previousOutputFile of previouslyManagedOutputFiles) {
    if (!currentlyManagedOutputFiles.has(previousOutputFile)) {
      await removeFileIfPresent(path.join(projectRoot, previousOutputFile));
    }
  }

  const manifestPath = path.join(licencesOutputDirectory, "MANIFEST.json");
  const manifestBase = {
    packageSource:
      "package.json dependencies + bundled assets + NuGet packages from BoardOil.Api/packages.lock.json",
    copiedLicences,
    unresolvedPackages
  };

  const existingManifest = await loadExistingManifest(manifestPath);
  const existingCopiedLicences = existingManifest?.copiedLicences ?? [];
  const existingUnresolvedPackages = existingManifest?.unresolvedPackages ?? [];
  const shouldPreserveGeneratedAtUtc =
    typeof existingManifest?.generatedAtUtc === "string" &&
    existingManifest.packageSource === manifestBase.packageSource &&
    JSON.stringify(existingCopiedLicences) === JSON.stringify(manifestBase.copiedLicences) &&
    JSON.stringify(existingUnresolvedPackages) === JSON.stringify(manifestBase.unresolvedPackages);

  const manifest = {
    generatedAtUtc: shouldPreserveGeneratedAtUtc
      ? existingManifest.generatedAtUtc
      : new Date().toISOString(),
    ...manifestBase
  };

  await fs.writeFile(
    manifestPath,
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
      "All configured dependencies and bundled assets had a licence source resolved from package, manual, asset, or metadata sources."
    );
  } else {
    for (const unresolvedPackage of unresolvedPackages) {
      unresolvedReportLines.push(
        `- \`${unresolvedPackage.packageName}\` ` +
          `(ecosystem: \`${unresolvedPackage.ecosystem ?? "unknown"}\`, ` +
          `version: \`${unresolvedPackage.version ?? "unknown"}\`, ` +
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

  if (publishPublicMirror) {
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
