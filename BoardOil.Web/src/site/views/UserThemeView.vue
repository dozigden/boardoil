<template>
  <FormPage ref="formPage" :definition="form" class="user-theme-view"
    discard-message="You have unsaved theme changes. Discard them and leave?" @saved="themeSaved">
    <template #fields="{ values, busy, setValue }">
      <div class="user-theme-content">
        <aside class="user-theme-info">
          <Info :size="20" aria-hidden="true" />
          <p>These settings are per browser, and are kept in local storage.</p>
        </aside>
        <div class="user-theme-cards">
          <section v-for="choice in themeChoices" :key="choice.name" class="panel panel-stack panel-stack--cozy user-theme-card"
            :aria-labelledby="`theme-${choice.name}-heading`">
            <h3 :id="`theme-${choice.name}-heading`">{{ choice.label }}</h3>
            <div class="btn-tab-list user-theme-choices" role="group" :aria-label="choice.label">
              <button v-for="option in choice.options" :key="option.value" type="button" class="btn btn--tab"
                :class="{ 'is-active': values[choice.name] === option.value }"
                :aria-pressed="values[choice.name] === option.value" :disabled="busy"
                @click="setValue(choice.name, option.value)">{{ option.label }}</button>
            </div>
          </section>
        </div>
        <section class="panel user-theme-preview">
          <SlickEffectPreview :mode="values.slickEffect" :appearance="values.mode" />
        </section>
      </div>
    </template>
  </FormPage>
</template>

<script setup lang="ts">
import { Info } from '@lucide/vue';
import { useTemplateRef, watch } from 'vue';
import FormPage from '../../shared/forms/FormPage.vue';
import SlickEffectPreview from '../components/SlickEffectPreview.vue';
import { useThemeStore } from '../../shared/stores/themeStore';
import { useSlickEffectStore } from '../../shared/stores/slickEffectStore';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import { ok } from '../../shared/types/result';
import { createUserThemeForm, themeChoices, type ThemeDraft } from '../forms/userThemeForm';

const themeStore = useThemeStore();
const slickEffectStore = useSlickEffectStore();
const feedback = useUiFeedbackStore();
const formPage = useTemplateRef<{ updateSavedValues: (values: ThemeDraft) => void }>('formPage');
const form = createUserThemeForm(themeStore.mode, slickEffectStore.effect, async ({ mode, slickEffect }) => {
  themeStore.setMode(mode);
  slickEffectStore.setEffect(slickEffect);
  return ok({ mode: themeStore.mode, slickEffect: slickEffectStore.effect });
});

function themeSaved() {
  feedback.showToast('Saved successfully.');
}

watch(() => [themeStore.mode, slickEffectStore.effect], () => {
  formPage.value?.updateSavedValues({ mode: themeStore.mode, slickEffect: slickEffectStore.effect });
});
</script>

<style scoped>
.user-theme-view { width: min(100%, 40rem); min-width: 0; }
.user-theme-content { display: grid; gap: 1rem; min-width: 0; }
.user-theme-cards { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 16rem), 1fr)); gap: 1rem; }
.user-theme-card, .user-theme-preview { min-width: 0; }
.user-theme-card h3 { margin: 0; font-size: 1.1rem; font-weight: 750; }
.user-theme-choices { width: fit-content; max-width: 100%; align-self: flex-start; }
.user-theme-info {
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;
  padding: 0.65rem 0.85rem;
  border: 1px solid var(--bo-colour-info);
  border-radius: 0.75rem;
  background: color-mix(in srgb, var(--bo-colour-info) 15%, var(--bo-surface-base));
  color: var(--bo-ink-default);
}
.user-theme-info svg { flex-shrink: 0; margin-top: 0.1rem; }
.user-theme-info p { margin: 0; }
</style>
