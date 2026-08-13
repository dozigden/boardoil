const BrowserStorageDisabledMode = 'disabled';

export function isBrowserStorageEnabled() {
  return import.meta.env.VITE_BO_BROWSER_STORAGE_MODE !== BrowserStorageDisabledMode;
}

export function readBrowserStorageItem(key: string) {
  if (!isBrowserStorageEnabled()) {
    return null;
  }

  try {
    return globalThis.localStorage?.getItem(key) ?? null;
  } catch {
    return null;
  }
}

export function writeBrowserStorageItem(key: string, value: string) {
  if (!isBrowserStorageEnabled()) {
    return;
  }

  try {
    globalThis.localStorage?.setItem(key, value);
  } catch {
    // Ignore storage access failures; callers retain their in-memory state.
  }
}
