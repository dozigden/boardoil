<template>
  <ModalDialog :open="open" title="Rename Board" close-label="Cancel rename" @close="emit('close')" @submit="submit">
    <label>
      Board name
      <input v-model="boardName" :disabled="busy" maxlength="120" required />
    </label>

    <template #actions>
      <div class="editor-actions card-modal-actions">
        <div class="card-modal-actions-left">
          <button type="submit" class="btn" :disabled="busy || !boardName.trim()" aria-label="Save board name" title="Save board name">
            <Check :size="16" aria-hidden="true" />
            <span>Save</span>
          </button>
          <button type="button" class="btn btn--secondary" :disabled="busy" aria-label="Cancel rename" title="Cancel" @click="emit('close')">
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
import ModalDialog from '../../shared/components/ModalDialog.vue';

const props = defineProps<{
  open: boolean;
  busy: boolean;
  initialName: string;
}>();

const emit = defineEmits<{
  close: [];
  submit: [payload: { name: string }];
}>();

const boardName = ref('');

watch(
  () => props.open,
  isOpen => {
    if (isOpen) {
      boardName.value = props.initialName;
    }
  }
);

function submit() {
  emit('submit', { name: boardName.value.trim() });
}
</script>
