import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { err, ok } from '../types/result';
import type { AppError } from '../types/appError';
import type { ApiEnvelope } from '../types/boardTypes';
import type { BuildInfo } from './versionApi';
import { getEnvelope } from './http';
import { getBackendBuildInfo, getFrontendBuildInfo } from './versionApi';

vi.mock('./http', () => ({
  getEnvelope: vi.fn()
}));

describe('versionApi', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.unstubAllEnvs();
  });

  it('returns a parse error when backend metadata payload is missing', async () => {
    vi.mocked(getEnvelope).mockResolvedValue(
      ok<ApiEnvelope<BuildInfo>>({
        success: true,
        data: null,
        statusCode: 200
      })
    );

    const result = await getBackendBuildInfo();

    expect(result.ok).toBe(false);
    if (result.ok) {
      throw new Error('Expected error result.');
    }

    expect(result.error.kind).toBe('parse');
    expect(result.error.message).toBe('Build metadata payload is missing.');
  });

  it('propagates backend load errors from the shared HTTP client', async () => {
    const backendError: AppError = {
      kind: 'http',
      statusCode: 503,
      message: 'Service unavailable'
    };
    vi.mocked(getEnvelope).mockResolvedValue(err(backendError));

    const result = await getBackendBuildInfo();

    expect(result.ok).toBe(false);
    if (result.ok) {
      throw new Error('Expected error result.');
    }

    expect(result.error).toEqual(backendError);
  });

  it('falls back to local defaults when frontend env vars are not set', () => {
    const buildInfo = getFrontendBuildInfo();

    expect(buildInfo).toEqual({
      version: '0.2.0',
      channel: 'dev',
      build: 'local',
      commit: 'unknown'
    });
  });
});
