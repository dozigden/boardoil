import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';

// BoardOil #1040 tracks removal of this exception and the upstream fix.
const expiresAt = '2026-11-25T08:40:00Z';
const advisoryUrl = 'https://github.com/advisories/GHSA-fc5q-39xc-vjpf';
const packageName = 'picomatch';
const packageVersion = '4.0.7';

const auditArgs = [
  'audit', '--package-lock-only', '--include=dev', '--include=optional',
  '--include=peer', '--audit-level=high'
];

// Use npm's original gate once the exception has expired.
if (Date.now() >= Date.parse(expiresAt)) {
  const audit = spawnSync('npm', auditArgs, { stdio: 'inherit' });
  process.exit(audit.status ?? 1);
}

const audit = spawnSync('npm', [...auditArgs, '--json'], {
  encoding: 'utf8',
  maxBuffer: 10 * 1024 * 1024,
  stdio: ['ignore', 'pipe', 'inherit']
});
process.stdout.write(audit.stdout ?? '');

if (audit.error || (audit.status !== 0 && audit.status !== 1)) {
  throw new Error('npm audit did not complete successfully.', { cause: audit.error });
}

const report = JSON.parse(audit.stdout);
const totals = report.metadata?.vulnerabilities;
const vulnerabilities = report.vulnerabilities;
if (report.error || report.auditReportVersion !== 2 || !vulnerabilities
  || typeof vulnerabilities !== 'object' || Array.isArray(vulnerabilities) || !totals) {
  throw new Error('npm audit did not return a complete report.');
}

const severities = ['info', 'low', 'moderate', 'high', 'critical'];
const isBlocking = (severity) => severity === 'high' || severity === 'critical';
const findings = Object.entries(vulnerabilities);
for (const [name, finding] of findings) {
  if (finding.name !== name || !severities.includes(finding.severity)
    || !Array.isArray(finding.via) || finding.via.length === 0
    || !finding.via.every(cause => typeof cause === 'string'
      ? Object.hasOwn(vulnerabilities, cause)
      : cause && severities.includes(cause.severity) && typeof cause.url === 'string')) {
    throw new Error(`npm audit returned an invalid finding for ${name}.`);
  }
}

const blockingFindings = findings.filter(([, finding]) => isBlocking(finding.severity));
const criticalCount = blockingFindings.filter(([, finding]) => finding.severity === 'critical').length;
if (criticalCount !== totals.critical || blockingFindings.length - criticalCount !== totals.high
  || (audit.status === 0) !== (blockingFindings.length === 0)) {
  throw new Error('npm audit status does not match its findings.');
}
if (audit.status === 0) {
  process.exitCode = 0;
} else {
  process.exitCode = 1;

  // String causes are inherited findings. Inspect each underlying advisory once.
  const blockingAdvisories = findings.flatMap(([, finding]) =>
    finding.via.filter(cause => typeof cause !== 'string' && isBlocking(cause.severity))
  );
  const [advisory] = blockingAdvisories;
  if (blockingAdvisories.length === 1 && advisory.name === packageName
    && advisory.dependency === packageName && advisory.url === advisoryUrl
    && advisory.severity === 'high' && criticalCount === 0) {
    const lock = JSON.parse(readFileSync('package-lock.json', 'utf8'));
    const nodes = vulnerabilities[packageName]?.nodes;
    const affected = new Set([packageName]);

    // Follow inherited findings back to picomatch, including indirect chains.
    let previousSize;
    do {
      previousSize = affected.size;
      for (const [name, finding] of findings) {
        if (finding.via.some(cause => affected.has(cause))) {
          affected.add(name);
        }
      }
    } while (affected.size !== previousSize);

    if (Array.isArray(nodes) && nodes.length > 0
      && nodes.every(node => lock.packages?.[node]?.version === packageVersion)
      && blockingFindings.every(([name]) => affected.has(name))) {
      console.warn(`Nightly exception for ${advisoryUrl} on ${packageName} ${packageVersion} expires at ${expiresAt}.`);
      process.exitCode = 0;
    }
  }
}
