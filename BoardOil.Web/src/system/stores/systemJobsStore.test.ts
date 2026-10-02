import { createPinia, setActivePinia } from 'pinia';
import { watch } from 'vue';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { JobsApi } from '../../shared/api/jobsApi';
import type { Job, JobDetails, JobList } from '../../shared/types/jobTypes';
import { err, ok } from '../../shared/types/result';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import { createSystemJobsStore } from './systemJobsStore';

vi.mock('../../shared/api/jobsApi', () => ({ createJobsApi: vi.fn() }));

function job(id: number, status: Job['status'] = 'pending', updatedAtUtc = '2026-09-27T10:00:00Z'): Job {
  return { id, type: 'history.purge', status, runAfterUtc: '2026-09-27T10:00:00Z',
    payloadJson: '{}', resultJson: '{}', errorMessage: null, startedAtUtc: null,
    completedAtUtc: null, userId: null, correlationId: null,
    createdAtUtc: '2026-09-27T10:00:00Z', updatedAtUtc };
}
function details(value: Job): JobDetails {
  return { ...value, logs: [{ id: 2, level: 'info', message: 'second', dataJson: null, loggedAtUtc: '2026-09-27T10:02:00Z' },
    { id: 1, level: 'info', message: 'first', dataJson: null, loggedAtUtc: '2026-09-27T10:01:00Z' }] };
}
function page(items: Job[], offset = 0, limit = 2, totalCount = items.length): JobList {
  return { items, offset, limit, totalCount };
}
function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>(done => { resolve = done; });
  return { promise, resolve };
}
function setup() {
  const api = { getJobs: vi.fn(), getJobDetails: vi.fn() } as unknown as JobsApi & {
    getJobs: ReturnType<typeof vi.fn>; getJobDetails: ReturnType<typeof vi.fn>;
  };
  const store = createSystemJobsStore(api)();
  return { api, store };
}

beforeEach(() => setActivePinia(createPinia()));
afterEach(() => useUiFeedbackStore().clearToast());

