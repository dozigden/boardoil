import { spawn } from "node:child_process";
import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";

export async function captureCommandOutput(command, args, options = {}) {
  const outputDirectory = await fs.mkdtemp(
    path.join(os.tmpdir(), "boardoil-command-output-")
  );
  const stdoutPath = path.join(outputDirectory, "stdout");
  const stderrPath = path.join(outputDirectory, "stderr");
  let stdoutHandle;
  let stderrHandle;

  try {
    stdoutHandle = await fs.open(stdoutPath, "w");
    stderrHandle = await fs.open(stderrPath, "w");

    const result = await new Promise((resolve, reject) => {
      const child = spawn(command, args, {
        cwd: options.cwd,
        stdio: ["ignore", stdoutHandle.fd, stderrHandle.fd],
        windowsHide: true
      });

      child.once("error", reject);
      child.once("close", (exitCode, signal) => {
        resolve({ exitCode, signal });
      });
    });

    await stdoutHandle.close();
    stdoutHandle = null;
    await stderrHandle.close();
    stderrHandle = null;

    return {
      ...result,
      stdout: await fs.readFile(stdoutPath, "utf8"),
      stderr: await fs.readFile(stderrPath, "utf8")
    };
  } finally {
    await Promise.allSettled([
      stdoutHandle?.close(),
      stderrHandle?.close()
    ]);
    await fs.rm(outputDirectory, { recursive: true, force: true });
  }
}
