import type { AppError } from '../types/appError';
import type { JobDetails, JobList } from '../types/jobTypes';
import type { Result } from '../types/result';
import { err, ok } from '../types/result';
import { getEnvelope } from './http';

export type JobsApi = ReturnType<typeof createJobsApi>;

export function createJobsApi() {
  async function getJobs(offset: number, limit: number): Promise<Result<JobList, AppError>> {
    const result = await getEnvelope<JobList>(`/api/system/jobs?offset=${offset}&limit=${limit}`);
    if (!result.ok) return result;
    if (!result.data.data) return err({ kind: 'api', message: 'Job history was missing from the response.' });
    return ok(result.data.data);
  }

  async function getJobDetails(id: number): Promise<Result<JobDetails, AppError>> {
    const result = await getEnvelope<JobDetails>(`/api/system/jobs/${id}`);
    if (!result.ok) return result;
    if (!result.data.data) return err({ kind: 'api', message: 'Job details were missing from the response.' });
    return ok(result.data.data);
  }

  return { getJobs, getJobDetails };
}
