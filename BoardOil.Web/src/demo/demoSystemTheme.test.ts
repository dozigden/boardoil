import { afterEach, describe, expect, it, vi } from 'vitest';
import { activeDemoTheme, installDemoSystemTheme, toggleDemoTheme } from './demoSystemTheme';

describe('installDemoSystemTheme', () => {
  const originalMatchMedia = globalThis.matchMedia;
  const originalDocument = globalThis.document;
  const originalLocalStorage = globalThis.localStorage;

  afterEach(() => {
    vi.restoreAllMocks();
    Object.defineProperty(globalThis, 'matchMedia', {
      configurable: true,
      value: originalMatchMedia
    });
    Object.defineProperty(globalThis, 'document', {
      configurable: true,
      value: originalDocument
    });
    Object.defineProperty(globalThis, 'localStorage', {
      configurable: true,
      value: originalLocalStorage
    });
  });

  it('follows the browser colour scheme and reacts to changes', () => {
    const root = { dataset: {} as Record<string, string> };
    let isDark = true;
    let changeHandler: () => void = () => undefined;
    Object.defineProperty(globalThis, 'document', {
      configurable: true,
      value: { documentElement: root }
    });
    Object.defineProperty(globalThis, 'matchMedia', {
      configurable: true,
      value: vi.fn(() => ({
        get matches() {
          return isDark;
        },
        addEventListener: (_event: string, handler: () => void) => {
          changeHandler = handler;
        }
      }))
    });

    installDemoSystemTheme();
    expect(root.dataset).toEqual({ theme: 'dark', themeMode: 'system' });

    isDark = false;
    changeHandler();
    expect(root.dataset).toEqual({ theme: 'light', themeMode: 'system' });
  });

  it('allows an in-memory override without browser storage', () => {
    const root = { dataset: {} as Record<string, string> };
    const storageSet = vi.fn();
    Object.defineProperty(globalThis, 'document', {
      configurable: true,
      value: { documentElement: root }
    });
    Object.defineProperty(globalThis, 'matchMedia', {
      configurable: true,
      value: vi.fn(() => ({
        matches: false,
        addEventListener: vi.fn()
      }))
    });
    Object.defineProperty(globalThis, 'localStorage', {
      configurable: true,
      value: { setItem: storageSet }
    });

    installDemoSystemTheme();
    toggleDemoTheme();

    expect(activeDemoTheme.value).toBe('dark');
    expect(root.dataset).toEqual({ theme: 'dark', themeMode: 'manual' });
    expect(storageSet).not.toHaveBeenCalled();
  });
});
