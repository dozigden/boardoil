import { describe, expect, it } from 'vitest';
import { describeSchedule } from './scheduledJobPresentation';

describe('schedule presentation', () => {
  it.each([
    ['03:00:00', 'Daily at 03:00'],
    ['18:45:00', 'Daily at 18:45'],
    ['00:00:00', 'Daily at 00:00'],
    ['03:00:15', 'Daily at 03:00:15'],
    ['03:00:15.125', 'Daily at 03:00:15.125']
  ])('describes %s', (time, expected) => {
    expect(describeSchedule({ kind: 'daily', dailyTime: time })).toBe(expected);
  });

  it('describes a Once schedule without a daily time', () => {
    expect(describeSchedule({ kind: 'once', dailyTime: null })).toBe('Once');
  });
});
