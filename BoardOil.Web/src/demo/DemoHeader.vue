<template>
  <header class="demo-header">
    <div class="demo-brand">
      <BoardOilLogo class="demo-logo" />
      <span class="demo-wordmark">BoardOil</span>
      <span class="demo-board-name">{{ board?.name ?? 'Interactive preview' }}</span>
    </div>
    <div class="demo-actions">
      <span class="demo-local-label">Refresh to reset</span>
      <button
        type="button"
        class="btn btn--secondary"
        aria-label="View third-party licences"
        title="Licences"
        @click="licencesOpen = true"
      >
        <Scale :size="16" aria-hidden="true" />
        <span class="demo-action-label">Licences</span>
      </button>
      <button
        type="button"
        class="btn btn--secondary demo-theme-button"
        :aria-label="themeButtonLabel"
        :title="themeButtonLabel"
        @click="toggleDemoTheme"
      >
        <span aria-hidden="true">{{ activeDemoTheme === 'dark' ? '☀️' : '🌙' }}</span>
        <span class="demo-theme-label">{{ activeDemoTheme === 'dark' ? 'Light mode' : 'Dark mode' }}</span>
      </button>
      <button
        type="button"
        class="btn btn--secondary"
        aria-label="Reset preview"
        title="Reset preview"
        :disabled="isResetting"
        @click="resetPreview"
      >
        <RotateCcw :size="16" aria-hidden="true" />
        <span class="demo-action-label">Reset preview</span>
      </button>
      <a
        class="btn"
        href="https://boardoil.dozigden.com/installation/"
        target="_blank"
        rel="noopener noreferrer"
        aria-label="Get BoardOil"
        title="Get BoardOil"
      >
        <Download :size="16" aria-hidden="true" />
        <span class="demo-action-label">Get BoardOil</span>
      </a>
    </div>
    <DemoLicencesDialog :open="licencesOpen" @close="licencesOpen = false" />
  </header>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { Download, RotateCcw, Scale } from '@lucide/vue';
import { computed, ref } from 'vue';
import BoardOilLogo from '../site/components/BoardOilLogo.vue';
import { useBoardStore } from '../board/stores/boardStore';
import { resetDemoData } from './demoBoardApi';
import DemoLicencesDialog from './DemoLicencesDialog.vue';
import { demoResetVersion } from './demoReset';
import { activeDemoTheme, toggleDemoTheme } from './demoTheme';

const boardStore = useBoardStore();
const { board } = storeToRefs(boardStore);
const licencesOpen = ref(false);
const isResetting = ref(false);
const themeButtonLabel = computed(() => activeDemoTheme.value === 'dark' ? 'Use light mode' : 'Use dark mode');

async function resetPreview() {
  if (isResetting.value) {
    return;
  }

  isResetting.value = true;
  try {
    const boardId = board.value?.id ?? 1;
    resetDemoData();
    await boardStore.dispose();
    await boardStore.initialize(boardId);
    demoResetVersion.value += 1;
  } finally {
    isResetting.value = false;
  }
}
</script>

<style scoped>
.demo-header {
  min-height: 4.2rem;
  padding: 0.75rem 1.5rem;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  background: var(--bo-surface-panel-strong);
  border-bottom: 1px solid var(--bo-border-brand);
}

.demo-brand,
.demo-actions {
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

.demo-board-name {
  min-width: 0;
  max-width: 28rem;
  overflow: hidden;
  color: var(--bo-ink-muted);
  font-weight: 700;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.demo-local-label {
  color: var(--bo-ink-muted);
  font-size: 0.82rem;
  white-space: nowrap;
}

.demo-theme-button {
  gap: 0.35rem;
}

@media (max-width: 760px) {
  .demo-header {
    min-height: 3.5rem;
    padding: 0.5rem 0.75rem;
  }

  .demo-wordmark,
  .demo-local-label,
  .demo-theme-label {
    display: none;
  }

  .demo-board-name {
    max-width: 30vw;
  }

  .demo-actions {
    gap: 0.35rem;
  }

  .demo-actions .btn {
    padding-inline: 0.55rem;
  }
}

@media (max-width: 560px) {
  .demo-action-label {
    display: none;
  }

  .demo-actions .btn {
    min-width: 2.25rem;
    justify-content: center;
  }
}

@media (max-width: 400px) {
  .demo-board-name {
    display: none;
  }
}
</style>
