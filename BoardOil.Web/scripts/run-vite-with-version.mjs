import { spawn } from "node:child_process";

const viteArguments = process.argv.slice(2);

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

const viteCommand = ["npm", "exec", "vite", "--", ...viteArguments]
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
