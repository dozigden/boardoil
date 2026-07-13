import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useTagStore } from './tagStore';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import { err, ok } from '../../shared/types/result';
import type { Tag } from '../../shared/types/boardTypes';

const api = {
  getTags: vi.fn(),
  getTagCreateDefaultStyle: vi.fn(),
  createTag: vi.fn(),
  updateTagStyle: vi.fn(),
  deleteTag: vi.fn()
};

vi.mock('../../shared/api/boardApi', () => ({
  createBoardApi: () => api
}));

describe('tagStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    api.getTags.mockResolvedValue(ok([]));
    api.getTagCreateDefaultStyle.mockResolvedValue(ok({
      styleName: 'presets',
      stylePropertiesJson: '{"presetIndex":4,"textColorMode":"auto"}'
    }));
    api.createTag.mockResolvedValue(ok(makeTag(7, 'Release', 'auto', '{}', null)));
    api.updateTagStyle.mockResolvedValue(ok(makeTag(7, 'Release', 'presets', '{"presetIndex":2,"textColorMode":"auto"}', null)));
    api.deleteTag.mockResolvedValue(ok(undefined));
  });

  it('loads tags for the selected board', async () => {
    const store = useTagStore();
    api.getTags.mockResolvedValueOnce(ok([makeTag(7, 'Release', 'presets', '{"presetIndex":2,"textColorMode":"auto"}', null)]));

    const loaded = await store.loadTags(3);

    expect(loaded).toBe(true);
    expect(api.getTags).toHaveBeenCalledWith(3);
    expect(store.tags.map(x => x.name)).toEqual(['Release']);
  });

  it('ignores stale loadTags responses when board changes mid-load', async () => {
    const store = useTagStore();
    const firstRequest = createDeferred<ReturnType<typeof ok<Tag[]>>>();
    const secondRequest = createDeferred<ReturnType<typeof ok<Tag[]>>>();

    api.getTags
      .mockImplementationOnce(() => firstRequest.promise)
      .mockImplementationOnce(() => secondRequest.promise);

    const firstLoad = store.loadTags(1);
    const secondLoad = store.loadTags(2);

    secondRequest.resolve(ok([makeTag(20, 'Second Board Tag', 'auto', '{}', null)]));
    await secondLoad;

    firstRequest.resolve(ok([makeTag(10, 'First Board Tag', 'auto', '{}', null)]));
    await firstLoad;

    expect(store.activeBoardId).toBe(2);
    expect(store.tags.map(x => x.name)).toEqual(['Second Board Tag']);
  });

  it('saveTag create flow uses one typed model through create and style update', async () => {
    const store = useTagStore();
    const model = {
      name: 'Release',
      emoji: '🚀',
      styleName: 'presets' as const,
      stylePropertiesJson: '{"presetIndex":2,"textColorMode":"auto"}'
    };

    const result = await store.saveTag(3, null, model);

    expect(result?.createdTag?.id).toBe(7);
    expect(result?.savedTag?.id).toBe(7);
    expect(api.createTag).toHaveBeenCalledWith(3, 'Release', '🚀');
    expect(api.updateTagStyle).toHaveBeenCalledWith(3, 7, model);
  });

  it('loads create default style', async () => {
    const store = useTagStore();

    const defaultStyle = await store.getCreateDefaultStyle(3);

    expect(defaultStyle?.stylePropertiesJson).toBe('{"presetIndex":4,"textColorMode":"auto"}');
    expect(api.getTagCreateDefaultStyle).toHaveBeenCalledWith(3);
  });

  it('saveTag update flow updates existing tag from typed model', async () => {
    const store = useTagStore();
    const model = {
      name: 'Release',
      emoji: null,
      styleName: 'solid' as const,
      stylePropertiesJson: '{"backgroundColor":"#336699","textColorMode":"auto","borderMode":"auto"}'
    };
    api.updateTagStyle.mockResolvedValueOnce(ok(makeTag(7, 'Release', 'solid', model.stylePropertiesJson, null)));

    const result = await store.saveTag(3, 7, model);

    expect(result?.createdTag).toBeNull();
    expect(result?.savedTag?.styleName).toBe('solid');
    expect(api.updateTagStyle).toHaveBeenCalledWith(3, 7, model);
  });

  it('returns created tag when create succeeded but style update failed', async () => {
    const store = useTagStore();
    const feedback = useUiFeedbackStore();
    const model = {
      name: 'Release',
      emoji: null,
      styleName: 'solid' as const,
      stylePropertiesJson: '{"backgroundColor":"#336699","textColorMode":"auto","borderMode":"auto"}'
    };
    api.updateTagStyle.mockResolvedValueOnce(err({ kind: 'api', message: 'Could not update style.' }));

    const result = await store.saveTag(3, null, model);

    expect(result?.createdTag?.id).toBe(7);
    expect(result?.savedTag).toBeNull();
    expect(feedback.errorMessage).toBe('Could not update style.');
  });

  it('ignores stale createTag mutation response when active board changes', async () => {
    const store = useTagStore();
    const createRequest = createDeferred<ReturnType<typeof ok<Tag>>>();
    store.activeBoardId = 1;
    store.tags = [makeTag(100, 'Board 2 Existing', 'auto', '{}', null)];

    api.createTag.mockImplementationOnce(() => createRequest.promise);
    const pendingCreate = store.createTag(1, 'Release', '🚀');

    store.activeBoardId = 2;
    createRequest.resolve(ok(makeTag(7, 'Release', 'auto', '{}', '🚀')));
    const created = await pendingCreate;

    expect(created?.id).toBe(7);
    expect(store.tags.map(x => x.name)).toEqual(['Board 2 Existing']);
  });

  it('ignores stale updateTagStyle mutation response when active board changes', async () => {
    const store = useTagStore();
    const updateRequest = createDeferred<ReturnType<typeof ok<Tag>>>();
    store.activeBoardId = 1;
    store.tags = [makeTag(100, 'Board 2 Existing', 'auto', '{}', null)];

    api.updateTagStyle.mockImplementationOnce(() => updateRequest.promise);
    const pendingUpdate = store.updateTagStyle(1, 7, {
      name: 'Release',
      emoji: null,
      styleName: 'solid',
      stylePropertiesJson: '{"backgroundColor":"#336699","textColorMode":"auto","borderMode":"auto"}'
    });

    store.activeBoardId = 2;
    updateRequest.resolve(ok(makeTag(7, 'Release', 'solid', '{"backgroundColor":"#336699","textColorMode":"auto","borderMode":"auto"}', null)));
    const updated = await pendingUpdate;

    expect(updated?.id).toBe(7);
    expect(store.tags.map(x => x.name)).toEqual(['Board 2 Existing']);
  });

  it('ignores stale deleteTag mutation response when active board changes', async () => {
    const store = useTagStore();
    const deleteRequest = createDeferred<ReturnType<typeof ok<void>>>();
    store.activeBoardId = 1;
    store.tags = [makeTag(7, 'Release', 'auto', '{}', null), makeTag(100, 'Board 2 Existing', 'auto', '{}', null)];

    api.deleteTag.mockImplementationOnce(() => deleteRequest.promise);
    const pendingDelete = store.deleteTag(1, 7);

    store.activeBoardId = 2;
    deleteRequest.resolve(ok(undefined));
    const deleted = await pendingDelete;

    expect(deleted).toBe(true);
    expect(store.tags.map(x => x.id)).toEqual([7, 100]);
  });
});

function makeTag(
  id: number,
  name: string,
  styleName: 'auto' | 'presets' | 'solid' | 'gradient',
  stylePropertiesJson: string,
  emoji: string | null
) {
  return {
    id,
    name,
    styleName,
    stylePropertiesJson,
    emoji,
    createdAtUtc: '2026-05-16T00:00:00Z',
    updatedAtUtc: '2026-05-16T00:00:00Z'
  };
}

function createDeferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });

  return { promise, resolve, reject };
}
