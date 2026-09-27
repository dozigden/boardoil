import type { Job } from './jobTypes';

export type ScheduledJobRun = { id: number; status: Job['status']; startedAtUtc: string | null };
export type ScheduledJob = {
  name: string;
  displayName: string;
  enabled: boolean;
  dailyTime: string;
  timeZoneId: string;
  lastEvaluatedAtUtc: string | null;
  nextOccurrenceUtc: string | null;
  currentJob: ScheduledJobRun | null;
  latestStartedJob: ScheduledJobRun | null;
};
export type RunScheduledJobResult = { enqueuedCount: number; jobIds: number[] };
export type SystemTimeZone = { systemTimeZoneId: string };
export type SystemTimeZoneOptions = { defaultId: string; options: { id: string; displayName: string }[] };
