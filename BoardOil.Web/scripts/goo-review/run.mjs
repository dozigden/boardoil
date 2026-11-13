import { createHash } from 'node:crypto';
import { mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { cpus, tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { performance } from 'node:perf_hooks';
import ts from 'typescript';
import { NumericalPath, runWorkload } from './workload.mjs';

const directory = dirname(fileURLToPath(import.meta.url));
const defaultSource = resolve(directory, '../../src/board/goo');
const require = createRequire(import.meta.url);
const hash = text => createHash('sha256').update(text).digest('hex');
const workload = hash(['scenes.mjs', 'workload.mjs'].map(name => readFileSync(join(directory, name), 'utf8')).join('\n'));

export function fingerprint(value) {
  return hash(JSON.stringify(value, (_key, item) => {
    if (typeof item === 'number' && !Number.isFinite(item)) throw new Error('Non-finite geometry value');
    if (ArrayBuffer.isView(item)) return Array.from(item);
    return item;
  }));
}

export function compareReports(before, after) {
  if (before.format !== 1 || after.format !== 1 || before.workload !== after.workload) {
    throw new Error('Incompatible report format or scenes; capture both versions with the same harness');
  }
  if (!Object.keys(before.samples).length || !Object.keys(after.samples).length) throw new Error('Empty geometry report');
  return [...new Set([...Object.keys(before.samples), ...Object.keys(after.samples)])]
    .filter(key => before.samples[key] !== after.samples[key]);
}

function loadGeometry(source) {
  const temporary = mkdtempSync(join(tmpdir(), 'boardoil-goo-review-'));
  const cleanup = () => {
    for (const key of Object.keys(require.cache)) if (key.startsWith(temporary)) delete require.cache[key];
    rmSync(temporary, { recursive: true, force: true });
  };
  try {
    writeFileSync(join(temporary, 'package.json'), '{"type":"commonjs"}');
    const files = readdirSync(source).filter(name => name.endsWith('.ts') && !name.endsWith('.test.ts')).sort();
    const sources = files.map(name => [name, readFileSync(join(source, name), 'utf8')]);
    for (const [name, original] of sources) {
      let text = original;
      // Expose the existing field builder only in this disposable compiled copy.
      if (name === 'liquidSurface.ts') text += '\nexport { buildField };';
      const output = ts.transpileModule(text, { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS } });
      writeFileSync(join(temporary, name.replace(/\.ts$/, '.js')), output.outputText);
    }
    const api = Object.assign({}, ...['physics', 'smooth', 'surface', 'smoothLayers', 'liquidSurface',
      'liquidResidency', 'liquidDensity', 'liquidCardSamples'].map(name => require(join(temporary, `${name}.js`))));
    return { api, cleanup, sourceHash: hash(JSON.stringify(sources)) };
  } catch (error) {
    cleanup();
    throw error;
  }
}

export function run(args) {
  const [command, ...rest] = args;
  if (command === 'compare' && rest.length === 2) {
    const [before, after] = rest.map(path => JSON.parse(readFileSync(path, 'utf8')));
    const differences = compareReports(before, after);
    if (differences.length) {
      console.error(`${differences.length} changed or missing geometry samples:\n${differences.join('\n')}`);
      return 1;
    }
    console.log(`PASS: ${Object.keys(before.samples).length} exact geometry samples match`);
    return 0;
  }
  if ((command !== 'capture' || rest.length < 1 || rest.length > 2) &&
      (command !== 'benchmark' || rest.length > 1)) {
    throw new Error('Usage: node scripts/goo-review/run.mjs capture <output.json> [source-directory]\n' +
      '       node scripts/goo-review/run.mjs compare <before.json> <after.json>\n' +
      '       node scripts/goo-review/run.mjs benchmark [source-directory]');
  }
  let source = rest[0];
  if (command === 'capture') source = rest[1];
  const loaded = loadGeometry(resolve(source ?? defaultSource));
  const previousPath = globalThis.Path2D;
  globalThis.Path2D = NumericalPath;
  try {
    const metadata = { format: 1, workload, sourceHash: loaded.sourceHash, node: process.version,
      platform: `${process.platform}/${process.arch}`, cpu: cpus()[0]?.model };
    if (command === 'capture') {
      const samples = {};
      runWorkload(loaded.api, (key, value) => { samples[key] = fingerprint(value); });
      writeFileSync(rest[0], JSON.stringify({ ...metadata, samples }, null, 2) + '\n');
      console.log(`Captured ${Object.keys(samples).length} geometry samples to ${rest[0]}`);
    } else {
      for (let i = 0; i < 2; i++) runWorkload(loaded.api);
      const timings = new Map();
      for (let i = 0; i < 7; i++) {
        const totals = new Map();
        runWorkload(loaded.api, undefined, (name, work) => {
          const start = performance.now();
          const result = work();
          totals.set(name, (totals.get(name) ?? 0) + performance.now() - start);
          return result;
        });
        for (const [name, elapsed] of totals) {
          if (!timings.has(name)) timings.set(name, []);
          timings.get(name).push(elapsed);
        }
      }
      const phases = Object.fromEntries([...timings].map(([name, times]) => {
        const sorted = [...times].sort((a, b) => a - b);
        return [name, { medianMs: sorted[3], minMs: sorted[0], maxMs: sorted[6], samplesMs: times }];
      }));
      console.log(JSON.stringify({ ...metadata, units: 'milliseconds per complete anonymous workload; CPU only, not frame time',
        warmups: 2, runs: 7, phases }, null, 2));
    }
    return 0;
  } finally {
    if (previousPath === undefined) delete globalThis.Path2D;
    else globalThis.Path2D = previousPath;
    loaded.cleanup();
  }
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try { process.exitCode = run(process.argv.slice(2)); }
  catch (error) { console.error(error.message); process.exitCode = 1; }
}
