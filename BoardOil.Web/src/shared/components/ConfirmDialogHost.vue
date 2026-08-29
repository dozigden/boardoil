<template>
  <FixedChromeDialog
    :open="state.open"
    :title="state.title"
    close-label="Cancel confirmation"
    @close="cancel"
    @submit="accept"
  >
    <p class="confirm-dialog-message">{{ state.message }}</p>
    <template #actions>
      <section class="fixed-chrome-dialog-actions fixed-chrome-dialog-actions--end">
        <button type="button" class="btn btn--secondary" @click="cancel">{{ state.cancelLabel }}</button>
        <button type="submit" class="btn" :class="{ 'btn--danger': state.danger }">{{ state.confirmLabel }}</button>
      </section>
    </template>
  </FixedChromeDialog>
</template>

<script setup lang="ts">
import FixedChromeDialog from './FixedChromeDialog.vue';
import { useConfirmDialogState } from '../composables/useConfirm';

const { state, accept, cancel } = useConfirmDialogState();
</script>

<style scoped>
.confirm-dialog-message {
  margin: 0 0 0.8rem;
  white-space: pre-line;
}

</style>
