import type { AppError } from '../types/appError';
import type { Result } from '../types/result';
import { err, ok } from '../types/result';
import type { RunScheduledJobResult, ScheduledJob } from '../types/scheduledJobTypes';
import { getEnvelope, postData } from './http';

export type ScheduledJobsApi = ReturnType<typeof createScheduledJobsApi>;
export function createScheduledJobsApi() {
  async function read<T>(path: string): Promise<Result<T, AppError>> {
    const result = await getEnvelope<T>(path);
    if (!result.ok) return result;
    if (result.data.data == null) return err({ kind: 'api', message: 'Schedule settings were missing from the response.' });
    return ok(result.data.data);
  }
  return {
    getSchedules: () => read<ScheduledJob[]>('/api/system/scheduled-jobs'),
    runNow: (name: string) => postData<RunScheduledJobResult>(`/api/system/scheduled-jobs/${encodeURIComponent(name)}/runs`, {})
  };
}
