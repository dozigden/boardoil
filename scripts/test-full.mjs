#!/usr/bin/env node
import { spawnSync } from "node:child_process";
import path from "node:path";
import { fileURLToPath } from "node:url";

const rootDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const args = process.argv.slice(2);

let runBackend = true;
let runWeb = true;

for (const arg of args) {
  if (arg === "--backend-only") {
    runWeb = false;
    continue;
  }

  if (arg === "--web-only") {
    runBackend = false;
    continue;
  }

  if (arg === "--help" || arg === "-h") {
    console.log("Usage: node scripts/test-full.mjs [--backend-only|--web-only]");
    process.exit(0);
  }

  console.error(`Unknown argument: ${arg}`);
  process.exit(2);
}

function run(command, commandArgs, cwd = rootDir) {
  const result = spawnSync(command, commandArgs, {
    cwd,
    stdio: "inherit",
    shell: process.platform === "win32"
  });

  if (typeof result.status === "number" && result.status !== 0) {
    process.exit(result.status);
  }

  if (result.error) {
    console.error(result.error.message);
    process.exit(1);
  }
}

if (runBackend) {
  console.log("[test-full] Backend: restore + build + tests");
  run("dotnet", ["restore", "BoardOil.slnx", "--locked-mode", "-maxcpucount:1", "-nodeReuse:false"]);
  run("dotnet", ["build", "BoardOil.slnx", "--configuration", "Release", "--no-restore", "-maxcpucount:1", "-nodeReuse:false"]);
  run("dotnet", ["BoardOil.Api.Tests/bin/Release/net10.0/BoardOil.Api.Tests.dll"]);
  run("dotnet", ["BoardOil.Services.Tests/bin/Release/net10.0/BoardOil.Services.Tests.dll"]);
}

if (runWeb) {
  console.log("[test-full] Web: check + test");
  run("npm", ["run", "check"], path.join(rootDir, "BoardOil.Web"));
  run("npm", ["test"], path.join(rootDir, "BoardOil.Web"));
}

console.log("[test-full] Done");
