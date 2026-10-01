import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useSystemInfoMessageStore } from './systemInfoMessageStore';
import { err, ok } from '../types/result';
import type { SystemInfoMessageDto } from '../types/configurationTypes';
import type { AppError } from '../types/appError';
import type { Result } from '../types/result';

const api = {
  getSystemInfoMessage: vi.fn(),
  updateSystemInfoMessage: vi.fn()
};

vi.mock('../api/systemApi', () => ({
  createSystemApi: () => api
}));

describe('systemInfoMessageStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.resetAllMocks();
  });

  it.each([null, makeMessage('Maintenance')])('caches a successful load, including an empty message (%j)', async message => {
    const store = useSystemInfoMessageStore();
    api.getSystemInfoMessage.mockResolvedValueOnce(ok(message));

    expect(await store.load()).toBe(true);
    expect(await store.load()).toBe(true);
    expect(store.message).toEqual(message);
    expect(store.loaded).toBe(true);
    expect(store.busy).toBe(false);
    expect(api.getSystemInfoMessage).toHaveBeenCalledTimes(1);
  });

  it('refreshes a realtime message on forced load and reloads after clearing', async () => {
    const store = useSystemInfoMessageStore();
    store.setMessage(makeMessage('Realtime'));
    expect(await store.load()).toBe(true);
    expect(api.getSystemInfoMessage).not.toHaveBeenCalled();

    api.getSystemInfoMessage.mockResolvedValueOnce(ok(makeMessage('Refreshed')));
    expect(await store.load(true)).toBe(true);
    expect(store.message?.title).toBe('Refreshed');

    store.clear();
    expect(store.message).toBeNull();
    expect(store.loaded).toBe(false);
    api.getSystemInfoMessage.mockResolvedValueOnce(ok(makeMessage('Reloaded')));
    expect(await store.load()).toBe(true);
    expect(store.message?.title).toBe('Reloaded');
    expect(api.getSystemInfoMessage).toHaveBeenCalledTimes(2);
  });

  it.each(['success', 'failure'] as const)('applies only successful save responses and settles saving on %s', async outcome => {
    const store = useSystemInfoMessageStore();
    const original = makeMessage('Original');
    const draft = makeMessage('  Updated  ');
    const saved = makeMessage('Updated');
    store.setMessage(original);
    let complete!: (result: Result<SystemInfoMessageDto | null, AppError>) => void;
    api.updateSystemInfoMessage.mockReturnValueOnce(new Promise(resolve => { complete = resolve; }));

    const pending = store.save(draft);
    expect(store.saving).toBe(true);
    expect(store.message).toEqual(original);
    expect(api.updateSystemInfoMessage).toHaveBeenCalledWith(draft);

    const result = outcome === 'success' ? ok(saved) : err<AppError>({ kind: 'api', message: 'Save failed.' });
    complete(result);
    expect(await pending).toEqual(result);
    expect(store.message).toEqual(outcome === 'success' ? saved : original);
    expect(store.saving).toBe(false);
    expect(store.loaded).toBe(true);
    expect(api.getSystemInfoMessage).not.toHaveBeenCalled();
    expect(draft.title).toBe('  Updated  ');
  });

  it('applies an empty successful save response as a loaded empty message', async () => {
    const store = useSystemInfoMessageStore();
    store.setMessage(makeMessage('Original'));
    api.updateSystemInfoMessage.mockResolvedValueOnce(ok(null));

    expect(await store.save(null)).toEqual(ok(null));
    expect(store.message).toBeNull();
    expect(store.loaded).toBe(true);
    expect(store.saving).toBe(false);
  });
});

function makeMessage(title: string): SystemInfoMessageDto {
  return {
    enabled: true,
    emoji: null,
    title,
    description: 'Maintenance details',
    styleName: 'presets',
    stylePropertiesJson: '{"presetIndex":2}'
  };
}
