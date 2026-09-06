import { execFile } from "node:child_process";
import fs from "node:fs/promises";
import http from "node:http";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { afterEach, beforeEach, describe, expect, it } from "vitest";

const webRoot = fileURLToPath(new URL("../", import.meta.url));
const parentName = "boardoil-cooldown-parent";
const leafName = "boardoil-cooldown-leaf";

describe("npm release-age policy", () => {
  let directory;
  let server;
  let registry;
  let manifest;

  beforeEach(async () => {
    directory = await fs.mkdtemp(path.join(os.tmpdir(), "boardoil-npm-policy-"));
    await fs.copyFile(path.join(webRoot, ".npmrc"), path.join(directory, ".npmrc"));
    await fs.writeFile(path.join(directory, "user.npmrc"), "");
    await fs.writeFile(path.join(directory, "global.npmrc"), "");
    const project = JSON.parse(await fs.readFile(path.join(webRoot, "package.json"), "utf8"));
    manifest = {
      name: "boardoil-cooldown-test",
      version: "1.0.0",
      private: true,
      engines: project.engines,
      dependencies: { [parentName]: "^1.0.0" }
    };
    await writeManifest();

    const now = Date.now();
    server = http.createServer((request, response) => {
      const name = request.url.slice(1);
      if (![parentName, leafName].includes(name)) {
        response.writeHead(404).end();
        return;
      }

      const versions = {};
      const time = {};
      for (const [version, daysAgo] of [["1.0.0", 30], ["1.0.1", 8], ["1.1.0", 1]]) {
        versions[version] = {
          name,
          version,
          dist: { tarball: `${registry}/${name}/-/${name}-${version}.tgz` }
        };
        if (name === parentName) {
          versions[version].dependencies = { [leafName]: "^1.0.0" };
        }
        time[version] = new Date(now - daysAgo * 86_400_000).toISOString();
      }

      response.setHeader("Content-Type", "application/json");
      response.end(JSON.stringify({ name, versions, time, "dist-tags": { latest: "1.1.0" } }));
    });
    await new Promise((resolve, reject) => {
      server.once("error", reject);
      server.listen(0, "127.0.0.1", resolve);
    });
    registry = `http://127.0.0.1:${server.address().port}`;
  });

  afterEach(async () => {
    if (server) {
      await new Promise(resolve => server.close(resolve));
    }
    if (directory) {
      await fs.rm(directory, { recursive: true, force: true });
    }
  });

  async function writeManifest() {
    await fs.writeFile(path.join(directory, "package.json"), JSON.stringify(manifest));
  }

  async function npm(args) {
    // Invoke the same npm that launched the suite, without shell quoting or user settings.
    const env = Object.fromEntries(Object.entries(process.env)
      .filter(([key]) => !key.toLowerCase().startsWith("npm_config_")));
    return await new Promise(resolve => {
      execFile(process.execPath, [
        process.env.npm_execpath,
        ...args,
        `--registry=${registry}`,
        `--cache=${path.join(directory, "cache")}`,
        `--userconfig=${path.join(directory, "user.npmrc")}`,
        `--globalconfig=${path.join(directory, "global.npmrc")}`,
        "--no-audit",
        "--no-fund",
        "--fetch-retries=0"
      ], { cwd: directory, env, timeout: 15_000 }, (error, stdout, stderr) => {
        resolve({ code: error?.code ?? 0, stdout, stderr });
      });
    });
  }

  async function lockfile() {
    return JSON.parse(await fs.readFile(path.join(directory, "package-lock.json"), "utf8"));
  }

  function expectSuccess(result) {
    expect(result.code, result.stdout + result.stderr).toBe(0);
  }

  it("selects eligible direct and transitive versions instead of yesterday's releases", async () => {
    expectSuccess(await npm(["install", "--package-lock-only"]));
    const lock = await lockfile();
    expect(lock.packages[`node_modules/${parentName}`].version).toBe("1.0.1");
    expect(lock.packages[`node_modules/${leafName}`].version).toBe("1.0.1");
  });

  it("updates a locked dependency to an eligible release while skipping the newest release", async () => {
    manifest.dependencies[parentName] = "1.0.0";
    await writeManifest();
    expectSuccess(await npm(["install", "--package-lock-only"]));
    expect((await lockfile()).packages[`node_modules/${parentName}`].version).toBe("1.0.0");
    manifest.dependencies[parentName] = "^1.0.0";
    await writeManifest();
    expectSuccess(await npm(["update", parentName, "--package-lock-only"]));
    expect((await lockfile()).packages[`node_modules/${parentName}`].version).toBe("1.0.1");
  });

  it("rejects an exact version that is still inside the cooldown", async () => {
    manifest.dependencies[parentName] = "1.1.0";
    await writeManifest();
    const result = await npm(["install", "--package-lock-only"]);
    expect(result.code).not.toBe(0);
    expect(result.stderr).toContain("ETARGET");
  });

  it("allows a named security exception while retaining the transitive cooldown", async () => {
    manifest.dependencies[parentName] = "1.1.0";
    await writeManifest();
    expectSuccess(await npm(["install", "--package-lock-only", `--min-release-age-exclude=${parentName}`]));
    const lock = await lockfile();
    expect(lock.packages[`node_modules/${parentName}`].version).toBe("1.1.0");
    expect(lock.packages[`node_modules/${leafName}`].version).toBe("1.0.1");
  });

  it("preserves an approved locked release during ci without repeating the exception", async () => {
    expectSuccess(await npm(["install", "--package-lock-only", `--min-release-age-exclude=${parentName}`]));
    const before = await fs.readFile(path.join(directory, "package-lock.json"), "utf8");
    expect((await lockfile()).packages[`node_modules/${parentName}`].version).toBe("1.1.0");
    // No tarballs are served: this checks ci resolution and lock validation without extraction.
    expectSuccess(await npm(["ci", "--dry-run"]));
    expect(await fs.readFile(path.join(directory, "package-lock.json"), "utf8")).toBe(before);
  });

  it("rejects an npm version that does not match the project pin", async () => {
    manifest.engines = { ...manifest.engines, npm: "0.0.0" };
    await writeManifest();
    const result = await npm(["install", "--package-lock-only"]);
    expect(result.code).not.toBe(0);
    expect(result.stderr).toContain("EBADENGINE");
  });
});
