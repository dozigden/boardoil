import { afterEach, describe, expect, it, vi } from 'vitest';
import { activeDemoTheme, installDemoTheme, toggleDemoTheme } from './demoTheme';

describe('demoTheme', () => {
  const originalDocument = globalThis.document;
  const originalLocalStorage = globalThis.localStorage;

  afterEach(() => {
    vi.restoreAllMocks();
    Object.defineProperty(globalThis, 'document', {
      configurable: true,
      value: originalDocument
    });
    Object.defineProperty(globalThis, 'localStorage', {
      configurable: true,
      value: originalLocalStorage
    });
  });

  it('starts in manual light mode', () => {
    const root = { dataset: {} as Record<string, string> };
    Object.defineProperty(globalThis, 'document', {
      configurable: true,
      value: { documentElement: root }
    });

    installDemoTheme();

    expect(activeDemoTheme.value).toBe('light');
    expect(root.dataset).toEqual({ theme: 'light', themeMode: 'manual' });
  });

  it('toggles the manual theme without browser storage', () => {
    const root = { dataset: {} as Record<string, string> };
    const storageSet = vi.fn();
    Object.defineProperty(globalThis, 'document', {
      configurable: true,
      value: { documentElement: root }
    });
    Object.defineProperty(globalThis, 'localStorage', {
      configurable: true,
      value: { setItem: storageSet }
    });

    installDemoTheme();
    toggleDemoTheme();

    expect(activeDemoTheme.value).toBe('dark');
    expect(root.dataset).toEqual({ theme: 'dark', themeMode: 'manual' });
    expect(storageSet).not.toHaveBeenCalled();
  });
});
