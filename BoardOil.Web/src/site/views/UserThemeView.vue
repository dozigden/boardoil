<template>
  <section class="user-theme-view">
    <header class="user-theme-header">
      <h2>Theme</h2>
      <p>Choose how BoardOil should look for your account on this browser.</p>
    </header>

    <section class="user-theme-card panel panel-stack panel-stack--cozy">
      <fieldset class="user-theme-options">
        <legend class="user-theme-legend">Appearance mode</legend>
        <label class="user-theme-option">
          <input v-model="draftThemeMode" type="radio" name="theme-mode" value="system" />
          <span>System</span>
        </label>
        <label class="user-theme-option">
          <input v-model="draftThemeMode" type="radio" name="theme-mode" value="light" />
          <span>Light</span>
        </label>
        <label class="user-theme-option">
          <input v-model="draftThemeMode" type="radio" name="theme-mode" value="dark" />
          <span>Dark</span>
        </label>
      </fieldset>

      <p class="user-theme-active" aria-live="polite">
        Active theme: <strong>{{ activeThemeLabel }}</strong>
      </p>

      <div class="user-theme-actions">
        <button type="button" class="btn" :disabled="!isDirty" @click="saveThemeMode">Save</button>
      </div>
      <p v-if="saveMessage" class="user-theme-save-message" role="status">{{ saveMessage }}</p>
    </section>
  </section>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed, ref, watch } from 'vue';
import { useThemeStore, type ThemeMode } from '../../shared/stores/themeStore';

const themeStore = useThemeStore();
const { mode: themeMode, activeTheme } = storeToRefs(themeStore);
const draftThemeMode = ref<ThemeMode>(themeMode.value);
const saveMessage = ref<string | null>(null);
const activeThemeLabel = computed(() => activeTheme.value === 'dark' ? 'Dark' : 'Light');
const isDirty = computed(() => draftThemeMode.value !== themeMode.value);

watch(
  themeMode,
  nextMode => {
    draftThemeMode.value = nextMode;
  },
  { immediate: true }
);

watch(
  draftThemeMode,
  () => {
    saveMessage.value = null;
  }
);

function saveThemeMode() {
  if (!isDirty.value) {
    return;
  }

  themeStore.setMode(draftThemeMode.value);
  saveMessage.value = 'Theme preference saved.';
}
</script>

<style scoped>
.user-theme-view {
  display: grid;
  gap: 1rem;
}

.user-theme-header h2 {
  margin: 0;
}

.user-theme-header p {
  margin: 0.3rem 0 0;
  color: var(--bo-ink-muted);
}

.user-theme-card {
  max-width: 32rem;
}

.user-theme-options {
  margin: 0;
  border: 0;
  padding: 0;
  display: grid;
  gap: 0.4rem;
}

.user-theme-legend {
  padding: 0;
  font-size: 0.9rem;
  color: var(--bo-ink-muted);
  margin-bottom: 0.1rem;
}

.user-theme-option {
  display: inline-flex;
  align-items: center;
  gap: 0.45rem;
  color: var(--bo-ink-default);
}

.user-theme-option input[type='radio'] {
  appearance: none;
  margin: 0;
  inline-size: 1rem;
  block-size: 1rem;
  border-radius: 999px;
  border: 1px solid var(--bo-border-default);
  background: var(--bo-surface-base);
  display: grid;
  place-content: center;
}

.user-theme-option input[type='radio']::before {
  content: '';
  inline-size: 0.5rem;
  block-size: 0.5rem;
  border-radius: 999px;
  transform: scale(0);
  transition: transform 120ms ease-in-out;
  background: var(--bo-colour-brand-strong);
}

.user-theme-option input[type='radio']:checked {
  border-color: var(--bo-colour-brand-strong);
}

.user-theme-option input[type='radio']:checked::before {
  transform: scale(1);
}

.user-theme-option input[type='radio']:focus-visible {
  outline: none;
  border-color: var(--bo-focus-ring);
  box-shadow: 0 0 0 2px var(--bo-focus-ring-shadow);
}

.user-theme-active {
  margin: 0;
  color: var(--bo-ink-default);
}

.user-theme-actions {
  display: flex;
  justify-content: flex-start;
}

.user-theme-save-message {
  margin: 0;
  color: var(--bo-colour-success-ink);
}
</style>