describe('systemJobsStore', () => {
  it('keeps page membership separate from details and navigates stable pages', async () => {
    const { api, store } = setup();
    api.getJobs.mockResolvedValueOnce(ok(page([job(3), job(2)], 0, 2, 3)))
      .mockResolvedValueOnce(ok(page([job(1)], 2, 2, 3)));
    api.getJobDetails.mockResolvedValue(ok(details(job(8))));
    await store.loadJobs(0, 2);
    await store.loadDetails(8);
    expect(store.ids).toEqual([3, 2]);
    expect(store.byId[8].logs?.map(log => log.message)).toEqual(['first', 'second']);
    await store.goNextPage();
    expect(store.ids).toEqual([1]);
    expect(store.offset).toBe(2);
    expect(store.totalCount).toBe(3);
  });

  it('publishes a multi-job list merge once', async () => {
    const { api, store } = setup();
    api.getJobs.mockResolvedValue(ok(page([job(3), job(2), job(1)])));
    const published = vi.fn();
    const stop = watch(() => store.byId, published, { flush: 'sync' });

    await store.loadJobs();

    expect(published).toHaveBeenCalledTimes(1);
    expect(store.ids).toEqual([3, 2, 1]);
    stop();
  });

  it('keeps the requested page when invalidation overtakes navigation', async () => {
    const { api, store } = setup();
    const navigation = deferred<ReturnType<typeof ok<JobList>>>();
    api.getJobs.mockResolvedValueOnce(ok(page([job(3)], 0, 1, 3)))
      .mockReturnValueOnce(navigation.promise)
      .mockResolvedValueOnce(ok(page([job(2)], 1, 1, 3)));
    store.setVisible(true);
    await store.loadJobs(0, 1);

    const pending = store.goNextPage();
    await store.invalidated(null);
    navigation.resolve(ok(page([job(1)], 1, 1, 3)));
    await pending;

    expect(api.getJobs).toHaveBeenNthCalledWith(3, 1, 1);
    expect(store.offset).toBe(1);
    expect(store.ids).toEqual([1]);
  });

  it('keeps a requested page size when invalidation overtakes its response', async () => {
    const { api, store } = setup();
    const sizeChange = deferred<ReturnType<typeof ok<JobList>>>();
    api.getJobs.mockResolvedValueOnce(ok(page([job(3)], 0, 100, 3)))
      .mockReturnValueOnce(sizeChange.promise)
      .mockResolvedValueOnce(ok(page([job(3), job(2)], 0, 50, 3)));
    store.setVisible(true);
    await store.loadJobs();

    const pending = store.setPageSize(50);
    await store.invalidated(null);
    sizeChange.resolve(ok(page([job(1)], 0, 50, 3)));
    await pending;

    expect(api.getJobs).toHaveBeenNthCalledWith(3, 0, 50);
    expect(store.limit).toBe(50);
    expect(store.ids).toEqual([1]);
  });

  it('refreshes the visible page and only the affected open detail, then resyncs both on reconnect', async () => {
    const { api, store } = setup();
    api.getJobs.mockResolvedValue(ok(page([job(4)])));
    api.getJobDetails.mockResolvedValue(ok(details(job(4))));
    store.setVisible(true);
    store.setOpenDetail(4);
    await store.loadJobs();
    await store.loadDetails(4);
    await store.invalidated(5);
    expect(api.getJobs).toHaveBeenCalledTimes(2);
    expect(api.getJobDetails).toHaveBeenCalledTimes(1);
    await store.invalidated(4);
    expect(api.getJobDetails).toHaveBeenCalledTimes(2);
    await store.recovered();
    expect(api.getJobs).toHaveBeenCalledTimes(4);
    expect(api.getJobDetails).toHaveBeenCalledTimes(3);
  });

  it('applies list responses in arrival order', async () => {
    const { api, store } = setup();
    const oldPage = deferred<ReturnType<typeof ok<JobList>>>();
    api.getJobs.mockReturnValueOnce(oldPage.promise).mockResolvedValueOnce(ok(page([job(2)])));
    const first = store.loadJobs();
    await store.loadJobs();
    expect(store.ids).toEqual([2]);
    oldPage.resolve(ok(page([job(1)])));
    await first;
    expect(store.ids).toEqual([1]);
    expect(store.listLoading).toBe(false);
  });

  it('clears state on disposal and accepts outstanding list and detail responses', async () => {
    const { api, store } = setup();
    api.getJobs.mockResolvedValueOnce(ok(page([job(1)], 50, 50, 200)));
    await store.loadJobs(50, 50);
    store.setVisible(true);
    store.setOpenDetail(2);
    const oldPage = deferred<ReturnType<typeof ok<JobList>>>();
    const oldDetails = deferred<ReturnType<typeof ok<JobDetails>>>();
    api.getJobs.mockReturnValueOnce(oldPage.promise);
    api.getJobDetails.mockReturnValueOnce(oldDetails.promise);
    const list = store.loadJobs();
    const detail = store.loadDetails(2);

    store.dispose();
    expect(store.byId).toEqual({});
    expect(store.ids).toEqual([]);
    expect(store.offset).toBe(0);
    expect(store.limit).toBe(100);
    expect(store.totalCount).toBe(0);
    expect(store.listLoading).toBe(false);
    expect(store.detailLoading).toEqual({});
    expect(store.visible).toBe(false);
    expect(store.openDetailId).toBeNull();

    oldPage.resolve(ok(page([job(1)])));
    oldDetails.resolve(ok(details(job(2))));
    await Promise.all([list, detail]);
    expect(store.ids).toEqual([1]);
    expect(store.byId[2].logs).toHaveLength(2);
    expect(store.detailLoading[2]).toBe(false);
    expect(store.visible).toBe(false);
    expect(store.openDetailId).toBeNull();

    api.getJobs.mockResolvedValueOnce(ok(page([])));
    await store.loadJobs();
    expect(api.getJobs).toHaveBeenLastCalledWith(0, 100);
  });

  it('applies a late older summary and clears logs that no longer match', async () => {
    const { api, store } = setup();
    const oldPage = deferred<ReturnType<typeof ok<JobList>>>();
    api.getJobs.mockReturnValueOnce(oldPage.promise);
    api.getJobDetails.mockResolvedValue(ok(details(job(7, 'completed', '2026-09-27T10:02:00Z'))));
    const pending = store.loadJobs();
    await store.loadDetails(7);
    oldPage.resolve(ok(page([job(7, 'running', '2026-09-27T10:01:00Z')])));
    await pending;
    expect(store.byId[7].status).toBe('running');
    expect(store.byId[7].logs).toBeNull();
    expect(store.ids).toEqual([7]);
  });

  it('applies a late older detail response without retrying', async () => {
    const { api, store } = setup();
    const oldDetails = deferred<ReturnType<typeof ok<JobDetails>>>();
    api.getJobDetails.mockReturnValueOnce(oldDetails.promise);
    api.getJobs.mockResolvedValue(ok(page([job(9, 'completed', '2026-09-27T10:02:00Z')])));

    const pending = store.loadDetails(9);
    await store.loadJobs();
    oldDetails.resolve(ok(details(job(9, 'running', '2026-09-27T10:01:00Z'))));
    await pending;

    expect(api.getJobDetails).toHaveBeenCalledTimes(1);
    expect(store.byId[9].status).toBe('running');
    expect(store.byId[9].logs).toHaveLength(2);
  });

  it('retains cached logs only while the summary timestamp matches', async () => {
    const { api, store } = setup();
    const original = job(9, 'running', '2026-09-27T10:00:00.1231000Z');
    const changed = job(9, 'completed', '2026-09-27T10:00:00.1239000Z');
    const result = details(original);
    api.getJobDetails.mockResolvedValue(ok(result));
    await store.loadDetails(9);
    api.getJobs.mockResolvedValueOnce(ok(page([original])))
      .mockResolvedValueOnce(ok(page([changed])));

    await store.loadJobs();
    expect(store.byId[9].logs?.map(log => log.id)).toEqual([1, 2]);
    expect(result.logs.map(log => log.id)).toEqual([2, 1]);
    await store.loadJobs();
    expect(store.byId[9].status).toBe('completed');
    expect(store.byId[9].logs).toBeNull();
  });

  it('invalidates matching cached logs while hidden without requesting data', async () => {
    const { api, store } = setup();
    api.getJobDetails.mockImplementation(async (id: number) => ok(details(job(id))));
    await store.loadDetails(1);
    await store.loadDetails(2);
    store.setVisible(false);
    store.setOpenDetail(null);

    await store.invalidated(1);
    expect(store.byId[1].logs).toBeNull();
    expect(store.byId[2].logs).toHaveLength(2);
    await store.invalidated(null);
    expect(store.byId[2].logs).toBeNull();
    await store.recovered();
    expect(api.getJobs).not.toHaveBeenCalled();
    expect(api.getJobDetails).toHaveBeenCalledTimes(2);
  });

  it('removes only the missing job after a detail 404', async () => {
    const { api, store } = setup();
    api.getJobs.mockResolvedValue(ok(page([job(1), job(2)])));
    await store.loadJobs();
    api.getJobDetails.mockResolvedValue(err({ kind: 'http', statusCode: 404, message: 'Job not found.' }));

    expect(await store.loadDetails(1)).toBeNull();

    expect(store.byId[1]).toBeUndefined();
    expect(store.jobs.map(value => value.id)).toEqual([2]);
    expect(store.detailError[1]).toBe('Job not found.');
    expect(store.detailLoading[1]).toBe(false);
  });

  it('reports request failures through inline state and feedback', async () => {
    const { api, store } = setup();
    api.getJobs.mockResolvedValue(err({ kind: 'api', message: 'History unavailable.' }));
    api.getJobDetails.mockResolvedValue(err({ kind: 'api', message: 'Job not found.' }));
    await store.loadJobs();
    await store.loadDetails(99);
    expect(store.listError).toBe('History unavailable.');
    expect(store.detailError[99]).toBe('Job not found.');
    expect(useUiFeedbackStore().toastMessage).toBe('Job not found.');
  });
});
