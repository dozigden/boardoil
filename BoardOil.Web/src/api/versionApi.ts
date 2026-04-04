import { err, ok } from '../types/result';
import type { AppError } from '../types/appError';
import type { Result } from '../types/result';
import { getEnvelope } from './http';

export type BuildInfo = {
  version: string;
  channel: string;
  build: string;
  commit: string;
};

export function getFrontendBuildInfo(): BuildInfo {
  return {
    version: normaliseString(import.meta.env.VITE_BO_VERSION as string | undefined, '0.1.0'),
    channel: normaliseString(import.meta.env.VITE_BO_CHANNEL as string | undefined, 'dev').toLowerCase(),
    build: normaliseString(import.meta.env.VITE_BO_BUILD as string | undefined, 'local'),
    commit: normaliseString(import.meta.env.VITE_BO_COMMIT as string | undefined, 'unknown')
  };
}

export async function getBackendBuildInfo(): Promise<Result<BuildInfo, AppError>> {
  const envelopeResult = await getEnvelope<BuildInfo>('/api/version');
  if (!envelopeResult.ok) {
    return envelopeResult;
  }

  const data = envelopeResult.data.data;
  if (!data) {
    return err({
      kind: 'parse',
      message: 'Build metadata payload is missing.'
    });
  }

  return ok({
    version: normaliseString(data.version, 'unknown'),
    channel: normaliseString(data.channel, 'unknown').toLowerCase(),
    build: normaliseString(data.build, 'unknown'),
    commit: normaliseString(data.commit, 'unknown')
  });
}

function normaliseString(value: string | undefined, fallback: string) {
  const trimmed = value?.trim();
  return trimmed ? trimmed : fallback;
}
