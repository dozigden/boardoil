<template>
  <aside class="app-update-banner" aria-label="Application update">
    <TriangleAlert class="app-update-icon" :size="18" aria-hidden="true" />
    <span role="status" :title="refreshMessage || 'BoardOil has updated'">{{ refreshMessage || 'BoardOil has updated' }}</span>
    <button
      type="button"
      class="btn"
      @click="refresh"
    >
      <RefreshCw :size="14" aria-hidden="true" />
      Reload
    </button>
  </aside>
</template>

<script setup lang="ts">
import { RefreshCw, TriangleAlert } from '@lucide/vue';
import { storeToRefs } from 'pinia';
import { useAppUpdateStore } from '../../shared/stores/appUpdateStore';

const updateStore = useAppUpdateStore();
const { refreshMessage } = storeToRefs(updateStore);
const { refresh } = updateStore;
</script>

<style scoped>
.app-update-banner {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex: 0 0 auto;
  min-width: 0;
  max-width: 100%;
  box-sizing: border-box;
  padding: 0.3rem 0.4rem 0.3rem 0.65rem;
  border: 1px solid var(--bo-toast-warning-border);
  border-radius: 0.65rem;
  background: var(--bo-toast-warning-bg);
  color: var(--bo-toast-warning-text);
  font-size: 0.85rem;
}

.app-update-icon {
  flex: 0 0 auto;
}

.app-update-banner .btn {
  flex: 0 0 auto;
  font-size: 0.85rem;
}

.app-update-banner span {
  flex: 1 1 auto;
  min-width: 0;
  font-weight: 700;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
