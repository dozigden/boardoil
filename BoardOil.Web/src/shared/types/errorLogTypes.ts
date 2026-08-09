export type ErrorLog = {
  id: number;
  occurredAtUtc: string;
  source: string;
  area: string;
  exceptionType: string;
  message: string;
  traceIdentifier: string | null;
  requestMethod: string | null;
  requestPath: string | null;
  actorUserId: number | null;
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type ErrorLogDetails = ErrorLog & {
  stackTrace: string | null;
  contextJson: string | null;
};

export type ErrorLogList = {
  items: ErrorLog[];
  offset: number;
  limit: number;
  totalCount: number;
};

export type ErrorLogPurgeResult = {
  retentionDays: number;
  cutoffUtc: string;
  deletedCount: number;
};
