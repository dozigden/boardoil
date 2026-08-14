import { readonly, ref } from 'vue';

type DemoTheme = 'light' | 'dark';

const activeTheme = ref<DemoTheme>('light');

export const activeDemoTheme = readonly(activeTheme);

export function installDemoTheme() {
  applyTheme('light');
}

export function toggleDemoTheme() {
  const nextTheme = activeTheme.value === 'dark' ? 'light' : 'dark';
  applyTheme(nextTheme);
}

function applyTheme(theme: DemoTheme) {
  activeTheme.value = theme;

  const root = globalThis.document?.documentElement;
  if (!root) {
    return;
  }

  root.dataset.theme = theme;
  root.dataset.themeMode = 'manual';
}
