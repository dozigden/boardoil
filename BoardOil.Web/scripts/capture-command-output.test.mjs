import { describe, expect, it } from "vitest";
import { captureCommandOutput } from "./capture-command-output.mjs";

describe("captureCommandOutput", () => {
  it("captures stdout and stderr without child-process pipes", async () => {
    const result = await captureCommandOutput(
      process.execPath,
      [
        "-e",
        "process.stdout.write('captured output'); process.stderr.write('captured error'); process.exitCode = 3;"
      ]
    );

    expect(result).toEqual({
      exitCode: 3,
      signal: null,
      stdout: "captured output",
      stderr: "captured error"
    });
  });
});
