import { describe, expect, it } from 'vitest';
import { compareReports, fingerprint } from './run.mjs';

const report = samples => ({ format: 1, workload: 'same-scenes', samples });

describe('goo geometry comparison', () => {
  it('compares geometry independently of source and machine metadata', () => {
    const before = { ...report({ scene: fingerprint([[1, 2], [3, 4]]) }), sourceHash: 'before', node: 'one' };
    const after = { ...before, sourceHash: 'after', node: 'two' };
    expect(compareReports(before, after)).toEqual([]);
  });

  it('identifies changed, removed and added samples', () => {
    const before = report({ stable: 'a', changed: 'b', removed: 'c' });
    const after = report({ stable: 'a', changed: 'd', added: 'e' });
    expect(compareReports(before, after)).toEqual(['changed', 'removed', 'added']);
  });

  it('rejects incompatible workloads and empty captures', () => {
    const before = report({ scene: 'a' });
    expect(() => compareReports(before, { ...before, workload: 'different-scenes' })).toThrow('Incompatible');
    expect(() => compareReports(before, { ...before, format: 2 })).toThrow('Incompatible');
    expect(() => compareReports(report({}), before)).toThrow('Empty');
  });

  it('detects field and contour changes without rounding coordinates', () => {
    expect(fingerprint({ values: new Float32Array([.5, 1]) })).toBe(fingerprint({ values: [.5, 1] }));
    expect(fingerprint([[1, 2]])).not.toBe(fingerprint([[1 + Number.EPSILON, 2]]));
    expect(fingerprint([[1, 2], [3, 4]])).not.toBe(fingerprint([[3, 4], [1, 2]]));
    expect(() => fingerprint({ point: [1, NaN] })).toThrow('Non-finite');
    expect(() => fingerprint(new Float32Array([Infinity]))).toThrow('Non-finite');
  });
});
