import type { ScheduledJobTiming } from '../../shared/types/scheduledJobTypes';

export function describeSchedule(schedule: ScheduledJobTiming): string {
  if (schedule.kind === 'once') return 'Once';
  const dailyTime = schedule.dailyTime;
  let displayTime = dailyTime;
  if (dailyTime.endsWith(':00')) displayTime = dailyTime.slice(0, -3);
  return `Daily at ${displayTime}`;
}

export function formatScheduleDate(value: string, timeZone: string): string {
  return new Intl.DateTimeFormat(undefined, {
    timeZone, dateStyle: 'medium', timeStyle: 'short'
  }).format(new Date(value));
}
