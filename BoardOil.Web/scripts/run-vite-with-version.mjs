import { spawn } from "node:child_process";

const viteArguments = process.argv.slice(2);

function isTruthyEnv(value) {
  if (!value) {
    return false;
  }

  const normalised = value.trim().toLowerCase();
  return normalised === "1" || normalised === "true";
}

function useCompactOutput() {
  const configuredMode = process.env.BOARDOIL_TEST_OUTPUT?.trim().toLowerCase();
  if (configuredMode === "compact") {
    return true;
  }

  if (configuredMode === "verbose") {
    return false;
  }

  return isTruthyEnv(process.env.CI)
    || isTruthyEnv(process.env.GITHUB_ACTIONS)
    || isTruthyEnv(process.env.CODEX_CI)
    || isTruthyEnv(process.env.CLAUDECODE)
    || Boolean(process.env.CODEX_THREAD_ID);
}

function hasLogLevelArgument() {
  return viteArguments.some(argument =>
    argument === "--logLevel" || argument.startsWith("--logLevel=")
  );
}

const effectiveViteArguments = [...viteArguments];
if (useCompactOutput() && viteArguments[0] === "build" && !hasLogLevelArgument()) {
  effectiveViteArguments.push("--logLevel", "warn");
}

const environment = {
  ...process.env,
  VITE_BO_VERSION: process.env.VITE_BO_VERSION ?? process.env.npm_package_version ?? "0.0.0"
};

function shellEscape(argument) {
  if (/^[A-Za-z0-9_./:-]+$/.test(argument)) {
    return argument;
  }

  return `"${argument.replace(/"/g, '\\"')}"`;
}

const viteCommand = ["npm", "exec", "vite", "--", ...effectiveViteArguments]
  .map(shellEscape)
  .join(" ");

const child = spawn(viteCommand, {
  env: environment,
  stdio: "inherit",
  shell: true
});

child.on("error", error => {
  console.error(error.message);
  process.exit(1);
});

child.on("exit", code => {
  process.exit(code ?? 0);
});
