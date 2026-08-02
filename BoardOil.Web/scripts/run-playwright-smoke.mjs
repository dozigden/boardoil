import { spawn } from 'node:child_process';
import { mkdtemp, mkdir, rm } from 'node:fs/promises';
import net from 'node:net';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const webRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const repoRoot = path.resolve(webRoot, '..');
const children = [];
const processStopPromises = new WeakMap();

let temporaryRoot = null;
let shuttingDown = false;
let signalExitCode = null;

for (const [signal, exitCode] of [['SIGINT', 130], ['SIGTERM', 143]]) {
  process.on(signal, () => {
    if (signalExitCode !== null) {
      return;
    }

    signalExitCode = exitCode;
    shuttingDown = true;
    void Promise.all(children.map(stopProcess));
  });
}

const exitCode = await runSmokeTests();
await cleanup();
process.exit(signalExitCode ?? exitCode);

async function runSmokeTests() {
  try {
    temporaryRoot = await mkdtemp(path.join(os.tmpdir(), 'boardoil-e2e-'));
    ensureNotInterrupted();

    const databasePath = path.join(temporaryRoot, 'boardoil.smoke.db');
    const imageRoot = path.join(temporaryRoot, 'images');
    const apiProjectPath = path.join(repoRoot, 'BoardOil.Api', 'BoardOil.Api.csproj');
    const apiPort = await reservePort();
    ensureNotInterrupted();
    const webPort = await reservePort();
    ensureNotInterrupted();
    const apiUrl = `http://127.0.0.1:${apiPort}`;
    const webUrl = `http://127.0.0.1:${webPort}`;

    await mkdir(imageRoot, { recursive: true });
    ensureNotInterrupted();
    console.log(`[e2e] Isolated data root: ${temporaryRoot}`);

    const apiEnvironment = {
      ASPNETCORE_ENVIRONMENT: 'Production',
      ASPNETCORE_URLS: apiUrl,
      ConnectionStrings__BoardOil: `Data Source=${databasePath};Default Timeout=30;Pooling=False`,
      BoardOil__DataPath: databasePath,
      BoardOil__ImageRootPath: imageRoot,
      BoardOilAuth__AllowInsecureCookies: 'true',
      BoardOilAuth__Issuer: 'boardoil-e2e',
      BoardOilAuth__Audience: 'boardoil-e2e',
      BoardOilAuth__SigningKey: 'boardoil-e2e-signing-key-12345678901234567890',
      NUGET_HTTP_CACHE_PATH: process.env.NUGET_HTTP_CACHE_PATH ?? path.join(temporaryRoot, 'nuget-http-cache')
    };

    const restoreExitCode = await runProcess('dotnet', [
      'restore',
      apiProjectPath,
      '--locked-mode',
      '-maxcpucount:1',
      '-nodeReuse:false'
    ], repoRoot, apiEnvironment);
    ensureNotInterrupted();
    if (restoreExitCode !== 0) {
      throw new Error(`API dependency restore failed (code ${restoreExitCode}).`);
    }

    const api = startProcess('dotnet', [
      'run',
      '--project',
      apiProjectPath,
      '--configuration',
      'Release',
      '--no-launch-profile',
      '--no-restore'
    ], repoRoot, apiEnvironment);
    await waitForUrl(`${apiUrl}/api/health`, api, 'API');
    ensureNotInterrupted();

    const web = startProcess(process.execPath, [
      path.join(webRoot, 'scripts', 'run-vite-with-version.mjs'),
      '--host',
      '127.0.0.1',
      '--port',
      String(webPort),
      '--strictPort'
    ], webRoot, {
      VITE_BO_API_PROXY_TARGET: apiUrl
    });
    await waitForUrl(webUrl, web, 'frontend');
    ensureNotInterrupted();

    const playwrightExecutable = process.platform === 'win32'
      ? path.join(webRoot, 'node_modules', '.bin', 'playwright.cmd')
      : path.join(webRoot, 'node_modules', '.bin', 'playwright');
    return await runProcess(playwrightExecutable, ['test', ...process.argv.slice(2)], webRoot, {
      BOARDOIL_E2E_BASE_URL: webUrl
    });
  } catch (error) {
    if (signalExitCode === null) {
      console.error(`[e2e] ${error instanceof Error ? error.message : String(error)}`);
    }

    return 1;
  }
}

async function cleanup() {
  shuttingDown = true;
  await Promise.all(children.map(stopProcess));
  if (temporaryRoot === null) {
    return;
  }

  await rm(temporaryRoot, { recursive: true, force: true });
  console.log('[e2e] Removed isolated data root.');
  temporaryRoot = null;
}

function ensureNotInterrupted() {
  if (signalExitCode !== null) {
    throw new Error('Browser smoke run was interrupted.');
  }
}

function startProcess(command, args, cwd, additionalEnvironment) {
  const child = spawn(command, args, {
    cwd,
    env: {
      ...process.env,
      ...additionalEnvironment
    },
    stdio: 'inherit',
    shell: false
  });

  child.on('error', error => {
    if (!shuttingDown) {
      console.error(`[e2e] Failed to start ${command}: ${error.message}`);
    }
  });

  children.push(child);
  return child;
}

function runProcess(command, args, cwd, additionalEnvironment) {
  return new Promise((resolve, reject) => {
    const child = startProcess(command, args, cwd, additionalEnvironment);
    child.once('error', reject);
    child.once('exit', code => resolve(code ?? 1));
  });
}

async function waitForUrl(url, child, label) {
  const deadline = Date.now() + 60_000;
  let lastError = null;

  while (Date.now() < deadline) {
    if (child.exitCode !== null) {
      throw new Error(`${label} exited before becoming ready (code ${child.exitCode}).`);
    }

    try {
      const response = await fetch(url);
      if (response.ok) {
        return;
      }

      lastError = new Error(`HTTP ${response.status}`);
    } catch (error) {
      lastError = error;
    }

    await delay(200);
  }

  const detail = lastError instanceof Error ? ` Last error: ${lastError.message}` : '';
  throw new Error(`${label} did not become ready within 60 seconds.${detail}`);
}

function stopProcess(child) {
  const existingStopPromise = processStopPromises.get(child);
  if (existingStopPromise) {
    return existingStopPromise;
  }

  if (child.exitCode !== null || child.signalCode !== null) {
    return Promise.resolve();
  }

  const stopPromise = new Promise(resolve => {
    const forceStop = setTimeout(() => {
      child.kill('SIGKILL');
    }, 5_000);

    child.once('exit', () => {
      clearTimeout(forceStop);
      resolve();
    });
    child.kill('SIGTERM');
  });
  processStopPromises.set(child, stopPromise);
  return stopPromise;
}

function reservePort() {
  return new Promise((resolve, reject) => {
    const server = net.createServer();
    server.unref();
    server.once('error', reject);
    server.listen(0, '127.0.0.1', () => {
      const address = server.address();
      if (!address || typeof address === 'string') {
        server.close();
        reject(new Error('Could not reserve a loopback port.'));
        return;
      }

      const port = address.port;
      server.close(error => {
        if (error) {
          reject(error);
          return;
        }

        resolve(port);
      });
    });
  });
}

function delay(milliseconds) {
  return new Promise(resolve => setTimeout(resolve, milliseconds));
}
