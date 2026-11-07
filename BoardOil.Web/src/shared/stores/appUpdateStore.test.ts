import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { getBackendBuildInfo, getFrontendBuildInfo } from '../api/versionApi';
import { hasPendingWrites } from '../api/http';
import { err, ok } from '../types/result';
import { useAppUpdateStore } from './appUpdateStore';

const { reload } = vi.hoisted(() => ({ reload: vi.fn() }));
vi.mock('../api/versionApi', () => ({ getBackendBuildInfo: vi.fn(), getFrontendBuildInfo: vi.fn() }));
vi.mock('../api/http', () => ({ hasPendingWrites: vi.fn() }));

const current = { version: '1.8.0', channel: 'nightly', build: '100', commit: 'abc123' };
const intervalMs = 5 * 60 * 1000;

describe('application updates', () => {
  let documentStub: EventTarget & { visibilityState: string };
  let store: ReturnType<typeof useAppUpdateStore>;

  beforeEach(() => {
    vi.resetAllMocks();
    vi.useFakeTimers();
    setActivePinia(createPinia());
    documentStub = Object.assign(new EventTarget(), { visibilityState: 'visible' });
    vi.stubGlobal('document', documentStub);
    vi.stubGlobal('window', { location: { reload } });
    vi.mocked(getFrontendBuildInfo).mockReturnValue(current);
    vi.mocked(getBackendBuildInfo).mockResolvedValue(ok(current));
    vi.mocked(hasPendingWrites).mockReturnValue(false);
    store = useAppUpdateStore();
  });

  afterEach(() => {
    store.dispose();
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  async function startWithUpdate() {
    vi.mocked(getBackendBuildInfo).mockResolvedValue(ok({ ...current, build: '101' }));
    store.initialize();
    await vi.advanceTimersByTimeAsync(0);
  }

  it('checks once at startup and periodically, without duplicating initialization', async () => {
    store.initialize();
    store.initialize();
    await vi.advanceTimersByTimeAsync(0);
    expect(getBackendBuildInfo).toHaveBeenCalledTimes(1);
    expect(store.updateAvailable).toBe(false);
    await vi.advanceTimersByTimeAsync(intervalMs);
    expect(getBackendBuildInfo).toHaveBeenCalledTimes(2);
  });

  it.each([
    { ...current, build: '101' },
    { ...current, build: '99' },
    { ...current, commit: 'older-commit' },
    { ...current, version: '1.7.0' },
    { ...current, channel: 'release' }
  ])('detects a different deployment, including rollbacks: %j', async deployed => {
    vi.mocked(getBackendBuildInfo).mockResolvedValue(ok(deployed));
    store.initialize();
    await vi.advanceTimersByTimeAsync(0);
    expect(store.updateAvailable).toBe(true);
    expect(reload).not.toHaveBeenCalled();
  });

  it('pauses checks in hidden tabs and checks when returning', async () => {
    documentStub.visibilityState = 'hidden';
    store.initialize();
    await vi.advanceTimersByTimeAsync(intervalMs);
    expect(getBackendBuildInfo).not.toHaveBeenCalled();
    documentStub.visibilityState = 'visible';
    documentStub.dispatchEvent(new Event('visibilitychange'));
    await vi.advanceTimersByTimeAsync(0);
    expect(getBackendBuildInfo).toHaveBeenCalledTimes(1);
  });

  it('retains a detected update during failures and clears it if the loaded build returns', async () => {
    await startWithUpdate();
    vi.mocked(getBackendBuildInfo).mockResolvedValue(err({ kind: 'network', message: 'Offline' }));
    await vi.advanceTimersByTimeAsync(intervalMs);
    expect(store.updateAvailable).toBe(true);
    expect(store.refreshMessage).toBe('');
    vi.mocked(getBackendBuildInfo).mockResolvedValue(ok(current));
    await vi.advanceTimersByTimeAsync(intervalMs);
    expect(store.updateAvailable).toBe(false);
  });

  it('coalesces checks and times out a stalled request so a later check can retry', async () => {
    vi.mocked(getBackendBuildInfo).mockImplementationOnce(signal => new Promise(resolve => {
      signal!.addEventListener('abort', () => resolve(err({ kind: 'network', message: 'Aborted' })));
    }));
    store.initialize();
    documentStub.dispatchEvent(new Event('visibilitychange'));
    expect(getBackendBuildInfo).toHaveBeenCalledTimes(1);
    await vi.advanceTimersByTimeAsync(10_000);
    documentStub.dispatchEvent(new Event('visibilitychange'));
    await vi.advanceTimersByTimeAsync(0);
    expect(getBackendBuildInfo).toHaveBeenCalledTimes(2);
  });

  it('stops timers and listeners, aborts the request and ignores its late result on disposal', async () => {
    let complete!: (result: Awaited<ReturnType<typeof getBackendBuildInfo>>) => void;
    vi.mocked(getBackendBuildInfo).mockImplementationOnce(() => new Promise(resolve => { complete = resolve; }));
    store.initialize();
    const signal = vi.mocked(getBackendBuildInfo).mock.calls[0]![0]!;
    store.dispose();
    expect(signal.aborted).toBe(true);
    complete(ok({ ...current, build: '101' }));
    documentStub.dispatchEvent(new Event('visibilitychange'));
    await vi.advanceTimersByTimeAsync(intervalMs);
    expect(store.updateAvailable).toBe(false);
    expect(getBackendBuildInfo).toHaveBeenCalledTimes(1);
  });

  it('reloads immediately when requested', async () => {
    await startWithUpdate();
    store.refresh();
    expect(reload).toHaveBeenCalledTimes(1);
  });

  it('blocks refresh during writes and allows it once they finish', async () => {
    await startWithUpdate();
    vi.mocked(hasPendingWrites).mockReturnValue(true);
    store.refresh();
    expect(store.refreshMessage).toContain('save or upload');
    expect(reload).not.toHaveBeenCalled();
    vi.mocked(hasPendingWrites).mockReturnValue(false);
    store.refresh();
    expect(reload).toHaveBeenCalledTimes(1);
    expect(store.refreshMessage).toBe('');
  });

  it('does not reload after disposal', async () => {
    await startWithUpdate();
    store.dispose();
    store.refresh();
    expect(reload).not.toHaveBeenCalled();
  });
});
