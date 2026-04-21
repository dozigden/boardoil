<template>
  <ModalDialog
    :open="open"
    title="Archive Selected Cards"
    close-label="Close archive confirmation"
    @close="emit('close')"
  >
    <p class="archive-confirm-summary">
      Archive {{ selectedCount }} selected card{{ selectedCount === 1 ? '' : 's' }}?
    </p>
    <p v-if="selectedCount === 0" class="archive-confirm-empty">
      No cards selected.
    </p>
    <ul v-else class="archive-confirm-list">
      <li v-for="card in selectedCards" :key="card.id" class="archive-confirm-list-item">
        {{ card.title }}
      </li>
    </ul>
    <section class="card-modal-actions">
      <div class="card-modal-actions-left">
        <button type="button" class="btn btn--secondary" :disabled="isArchiving" @click="emit('close')">
          Cancel
        </button>
      </div>
      <button
        type="button"
        class="btn btn--danger"
        :disabled="isArchiving || selectedCount === 0"
        @click="emit('confirm')"
      >
        {{ isArchiving ? 'Archiving...' : 'Archive selected' }}
      </button>
    </section>
  </ModalDialog>
</template>

<script setup lang="ts">
import type { Card as BoardCard } from '../types/boardTypes';
import ModalDialog from './ModalDialog.vue';

defineProps<{
  open: boolean;
  selectedCards: BoardCard[];
  selectedCount: number;
  isArchiving: boolean;
}>();

const emit = defineEmits<{
  close: [];
  confirm: [];
}>();
</script>

<style scoped>
.archive-confirm-summary {
  margin: 0 0 0.6rem;
  color: var(--bo-ink);
}

.archive-confirm-empty {
  margin: 0 0 0.6rem;
  color: var(--bo-ink-muted);
}

.archive-confirm-list {
  margin: 0 0 0.9rem;
  padding-left: 1.25rem;
  max-height: 14rem;
  overflow-y: auto;
}

.archive-confirm-list-item {
  margin: 0.15rem 0;
  color: var(--bo-ink);
}
</style>
