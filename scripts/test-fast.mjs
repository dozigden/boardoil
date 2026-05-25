#!/usr/bin/env node
import { spawnSync } from "node:child_process";
import path from "node:path";
import { fileURLToPath } from "node:url";

const rootDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const args = process.argv.slice(2);

const API_TEST_PROJECT = "BoardOil.Api.Tests/BoardOil.Api.Tests.csproj";
const SERVICES_TEST_PROJECT = "BoardOil.Services.Tests/BoardOil.Services.Tests.csproj";
const API_TEST_DLL = "BoardOil.Api.Tests/bin/Release/net10.0/BoardOil.Api.Tests.dll";
const SERVICES_TEST_DLL = "BoardOil.Services.Tests/bin/Release/net10.0/BoardOil.Services.Tests.dll";
const FAST_API_EXCLUDE_CLASS_FILTER = "*IntegrationTests*";

let mode = "auto";
let runApi = false;
let runServices = false;
let runWeb = false;
let restoreCompleted = false;
const failedSuites = [];

for (const arg of args) {
  if (["--api-only", "--services-only", "--web-only", "--backend-only", "--full"].includes(arg)) {
    mode = arg.slice(2);
    continue;
  }

  if (arg === "--help" || arg === "-h") {
    console.log("Usage: node scripts/test-fast.mjs [--api-only|--services-only|--web-only|--backend-only|--full]");
    process.exit(0);
  }

  console.error(`Unknown argument: ${arg}`);
  process.exit(2);
}

function run(command, commandArgs, cwd = rootDir) {
  return spawnSync(command, commandArgs, {
    cwd,
    stdio: "inherit",
    shell: process.platform === "win32",
    encoding: "utf8"
  });
}

function runCapture(command, commandArgs, cwd = rootDir) {
  return spawnSync(command, commandArgs, {
    cwd,
    shell: process.platform === "win32",
    encoding: "utf8"
  });
}

function restoreBackendOnce() {
  if (restoreCompleted) {
    return;
  }

  console.log("[test-fast] Restoring backend solution (one-time fallback)");
  const restore = run("dotnet", ["restore", "BoardOil.slnx", "--locked-mode", "-maxcpucount:1", "-nodeReuse:false"]);
  if (restore.status !== 0) {
    throw new Error("dotnet restore failed");
  }

  restoreCompleted = true;
}

function buildTestProjectRelease(projectPath) {
  const firstBuild = run("dotnet", [
    "build",
    projectPath,
    "--configuration",
    "Release",
    "--no-restore",
    "-maxcpucount:1",
    "-nodeReuse:false"
  ]);

  if (firstBuild.status === 0) {
    return;
  }

  console.log(`[test-fast] Build without restore failed for ${projectPath}; retrying after restore.`);
  restoreBackendOnce();

  const secondBuild = run("dotnet", [
    "build",
    projectPath,
    "--configuration",
    "Release",
    "--no-restore",
    "-maxcpucount:1",
    "-nodeReuse:false"
  ]);

  if (secondBuild.status !== 0) {
    throw new Error(`Build failed for ${projectPath}`);
  }
}

function runApiReleaseTests() {
  console.log("[test-fast] Running API fast tests (Release, excludes slow integration classes)");
  buildTestProjectRelease(API_TEST_PROJECT);
  const result = run("dotnet", [API_TEST_DLL, "--filter-not-class", FAST_API_EXCLUDE_CLASS_FILTER]);
  if (result.status !== 0) {
    throw new Error("api-fast failed");
  }
}

function runServicesReleaseTests() {
  console.log("[test-fast] Running Services fast tests (Release)");
  buildTestProjectRelease(SERVICES_TEST_PROJECT);
  const result = run("dotnet", [SERVICES_TEST_DLL]);
  if (result.status !== 0) {
    throw new Error("services-fast failed");
  }
}

