import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useSlickEffectStore } from './slickEffectStore';

const key = 'boardoil:slick-effect';
let values: Map<string, string>;
let storageListener: (event: StorageEvent) => void;

beforeEach(() => {
  setActivePinia(createPinia());
  values = new Map();
  vi.stubGlobal('localStorage', {
    getItem: (name: string) => values.get(name) ?? null,
    setItem: (name: string, value: string) => values.set(name, value)
  });
  vi.stubGlobal('window', {
    addEventListener: vi.fn((_name: string, listener: typeof storageListener) => { storageListener = listener; }),
    removeEventListener: vi.fn()
  });
});
afterEach(() => vi.unstubAllGlobals());

describe('slick effect browser preference', () => {
  it.each([null, '', 'full', 'smooth', 'unknown'])('defaults to Bridges for stored value %s', value => {
    if (value !== null) values.set(key, value);
    const store = useSlickEffectStore();
    store.initialize();
    expect(store.effect).toBe('bridges');
  });

  it.each(['none', 'bridges', 'liquid'] as const)('restores and saves %s across store lifetimes', effect => {
    const store = useSlickEffectStore();
    store.initialize();
    store.setEffect(effect);
    expect(values.get(key)).toBe(effect);
    store.dispose();
    setActivePinia(createPinia());
    const restored = useSlickEffectStore();
    restored.initialize();
    expect(restored.effect).toBe(effect);
  });

  it('keeps the current session usable when browser storage throws', () => {
    vi.stubGlobal('localStorage', {
      getItem() { throw new Error('Storage unavailable'); },
      setItem() { throw new Error('Storage unavailable'); }
    });
    const store = useSlickEffectStore();
    store.initialize();
    expect(store.effect).toBe('bridges');
    store.setEffect('liquid');
    store.initialize();
    expect(store.effect).toBe('liquid');
  });

  it('applies another tab’s saves and clearing without writing them back', () => {
    const store = useSlickEffectStore();
    store.initialize();
    storageListener({ key, newValue: 'none', storageArea: localStorage } as StorageEvent);
    expect(store.effect).toBe('none');
    expect(values.has(key)).toBe(false);
    storageListener({ key: 'unrelated', newValue: 'liquid', storageArea: localStorage } as StorageEvent);
    expect(store.effect).toBe('none');
    storageListener({ key: null, newValue: null, storageArea: localStorage } as StorageEvent);
    expect(store.effect).toBe('bridges');
    store.dispose();
    expect(window.removeEventListener).toHaveBeenCalledWith('storage', storageListener);
  });

  it('retains session choices if storage becomes inaccessible after initialization', () => {
    const store = useSlickEffectStore();
    store.initialize();
    store.setEffect('none');
    const storage = localStorage;
    Object.defineProperty(globalThis, 'localStorage', { configurable: true, get() { throw new Error('Storage blocked'); } });
    expect(() => storageListener({ key, newValue: 'liquid', storageArea: storage } as StorageEvent)).not.toThrow();
    expect(store.effect).toBe('none');
    store.setEffect('liquid');
    expect(store.effect).toBe('liquid');
  });
});
