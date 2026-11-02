import { afterEach, expect, it, vi } from 'vitest';
import { getEnvelope } from './http';
import { getSupportInfo } from './supportApi';
import { err, ok } from '../types/result';

vi.mock('./http', () => ({ getEnvelope: vi.fn() }));
afterEach(() => vi.clearAllMocks());

it('returns support information from the support endpoint', async () => {
  const data = { database: { provider: 'SQLite', version: '3.53.4', journalMode: 'delete', sizeBytes: 8192 } };
  vi.mocked(getEnvelope).mockResolvedValue(ok({ success: true, data, statusCode: 200 }));
  expect(await getSupportInfo()).toEqual(ok(data));
  expect(getEnvelope).toHaveBeenCalledWith('/api/system/support/info');
});

it('preserves errors for support page feedback', async () => {
  const failure = err({ kind: 'http' as const, statusCode: 403, message: 'Forbidden' });
  vi.mocked(getEnvelope).mockResolvedValue(failure);
  expect(await getSupportInfo()).toEqual(failure);
});
