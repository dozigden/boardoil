import type { AppError } from '../types/appError';
import type {
  ErrorLogDetails,
  ErrorLogList,
  ErrorLogPurgeResult
} from '../types/errorLogTypes';
import type { Result } from '../types/result';
import { err, ok } from '../types/result';
import { getEnvelope, postData } from './http';

export type ErrorLogsApi = ReturnType<typeof createErrorLogsApi>;

export function createErrorLogsApi() {
  async function getErrorLogs(
    offset: number,
    limit: number
  ): Promise<Result<ErrorLogList, AppError>> {
    const result = await getEnvelope<ErrorLogList>(
      `/api/system/error-logs?offset=${offset}&limit=${limit}`
    );
    if (!result.ok) {
      return result;
    }

    if (!result.data.data) {
      return err({
        kind: 'api',
        message: result.data.message ?? 'Failed to load error logs.'
      });
    }

    return ok(result.data.data);
  }

  async function getErrorLogDetails(
    errorLogId: number
  ): Promise<Result<ErrorLogDetails, AppError>> {
    const result = await getEnvelope<ErrorLogDetails>(
      `/api/system/error-logs/${errorLogId}`
    );
    if (!result.ok) {
      return result;
    }

    if (!result.data.data) {
      return err({
        kind: 'api',
        message: result.data.message ?? 'Failed to load error log details.'
      });
    }

    return ok(result.data.data);
  }

  async function purgeExpiredErrorLogs(): Promise<Result<ErrorLogPurgeResult, AppError>> {
    return postData<ErrorLogPurgeResult>('/api/system/error-logs:purge', {});
  }

  return {
    getErrorLogs,
    getErrorLogDetails,
    purgeExpiredErrorLogs
  };
}

export const errorLogsApi = createErrorLogsApi();
