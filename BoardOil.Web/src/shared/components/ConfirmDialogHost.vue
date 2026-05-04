<template>
  <ModalDialog
    :open="state.open"
    :title="state.title"
    close-label="Cancel confirmation"
    @close="cancel"
    @submit="accept"
  >
    <p class="confirm-dialog-message">{{ state.message }}</p>
    <template #actions>
      <section class="card-modal-actions confirm-dialog-actions">
        <button type="button" class="btn btn--secondary" @click="cancel">{{ state.cancelLabel }}</button>
        <button type="submit" class="btn" :class="{ 'btn--danger': state.danger }">{{ state.confirmLabel }}</button>
      </section>
    </template>
  </ModalDialog>
</template>

<script setup lang="ts">
import ModalDialog from './ModalDialog.vue';
import { useConfirmDialogState } from '../composables/useConfirm';

const { state, accept, cancel } = useConfirmDialogState();
</script>

<style scoped>
.confirm-dialog-message {
  margin: 0 0 0.8rem;
  white-space: pre-line;
}

.confirm-dialog-actions {
  justify-content: flex-end;
  gap: 0.5rem;
}
</style>
