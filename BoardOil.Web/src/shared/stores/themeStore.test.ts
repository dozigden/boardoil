import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useThemeStore } from './themeStore';

type MatchMediaMock = {
  matches: boolean;
  listener: ((event: MediaQueryListEvent) => void) | null;
  addEventListener: ReturnType<typeof vi.fn>;
  removeEventListener: ReturnType<typeof vi.fn>;
};

describe('themeStore', () => {
  let originalMatchMedia: typeof globalThis.matchMedia | undefined;
  let originalDocument: Document | undefined;
  let originalLocalStorage: Storage | undefined;
  let matchMediaMock: MatchMediaMock;
  let localStorageMock: ReturnType<typeof createLocalStorageMock>;

  beforeEach(() => {
    setActivePinia(createPinia());
    localStorageMock = createLocalStorageMock();
    originalLocalStorage = globalThis.localStorage;
    Object.defineProperty(globalThis, 'localStorage', {
      configurable: true,
      writable: true,
      value: localStorageMock
    });

    originalDocument = globalThis.document;
    Object.defineProperty(globalThis, 'document', {
      configurable: true,
      writable: true,
      value: makeDocumentMock()
    });

    matchMediaMock = makeMatchMediaMock(false);
    originalMatchMedia = globalThis.matchMedia;
    Object.defineProperty(globalThis, 'matchMedia', {
      configurable: true,
      writable: true,
      value: vi.fn().mockImplementation(() => {
        return {
          get matches() {
            return matchMediaMock.matches;
          },
          addEventListener: matchMediaMock.addEventListener,
          removeEventListener: matchMediaMock.removeEventListener
        };
      })
    });
  });

  it('initializes using system mode and applies light theme by default', () => {
    const store = useThemeStore();

    store.initialize();

    expect(store.mode).toBe('system');
    expect(store.activeTheme).toBe('light');
    expect(document.documentElement.dataset.theme).toBe('light');
    expect(document.documentElement.dataset.themeMode).toBe('system');
  });

  it('loads and applies stored dark mode preference', () => {
    globalThis.localStorage.setItem('boardoil:theme-mode', 'dark');
    const store = useThemeStore();

    store.initialize();

    expect(store.mode).toBe('dark');
    expect(store.activeTheme).toBe('dark');
    expect(document.documentElement.dataset.theme).toBe('dark');
  });

  it('persists mode changes and reacts to system theme updates in system mode', () => {
    const store = useThemeStore();
    store.initialize();

    store.setMode('dark');
    expect(globalThis.localStorage.getItem('boardoil:theme-mode')).toBe('dark');
    expect(store.activeTheme).toBe('dark');

    store.setMode('system');
    expect(globalThis.localStorage.getItem('boardoil:theme-mode')).toBe('system');

    matchMediaMock.matches = true;
    const listener = matchMediaMock.listener;
    expect(listener).not.toBeNull();
    listener?.({ matches: true } as MediaQueryListEvent);
    expect(store.activeTheme).toBe('dark');
    expect(document.documentElement.dataset.theme).toBe('dark');
  });

  it('removes the media query listener on dispose', () => {
    const store = useThemeStore();
    store.initialize();

    store.dispose();

    expect(matchMediaMock.removeEventListener).toHaveBeenCalledTimes(1);
  });

  afterEach(() => {
    Object.defineProperty(globalThis, 'matchMedia', {
      configurable: true,
      writable: true,
      value: originalMatchMedia
    });
    Object.defineProperty(globalThis, 'document', {
      configurable: true,
      writable: true,
      value: originalDocument
    });
    Object.defineProperty(globalThis, 'localStorage', {
      configurable: true,
      writable: true,
      value: originalLocalStorage
    });
  });
});

function makeMatchMediaMock(matches: boolean): MatchMediaMock {
  const state: MatchMediaMock = {
    matches,
    listener: null,
    addEventListener: vi.fn((eventName: string, listener: (event: MediaQueryListEvent) => void) => {
      if (eventName === 'change') {
        state.listener = listener;
      }
    }),
    removeEventListener: vi.fn((eventName: string, listener: (event: MediaQueryListEvent) => void) => {
      if (eventName === 'change' && state.listener === listener) {
        state.listener = null;
      }
    })
  };

  return state;
}

function createLocalStorageMock() {
  const values = new Map<string, string>();
  return {
    getItem(key: string) {
      return values.get(key) ?? null;
    },
    setItem(key: string, value: string) {
      values.set(key, value);
    },
    removeItem(key: string) {
      values.delete(key);
    },
    clear() {
      values.clear();
    }
  };
}

function makeDocumentMock() {
  const dataset: Record<string, string> = {};
  return {
    documentElement: {
      dataset,
      removeAttribute(name: string) {
        if (name === 'data-theme') {
          delete dataset.theme;
          return;
        }

        if (name === 'data-theme-mode') {
          delete dataset.themeMode;
        }
      }
    }
  };
}
