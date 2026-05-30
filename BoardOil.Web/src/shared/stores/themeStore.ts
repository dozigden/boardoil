import { computed, ref } from 'vue';
import { defineStore } from 'pinia';

const THEME_MODE_STORAGE_KEY = 'boardoil:theme-mode';
const SYSTEM_DARK_MEDIA_QUERY = '(prefers-color-scheme: dark)';

export type ThemeMode = 'system' | 'light' | 'dark';
export type ThemeValue = 'light' | 'dark';

export const useThemeStore = defineStore('theme', () => {
  const mode = ref<ThemeMode>('system');
  const activeTheme = ref<ThemeValue>('light');
  const initialized = ref(false);

  let systemThemeQuery: MediaQueryList | null = null;
  let removeSystemThemeListener: (() => void) | null = null;

  const isSystemMode = computed(() => mode.value === 'system');

  function initialize() {
    if (initialized.value) {
      return;
    }

    mode.value = readStoredMode();
    systemThemeQuery = resolveSystemThemeQuery();
    registerSystemThemeListener();
    applyTheme();
    initialized.value = true;
  }

  function setMode(nextMode: ThemeMode) {
    mode.value = nextMode;
    persistMode(nextMode);
    applyTheme();
  }

  function dispose() {
    removeSystemThemeListener?.();
    removeSystemThemeListener = null;
    systemThemeQuery = null;
    initialized.value = false;
  }

  function registerSystemThemeListener() {
    if (!systemThemeQuery) {
      return;
    }

    const handleSystemThemeChange = () => {
      if (mode.value !== 'system') {
        return;
      }

      applyTheme();
    };

    if (typeof systemThemeQuery.addEventListener === 'function') {
      systemThemeQuery.addEventListener('change', handleSystemThemeChange);
      removeSystemThemeListener = () => systemThemeQuery?.removeEventListener('change', handleSystemThemeChange);
      return;
    }

    if (typeof systemThemeQuery.addListener === 'function') {
      systemThemeQuery.addListener(handleSystemThemeChange);
      removeSystemThemeListener = () => systemThemeQuery?.removeListener?.(handleSystemThemeChange);
    }
  }

  function applyTheme() {
    const nextTheme = resolveTheme(mode.value, systemThemeQuery?.matches ?? false);
    activeTheme.value = nextTheme;

    const root = globalThis.document?.documentElement;
    if (!root) {
      return;
    }

    root.dataset.theme = nextTheme;
    root.dataset.themeMode = mode.value;
  }

  return {
    mode,
    activeTheme,
    initialized,
    isSystemMode,
    initialize,
    setMode,
    dispose
  };
});

function readStoredMode(): ThemeMode {
  try {
    const value = globalThis.localStorage?.getItem(THEME_MODE_STORAGE_KEY);
    if (value === 'light' || value === 'dark' || value === 'system') {
      return value;
    }
  } catch {
    // Ignore storage access failures and fall back to system mode.
  }

  return 'system';
}

function persistMode(mode: ThemeMode) {
  try {
    globalThis.localStorage?.setItem(THEME_MODE_STORAGE_KEY, mode);
  } catch {
    // Ignore storage access failures; theme still applies for current session.
  }
}

function resolveSystemThemeQuery(): MediaQueryList | null {
  if (typeof globalThis.matchMedia !== 'function') {
    return null;
  }

  return globalThis.matchMedia(SYSTEM_DARK_MEDIA_QUERY);
}

function resolveTheme(mode: ThemeMode, systemPrefersDark: boolean): ThemeValue {
  if (mode === 'dark') {
    return 'dark';
  }

  if (mode === 'light') {
    return 'light';
  }

  return systemPrefersDark ? 'dark' : 'light';
}
