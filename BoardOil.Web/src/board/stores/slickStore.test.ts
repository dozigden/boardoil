import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useSlickStore } from './slickStore';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import { err, ok } from '../../shared/types/result';

const api = {
  getSlicks: vi.fn(),
  getSlickCreateDefaultStyle: vi.fn(),
  createSlick: vi.fn(),
  updateSlick: vi.fn(),
  deleteSlick: vi.fn()
};

vi.mock('../../shared/api/boardApi', () => ({
  createBoardApi: () => api
}));

describe('slickStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    api.getSlicks.mockResolvedValue(ok([]));
    api.getSlickCreateDefaultStyle.mockResolvedValue(ok({
      styleName: 'presets',
      stylePropertiesJson: '{"presetIndex":5,"textColorMode":"auto"}'
    }));
    api.createSlick.mockResolvedValue(ok(makeSlick(7, 'Release Train', 'presets', '{"presetIndex":2}')));
    api.updateSlick.mockResolvedValue(ok(makeSlick(7, 'Release Train', 'solid', '{"backgroundColor":"#336699","textColorMode":"auto","borderMode":"auto"}')));
    api.deleteSlick.mockResolvedValue(ok(undefined));
  });

  it('loads slicks for the selected board', async () => {
    const store = useSlickStore();
    api.getSlicks.mockResolvedValueOnce(ok([makeSlick(7, 'Release Train', 'presets', '{"presetIndex":2}')]));

    const loaded = await store.loadSlicks(3);

    expect(loaded).toBe(true);
    expect(api.getSlicks).toHaveBeenCalledWith(3);
    expect(store.slicks.map(x => x.name)).toEqual(['Release Train']);
  });

  it('ignores stale loadSlicks responses when board changes mid-load', async () => {
    const store = useSlickStore();
    const firstRequest = createDeferred<{ ok: true; data: ReturnType<typeof makeSlick>[] }>();
    const secondRequest = createDeferred<{ ok: true; data: ReturnType<typeof makeSlick>[] }>();

    api.getSlicks
      .mockImplementationOnce(() => firstRequest.promise)
      .mockImplementationOnce(() => secondRequest.promise);

    const firstLoad = store.loadSlicks(1);
    const secondLoad = store.loadSlicks(2);

    secondRequest.resolve({ ok: true, data: [makeSlick(20, 'Second Board Slick', 'presets', '{"presetIndex":1}')] });
    await secondLoad;

    firstRequest.resolve({ ok: true, data: [makeSlick(10, 'First Board Slick', 'presets', '{"presetIndex":2}')] });
    await firstLoad;

    expect(store.activeBoardId).toBe(2);
    expect(store.slicks.map(x => x.name)).toEqual(['Second Board Slick']);
  });

  it('creates and caches slick', async () => {
    const store = useSlickStore();
    store.activeBoardId = 3;
    const created = await store.createSlick({
      name: 'Release Train',
      styleName: 'presets',
      stylePropertiesJson: '{"presetIndex":2}'
    }, 3);

    expect(created?.id).toBe(7);
    expect(api.createSlick).toHaveBeenCalledWith(3, {
      name: 'Release Train',
      styleName: 'presets',
      stylePropertiesJson: '{"presetIndex":2}'
    });
    expect(store.slicks.map(x => x.name)).toEqual(['Release Train']);
  });

  it('loads create default style', async () => {
    const store = useSlickStore();

    const defaultStyle = await store.getCreateDefaultStyle(3);

    expect(defaultStyle?.stylePropertiesJson).toBe('{"presetIndex":5,"textColorMode":"auto"}');
    expect(api.getSlickCreateDefaultStyle).toHaveBeenCalledWith(3);
  });

  it('updates slick in cache', async () => {
    const store = useSlickStore();
    store.slicks = [makeSlick(7, 'Release Train', 'presets', '{"presetIndex":2}')];
    store.activeBoardId = 3;
    const updated = await store.updateSlick(7, {
      name: 'Release Train',
      styleName: 'solid',
      stylePropertiesJson: '{"backgroundColor":"#336699","textColorMode":"auto","borderMode":"auto"}'
    }, 3);

    expect(updated?.styleName).toBe('solid');
    expect(store.slicks[0]?.styleName).toBe('solid');
  });

  it('deletes slick from cache', async () => {
    const store = useSlickStore();
    store.slicks = [makeSlick(7, 'Release Train', 'presets', '{"presetIndex":2}')];
    store.activeBoardId = 3;
    const deleted = await store.deleteSlick(7, 3);

    expect(deleted).toBe(true);
    expect(api.deleteSlick).toHaveBeenCalledWith(3, 7);
    expect(store.slicks).toHaveLength(0);
  });

  it('reports errors from API operations', async () => {
    const store = useSlickStore();
    const feedback = useUiFeedbackStore();
    api.getSlicks.mockResolvedValueOnce(err({ kind: 'api', message: 'Could not load slicks.' }));

    const loaded = await store.loadSlicks(3);

    expect(loaded).toBe(false);
    expect(feedback.errorMessage).toBe('Could not load slicks.');
  });
});

function makeSlick(id: number, name: string, styleName: 'solid' | 'presets', stylePropertiesJson: string) {
  return {
    id,
    name,
    styleName,
    stylePropertiesJson,
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
