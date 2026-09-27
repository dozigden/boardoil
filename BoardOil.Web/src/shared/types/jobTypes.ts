export type Job = {
  id: number;
  type: string;
  status: 'pending' | 'running' | 'completed' | 'failed' | 'cancelled';
  runAfterUtc: string;
  payloadJson: string;
  resultJson: string;
  errorMessage: string | null;
  startedAtUtc: string | null;
  completedAtUtc: string | null;
  userId: number | null;
  correlationId: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type JobLog = {
  id: number;
  level: 'info' | 'warning' | 'error';
  message: string;
  dataJson: string | null;
  loggedAtUtc: string;
};

export type JobDetails = Job & { logs: JobLog[] };
export type JobList = { items: Job[]; offset: number; limit: number; totalCount: number };
