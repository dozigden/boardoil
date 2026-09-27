// Ported from KST's schedule descriptions; BoardOil's four definitions share this cadence.
export function describeSchedule(expression: string): string {
  if (expression.trim().split(/\s+/).join(' ') === '0 3 * * *') return 'Daily at 03:00';
  return expression;
}

export function formatScheduleDate(value: string, timeZone: string): string {
  return new Intl.DateTimeFormat(undefined, {
    timeZone, dateStyle: 'medium', timeStyle: 'short'
  }).format(new Date(value));
}
