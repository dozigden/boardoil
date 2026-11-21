<template>
  <header class="demo-header" role="banner">
    <div class="demo-brand">
      <BoardOilLogo class="demo-logo" />
      <span class="demo-wordmark">BoardOil</span>
    </div>
    <div class="demo-preference">
      <span class="demo-preference-label" aria-hidden="true">Theme</span>
      <div class="btn-tab-list" role="group" aria-label="Theme">
        <button
          v-for="option in themeOptions"
          :key="option.value"
          type="button"
          class="btn btn--tab"
          :class="{ 'is-active': activeDemoTheme === option.value }"
          :aria-pressed="activeDemoTheme === option.value"
          @click="setDemoTheme(option.value)"
        >{{ option.label }}</button>
      </div>
    </div>
    <div class="demo-preference demo-slick-effects">
      <span class="demo-preference-label" aria-hidden="true">Slick effect</span>
      <SlickEffectPicker />
    </div>
    <a
      class="btn demo-install-link"
      href="https://boardoil.dozigden.com/installation/"
      target="_blank"
      rel="noopener noreferrer"
      aria-label="Get BoardOil"
      title="Get BoardOil"
    >
      <Download :size="16" aria-hidden="true" />
      <span class="demo-action-label">Get BoardOil</span>
    </a>
  </header>
</template>

<script setup lang="ts">
import { Download } from '@lucide/vue';
import BoardOilLogo from '../site/components/BoardOilLogo.vue';
import SlickEffectPicker from '../site/components/SlickEffectPicker.vue';
import { activeDemoTheme, setDemoTheme } from './demoTheme';

const themeOptions = [
  { value: 'light', label: 'Light' },
  { value: 'dark', label: 'Dark' }
] as const;
</script>

<style scoped>
.demo-header {
  min-height: 4.2rem;
  padding: 0.75rem 1.5rem;
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto auto auto;
  align-items: center;
  gap: 1rem;
  background: var(--bo-surface-panel-strong);
  border-bottom: 1px solid var(--bo-border-brand);
}

.demo-brand {
  display: flex;
  align-items: center;
  gap: 0.65rem;
  min-width: 0;
}

.demo-logo {
  width: 2.25rem;
  height: 2.25rem;
  flex: 0 0 auto;
}

.demo-wordmark {
  color: var(--bo-link);
  font-size: 1.45rem;
  font-weight: 800;
}

.demo-preference {
  display: flex;
  align-items: center;
  gap: 0.65rem;
}

.demo-slick-effects {
  padding-inline-start: 1rem;
  border-inline-start: 1px solid var(--bo-border-brand);
}

.demo-preference-label {
  color: var(--bo-ink-default);
  font-size: 0.85rem;
  font-weight: 700;
  white-space: nowrap;
}

.demo-preference :deep(.btn--tab) {
  --bo-btn-ink: var(--bo-ink-default);
  --bo-btn-ink-hover: var(--bo-ink-default);
}

.demo-preference :deep(.btn--tab.is-active) {
  --bo-btn-ink: var(--bo-mix-dark);
  --bo-btn-ink-hover: var(--bo-mix-dark);
}

@media (max-width: 960px) {
  .demo-header {
    grid-template-columns: minmax(0, 1fr) auto auto;
    row-gap: 0.55rem;
  }

  .demo-slick-effects {
    grid-column: 1 / -1;
    grid-row: 2;
    justify-content: center;
    padding: 0.55rem 0 0;
    border-inline-start: 0;
    border-block-start: 1px solid var(--bo-border-soft);
  }

  .demo-install-link {
    grid-column: 3;
    grid-row: 1;
  }
}

@media (max-width: 760px) {
  .demo-header {
    min-height: 3.5rem;
    padding: 0.5rem 0.75rem;
    column-gap: 0.35rem;
  }

  .demo-wordmark {
    display: none;
  }

  .demo-install-link {
    padding-inline: 0.55rem;
  }
}

@media (max-width: 560px) {
  .demo-action-label {
    display: none;
  }

  .demo-install-link {
    min-width: 2.25rem;
    justify-content: center;
  }
}
</style>
