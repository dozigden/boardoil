import { readonly, ref } from 'vue';

const SystemDarkMediaQuery = '(prefers-color-scheme: dark)';

type DemoTheme = 'light' | 'dark';

const activeTheme = ref<DemoTheme>('light');
let systemThemeQuery: MediaQueryList | null = null;
let manualTheme: DemoTheme | null = null;

export const activeDemoTheme = readonly(activeTheme);

export function installDemoSystemTheme() {
  systemThemeQuery = globalThis.matchMedia?.(SystemDarkMediaQuery) ?? null;
  manualTheme = null;

  function applyTheme() {
    const root = globalThis.document?.documentElement;
    if (!root) {
      return;
    }

    const nextTheme = manualTheme ?? (systemThemeQuery?.matches ? 'dark' : 'light');
    activeTheme.value = nextTheme;
    root.dataset.theme = nextTheme;
    root.dataset.themeMode = 'system';
  }

  applyTheme();
  systemThemeQuery?.addEventListener('change', applyTheme);
}

export function toggleDemoTheme() {
  manualTheme = activeTheme.value === 'dark' ? 'light' : 'dark';
  activeTheme.value = manualTheme;

  const root = globalThis.document?.documentElement;
  if (!root) {
    return;
  }

  root.dataset.theme = manualTheme;
  root.dataset.themeMode = 'manual';
}
