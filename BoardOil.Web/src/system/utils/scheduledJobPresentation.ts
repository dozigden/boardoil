// TimeOnly is serialised as HH:mm:ss with optional fractional seconds.
export function describeSchedule(dailyTime: string): string {
  let displayTime = dailyTime;
  if (dailyTime.endsWith(':00')) displayTime = dailyTime.slice(0, -3);
  return `Daily at ${displayTime}`;
}

export function formatScheduleDate(value: string, timeZone: string): string {
  return new Intl.DateTimeFormat(undefined, {
    timeZone, dateStyle: 'medium', timeStyle: 'short'
  }).format(new Date(value));
}
