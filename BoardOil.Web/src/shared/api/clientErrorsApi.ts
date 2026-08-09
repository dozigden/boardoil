import { postJsonQuiet } from './http';

export type ClientErrorViewport = {
  width: number;
  height: number;
};

export type ClientErrorReportRequest = {
  message: string;
  exceptionType: string | null;
  stackTrace: string | null;
  phase: string;
  routeName: string | null;
  routePath: string | null;
  frontendVersion: string | null;
  viewport: ClientErrorViewport | null;
  userAgent: string | null;
  context: Record<string, unknown> | null;
};

export type ClientErrorsApi = ReturnType<typeof createClientErrorsApi>;

export function createClientErrorsApi() {
  async function reportClientError(request: ClientErrorReportRequest): Promise<void> {
    await postJsonQuiet('/api/system/error-logs:report-client-error', request);
  }

  return {
    reportClientError
  };
}
