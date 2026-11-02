import type { AppError } from '../types/appError';
import type { Result } from '../types/result';
import type { SupportInfo } from '../types/supportTypes';
import { err, ok } from '../types/result';
import { getEnvelope } from './http';

export async function getSupportInfo(): Promise<Result<SupportInfo, AppError>> {
  const result = await getEnvelope<SupportInfo>('/api/system/support/info');
  if (!result.ok) return result;
  if (!result.data.data) {
    return err({ kind: 'api', message: result.data.message ?? 'Failed to load support information.' });
  }
  return ok(result.data.data);
}
