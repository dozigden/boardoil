import { afterEach, describe, expect, it, vi } from 'vitest';
import { readBrowserStorageItem, writeBrowserStorageItem } from './browserStorage';

describe('browserStorage', () => {
  const originalLocalStorage = globalThis.localStorage;

  afterEach(() => {
    vi.unstubAllEnvs();
    vi.restoreAllMocks();
    Object.defineProperty(globalThis, 'localStorage', {
      configurable: true,
      value: originalLocalStorage
    });
  });

  it('does not access local storage when browser storage is disabled', () => {
    vi.stubEnv('VITE_BO_BROWSER_STORAGE_MODE', 'disabled');
    const getItem = vi.fn();
    const setItem = vi.fn();
    Object.defineProperty(globalThis, 'localStorage', {
      configurable: true,
      value: { getItem, setItem }
    });

    expect(readBrowserStorageItem('demo-key')).toBeNull();
    writeBrowserStorageItem('demo-key', 'demo-value');

    expect(getItem).not.toHaveBeenCalled();
    expect(setItem).not.toHaveBeenCalled();
  });
});
