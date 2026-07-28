#!/usr/bin/env node
import { spawnSync } from "node:child_process";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

const rootDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const args = process.argv.slice(2);

let runBackend = true;
let runWeb = true;
let outputModeOverride = null;

for (const arg of args) {
  if (arg === "--backend-only") {
    runWeb = false;
    continue;
  }

  if (arg === "--web-only") {
    runBackend = false;
    continue;
  }

  if (arg === "--compact" || arg === "--verbose") {
    outputModeOverride = arg.slice(2);
    continue;
  }

  if (arg === "--help" || arg === "-h") {
    console.log("Usage: node scripts/test-full.mjs [--backend-only|--web-only] [--compact|--verbose]");
    process.exit(0);
  }

  console.error(`Unknown argument: ${arg}`);
  process.exit(2);
}

function isTruthyEnv(value) {
  if (!value) {
    return false;
  }

  const normalised = value.trim().toLowerCase();
  return normalised === "1" || normalised === "true";
}

function resolveOutputMode() {
  if (outputModeOverride) {
    return outputModeOverride;
  }

  const configuredMode = process.env.BOARDOIL_TEST_OUTPUT?.trim().toLowerCase();
  if (configuredMode === "compact" || configuredMode === "verbose") {
    return configuredMode;
  }

  if (
    isTruthyEnv(process.env.CI) ||
    isTruthyEnv(process.env.GITHUB_ACTIONS) ||
    isTruthyEnv(process.env.CODEX_CI) ||
    isTruthyEnv(process.env.CLAUDECODE) ||
    Boolean(process.env.CODEX_THREAD_ID)
  ) {
    return "compact";
  }

  return "verbose";
}

const outputMode = resolveOutputMode();
const compactOutput = outputMode === "compact";

function isAgentEnvironment() {
  return (
    isTruthyEnv(process.env.CODEX_CI) ||
    isTruthyEnv(process.env.CLAUDECODE) ||
    Boolean(process.env.CODEX_THREAD_ID)
  );
}

function childEnv() {
  const environment = {
    ...process.env,
    BOARDOIL_TEST_OUTPUT: outputMode
  };

  if (isAgentEnvironment() && !environment.NUGET_HTTP_CACHE_PATH) {
    environment.NUGET_HTTP_CACHE_PATH = path.join(os.tmpdir(), "boardoil-nuget-http-cache");
  }

  return environment;
}

function printCapturedOutput(result) {
  if (result.stdout) {
    process.stdout.write(result.stdout);
    if (!result.stdout.endsWith("\n")) {
      process.stdout.write("\n");
    }
  }

  if (result.stderr) {
    process.stderr.write(result.stderr);
    if (!result.stderr.endsWith("\n")) {
      process.stderr.write("\n");
    }
  }
}

function commandFailed(result) {
  if (typeof result.status === "number") {
    return result.status !== 0;
  }

  return Boolean(result.error);
}

function run(command, commandArgs, cwd = rootDir) {
  const result = spawnSync(command, commandArgs, {
    cwd,
    stdio: compactOutput ? "pipe" : "inherit",
    shell: process.platform === "win32",
    encoding: "utf8",
    env: childEnv()
  });

  if (compactOutput && commandFailed(result)) {
    printCapturedOutput(result);
  }

  if (typeof result.status === "number" && result.status !== 0) {
    process.exit(result.status);
  }

  if (result.error && typeof result.status !== "number") {
    console.error(result.error.message);
    process.exit(1);
  }

  return result;
}

function compactTestRunnerArgs() {
  if (!compactOutput) {
    return [];
  }

  return ["--no-progress", "--no-ansi"];
}

function npmRunArgs(scriptName) {
  if (compactOutput) {
    return ["run", "--silent", scriptName];
  }

  return ["run", scriptName];
}

function printDotnetTestSummary(label, output) {
  if (!compactOutput) {
    return;
  }

  if (!output) {
    console.log(`[test-full] ${label}: passed`);
    return;
  }

  const statusMatch = output.match(/Test run summary:\s*([^!\r\n]+)!/);
  const totalMatch = output.match(/^\s*total:\s*(\d+)/m);
  const durationMatch = output.match(/^\s*duration:\s*(.+)$/m);

  const status = statusMatch?.[1]?.trim().toLowerCase() ?? "completed";
  const total = totalMatch?.[1]?.trim();
  const duration = durationMatch?.[1]?.trim();

  if (total && duration) {
    console.log(`[test-full] ${label}: ${status} (${total} tests, ${duration})`);
    return;
  }

  console.log(`[test-full] ${label}: ${status}`);
}

function printVitestSummary(label, output) {
  if (!compactOutput) {
    return;
  }

  if (!output) {
    console.log(`[test-full] ${label}: passed`);
    return;
  }

  const testsMatch = output.match(/^\s*Tests\s+(.+)$/m);
  const durationMatch = output.match(/^\s*Duration\s+(.+)$/m);

  if (testsMatch && durationMatch) {
    console.log(`[test-full] ${label}: ${testsMatch[1].trim()} (${durationMatch[1].trim()})`);
    return;
  }

  console.log(`[test-full] ${label}: passed`);
}

if (runBackend) {
  console.log("[test-full] Backend: restore + build + tests");
  run("dotnet", ["restore", "BoardOil.slnx", "--locked-mode", "-maxcpucount:1", "-nodeReuse:false"]);
  run("dotnet", ["build", "BoardOil.slnx", "--configuration", "Release", "--no-restore", "-maxcpucount:1", "-nodeReuse:false"]);
  const apiTests = run("dotnet", ["BoardOil.Api.Tests/bin/Release/net10.0/BoardOil.Api.Tests.dll", ...compactTestRunnerArgs()]);
  printDotnetTestSummary("API tests", apiTests.stdout);

  const servicesTests = run("dotnet", ["BoardOil.Services.Tests/bin/Release/net10.0/BoardOil.Services.Tests.dll", ...compactTestRunnerArgs()]);
  printDotnetTestSummary("Services tests", servicesTests.stdout);
}

if (runWeb) {
  console.log("[test-full] Web: check + test");
  run("npm", npmRunArgs("check"), path.join(rootDir, "BoardOil.Web"));
  if (compactOutput) {
    console.log("[test-full] Web check: passed");
  }

  const webTests = run("npm", npmRunArgs("test"), path.join(rootDir, "BoardOil.Web"));
  printVitestSummary("Web tests", webTests.stdout);
}

console.log("[test-full] Done");
