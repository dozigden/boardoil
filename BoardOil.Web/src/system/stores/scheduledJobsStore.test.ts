import { createPinia, setActivePinia } from 'pinia';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createScheduledJobsStore } from './scheduledJobsStore';
import type { ScheduledJob } from '../../shared/types/scheduledJobTypes';
import { err, ok } from '../../shared/types/result';

vi.mock('../../shared/api/scheduledJobsApi', () => ({ createScheduledJobsApi: vi.fn() }));

function schedule(zone = 'UTC', currentId: number | null = null): ScheduledJob {
  return { name: 'history-purge', displayName: 'Job history purge', enabled: true,
    cronExpression: '0 3 * * *', timeZoneId: zone, lastEvaluatedAtUtc: null,
    nextOccurrenceUtc: '2026-10-01T03:00:00Z', latestStartedJob: null,
    currentJob: currentId === null ? null : { id: currentId, status: 'pending', startedAtUtc: null } };
}
function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>(done => { resolve = done; });
  return { promise, resolve };
}
function setup() {
  const api = {
    getSchedules: vi.fn().mockResolvedValue(ok([schedule()])),
    runNow: vi.fn().mockResolvedValue(ok({ enqueuedCount: 1, jobIds: [7] }))
  };
  return { api, store: createScheduledJobsStore(api)() };
}

beforeEach(() => setActivePinia(createPinia()));

describe('scheduled jobs', () => {
  it('loads schedules on entry', async () => {
    const { store } = setup();
    await store.loadSchedules();
    expect(store.schedules).toEqual([schedule()]);
    expect(store.loading).toBe(false);
  });

  it('prevents duplicate submissions for one row while allowing other rows', async () => {
    const { api, store } = setup();
    const pending = deferred<ReturnType<typeof ok<{ enqueuedCount: number; jobIds: number[] }>>>();
    api.runNow.mockReturnValueOnce(pending.promise);
    const first = store.runNow('history-purge');
    await store.runNow('history-purge');
    await store.runNow('error-log-purge');
    expect(api.runNow).toHaveBeenCalledTimes(2);
    expect(store.running['history-purge']).toBe(true);
    expect(store.running['error-log-purge']).toBe(false);
    pending.resolve(ok({ enqueuedCount: 1, jobIds: [8] }));
    await first;
    expect(store.runResults['history-purge'].jobIds).toEqual([8]);
    expect(store.running['history-purge']).toBe(false);
  });

  it.each([{ ids: [] }, { ids: [7] }, { ids: [7, 8] }])('preserves the actual queued IDs $ids and refreshes job links', async ({ ids }) => {
    const { api, store } = setup();
    api.runNow.mockResolvedValue(ok({ enqueuedCount: ids.length, jobIds: ids }));
    api.getSchedules.mockResolvedValue(ok([schedule('UTC', ids[0] ?? null)]));
    await store.runNow('history-purge');
    expect(store.runResults['history-purge']).toEqual({ enqueuedCount: ids.length, jobIds: ids });
    expect(store.schedules[0].currentJob?.id ?? null).toBe(ids[0] ?? null);
    expect(api.getSchedules).toHaveBeenCalledTimes(1);
  });

  it('retains a failed run message without claiming a job was queued', async () => {
    const { api, store } = setup();
    api.runNow.mockResolvedValue(err({ kind: 'http', message: 'Queue failed.' }));
    await store.runNow('history-purge');
    expect(store.runErrors['history-purge']).toBe('Queue failed.');
    expect(store.runResults['history-purge']).toBeUndefined();
    expect(store.running['history-purge']).toBe(false);
    expect(api.getSchedules).not.toHaveBeenCalled();
  });

  it('keeps a successful enqueue acknowledgement if its subsequent refresh fails', async () => {
    const { api, store } = setup();
    api.getSchedules.mockResolvedValue(err({ kind: 'network', message: 'Refresh failed.' }));
    await store.runNow('history-purge');
    expect(store.runResults['history-purge'].jobIds).toEqual([7]);
    expect(store.error).toBe('Refresh failed.');
  });





  it('ignores an older list response after a newer refresh', async () => {
    const { api, store } = setup();
    const pending = deferred<ReturnType<typeof ok<ScheduledJob[]>>>();
    api.getSchedules.mockReturnValueOnce(pending.promise);
    const old = store.loadSchedules();
    api.getSchedules.mockResolvedValue(ok([schedule('Europe/London', 7)]));
    await store.loadSchedules();
    pending.resolve(ok([schedule()]));
    await old;
    expect(store.schedules).toEqual([schedule('Europe/London', 7)]);
  });

  it('ignores initial data and errors after disposal', async () => {
    const { api, store } = setup();
    const pending = deferred<ReturnType<typeof ok<ScheduledJob[]>>>();
    api.getSchedules.mockReturnValueOnce(pending.promise);
    const load = store.loadSchedules();
    store.dispose();
    pending.resolve(ok([schedule()]));
    await load;
    expect(store.schedules).toEqual([]);
    expect(store.loading).toBe(false);
  });

  it('does not publish a late run result or refresh a new page lifetime', async () => {
    const { api, store } = setup();
    const pending = deferred<ReturnType<typeof ok<{ enqueuedCount: number; jobIds: number[] }>>>();
    api.runNow.mockReturnValueOnce(pending.promise);
    const run = store.runNow('history-purge');
    store.dispose();
    await store.loadSchedules();
    pending.resolve(ok({ enqueuedCount: 1, jobIds: [8] }));
    await run;
    expect(store.runResults).toEqual({});
    expect(store.running).toEqual({});
    expect(api.getSchedules).toHaveBeenCalledTimes(1);
  });

});
