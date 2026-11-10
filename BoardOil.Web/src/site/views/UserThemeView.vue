<template>
  <FormPage :definition="form" class="user-theme-view"
    discard-message="You have unsaved theme changes. Discard them and leave?" @saved="themeSaved">
    <template #before-fields>
      <p class="user-theme-description">Choose how BoardOil should look for your account on this browser.</p>
    </template>
    <template #after-fields>
      <p class="user-theme-active" aria-live="polite">
        Active theme: <strong>{{ activeThemeLabel }}</strong>
      </p>
    </template>
  </FormPage>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import FormPage from '../../shared/forms/FormPage.vue';
import { useThemeStore } from '../../shared/stores/themeStore';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import { ok } from '../../shared/types/result';
import { createUserThemeForm } from '../forms/userThemeForm';

const themeStore = useThemeStore();
const feedback = useUiFeedbackStore();
const activeThemeLabel = computed(() => themeStore.activeTheme === 'dark' ? 'Dark' : 'Light');
const form = createUserThemeForm(themeStore.mode, async ({ mode }) => {
  themeStore.setMode(mode);
  return ok({ mode: themeStore.mode });
});

function themeSaved() {
  feedback.showToast('Saved successfully.');
}
</script>

<style scoped>
.user-theme-view :deep(.panel) { max-width: 32rem; }
.user-theme-description { margin: 0; color: var(--bo-ink-muted); }
.user-theme-active { margin: 0; color: var(--bo-ink-default); }
</style>
