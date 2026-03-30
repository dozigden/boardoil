<template>
  <ModalDialog :open="open" title="Create Board" close-label="Cancel creation" @close="emit('close')" @submit="submit">
    <label>
      Board name
      <input v-model="boardName" :disabled="busy" maxlength="120" required />
    </label>

    <template #actions>
      <div class="editor-actions card-modal-actions">
        <div class="card-modal-actions-left">
          <button type="submit" class="btn" :disabled="busy || !boardName.trim()" aria-label="Create board" title="Create board">
            <Check :size="16" aria-hidden="true" />
            <span>Create board</span>
          </button>
          <button type="button" class="btn btn--ghost" :disabled="busy" aria-label="Cancel creation" title="Cancel" @click="emit('close')">
            <X :size="16" aria-hidden="true" />
            <span>Cancel</span>
          </button>
        </div>
      </div>
    </template>
  </ModalDialog>
</template>

<script setup lang="ts">
import { Check, X } from 'lucide-vue-next';
import { ref, watch } from 'vue';
import ModalDialog from './ModalDialog.vue';

const props = defineProps<{
  open: boolean;
  busy: boolean;
}>();

const emit = defineEmits<{
  close: [];
  submit: [payload: { name: string }];
}>();

const boardName = ref('');

function resetDraft() {
  boardName.value = '';
}

function submit() {
  emit('submit', { name: boardName.value.trim() });
}

watch(
  () => props.open,
  isOpen => {
    if (isOpen) {
      resetDraft();
    }
  }
);
</script>