function runWebChecks() {
  console.log("[test-fast] Running web checks");
  const check = run("npm", ["run", "check"], path.join(rootDir, "BoardOil.Web"));
  if (check.status !== 0) {
    throw new Error("web-checks failed at npm run check");
  }

  const test = run("npm", ["test"], path.join(rootDir, "BoardOil.Web"));
  if (test.status !== 0) {
    throw new Error("web-checks failed at npm test");
  }
}

function runSuite(suiteName, action) {
  try {
    action();
  } catch {
    console.log(`[test-fast] Suite failed: ${suiteName}`);
    failedSuites.push(suiteName);
  }
}

function finish() {
  if (failedSuites.length > 0) {
    console.log("[test-fast] Failed suites:");
    for (const suite of failedSuites) {
      console.log(`  ${suite}`);
    }

    console.log("[test-fast] Rerun using node scripts/test-fast.mjs mode flags or node scripts/test-full.mjs for full coverage.");
    process.exit(1);
  }

  console.log("[test-fast] Done");
}

if (mode !== "auto") {
  if (mode === "api-only") {
    runSuite("api-fast", runApiReleaseTests);
  } else if (mode === "services-only") {
    runSuite("services-fast", runServicesReleaseTests);
  } else if (mode === "web-only") {
    runSuite("web-checks", runWebChecks);
  } else if (mode === "backend-only") {
    runSuite("api-fast", runApiReleaseTests);
    runSuite("services-fast", runServicesReleaseTests);
  } else if (mode === "full") {
    runSuite("full-lane", () => {
      const full = run("node", ["scripts/test-full.mjs"]);
      if (full.status !== 0) {
        throw new Error("full-lane failed");
      }
    });
  } else {
    console.error(`Unsupported mode: ${mode}`);
    process.exit(2);
  }

  finish();
  process.exit(0);
}

const staged = runCapture("git", ["diff", "--name-only", "--cached"]);
const unstaged = runCapture("git", ["diff", "--name-only"]);
const untracked = runCapture("git", ["ls-files", "--others", "--exclude-standard"]);
const changedFiles = Array.from(
  new Set(
    `${staged.stdout ?? ""}\n${unstaged.stdout ?? ""}\n${untracked.stdout ?? ""}`
      .split(/\r?\n/)
      .map(value => value.trim())
      .filter(Boolean)
  )
).sort((a, b) => a.localeCompare(b));

if (changedFiles.length === 0) {
  console.log("[test-fast] No changed files found; nothing to run.");
  process.exit(0);
}

console.log("[test-fast] Changed files:");
for (const changedFile of changedFiles) {
  console.log(`  ${changedFile}`);
}

for (const file of changedFiles) {
  if (file.startsWith("BoardOil.Web/")) {
    runWeb = true;
    continue;
  }

  if (file.startsWith("BoardOil.Services/") || file.startsWith("BoardOil.Services.Tests/")) {
    runServices = true;
    continue;
  }

  if (file.startsWith("BoardOil.Api/") || file.startsWith("BoardOil.Api.Tests/")) {
    runApi = true;
    continue;
  }

  if (
    file.startsWith("BoardOil.Contracts/") ||
    file.startsWith("BoardOil.Abstractions/") ||
    file.startsWith("BoardOil.Ef/") ||
    file.startsWith("BoardOil.Data.Abstractions/") ||
    file.startsWith("BoardOil.Mcp.Contracts/")
  ) {
    runApi = true;
    runServices = true;
    continue;
  }

  if (
    file === "BoardOil.slnx" ||
    file === "Directory.Build.props" ||
    file === "Directory.Packages.props" ||
    file === "global.json" ||
    file === "NuGet.config" ||
    file.startsWith(".github/workflows/") ||
    file.startsWith("scripts/")
  ) {
    runApi = true;
    runServices = true;
  }
}

if (!runApi && !runServices && !runWeb) {
  console.log("[test-fast] No code/test-impacting changes detected; nothing to run.");
  process.exit(0);
}

if (runApi) {
  runSuite("api-fast", runApiReleaseTests);
}

if (runServices) {
  runSuite("services-fast", runServicesReleaseTests);
}

if (runWeb) {
  runSuite("web-checks", runWebChecks);
}

finish();
