<template>
  <ModalDialog :open="open" title="Create Board" close-label="Cancel creation" @close="emit('close')" @submit="submit">
    <div class="board-create-dialog-modes" role="tablist" aria-label="Board create mode">
      <button
        type="button"
        class="btn btn--tab"
        :class="{ 'is-active': mode === 'blank' }"
        role="tab"
        :aria-selected="mode === 'blank'"
        :disabled="busy"
        @click="mode = 'blank'"
      >
        Create Blank
      </button>
      <button
        type="button"
        class="btn btn--tab"
        :class="{ 'is-active': mode === 'tasksmd' }"
        role="tab"
        :aria-selected="mode === 'tasksmd'"
        :disabled="busy"
        @click="mode = 'tasksmd'"
      >
        Import tasksmd
      </button>
    </div>

    <label v-if="mode === 'blank'">
      Board name
      <input v-model="boardName" :disabled="busy" maxlength="120" required />
    </label>
    <label v-else>
      tasksmd URL
      <input v-model="tasksMdUrl" :disabled="busy" maxlength="2000" required type="url" placeholder="https://tasks.example.net/" />
    </label>

    <template #actions>
      <div class="editor-actions card-modal-actions">
        <div class="card-modal-actions-left">
          <button type="submit" class="btn" :disabled="!canSubmit" :aria-label="submitLabel" :title="submitLabel">
            <Check :size="16" aria-hidden="true" />
            <span>{{ submitLabel }}</span>
          </button>
          <button type="button" class="btn btn--secondary" :disabled="busy" aria-label="Cancel creation" title="Cancel" @click="emit('close')">
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
import { computed, ref, watch } from 'vue';
import ModalDialog from './ModalDialog.vue';

type BoardCreateDialogSubmitPayload =
  | { mode: 'blank'; name: string }
  | { mode: 'tasksmd'; url: string };

const props = defineProps<{
  open: boolean;
  busy: boolean;
}>();

const emit = defineEmits<{
  close: [];
  submit: [payload: BoardCreateDialogSubmitPayload];
}>();

const mode = ref<'blank' | 'tasksmd'>('blank');
const boardName = ref('');
const tasksMdUrl = ref('');

const submitLabel = computed(() => (mode.value === 'blank' ? 'Create board' : 'Import board'));
const canSubmit = computed(() => {
  if (props.busy) {
    return false;
  }

  if (mode.value === 'blank') {
    return boardName.value.trim().length > 0;
  }

  return tasksMdUrl.value.trim().length > 0;
});

function resetDraft() {
  mode.value = 'blank';
  boardName.value = '';
  tasksMdUrl.value = '';
}

function submit() {
  if (mode.value === 'blank') {
    emit('submit', { mode: 'blank', name: boardName.value.trim() });
    return;
  }

  emit('submit', { mode: 'tasksmd', url: tasksMdUrl.value.trim() });
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

<style scoped>
.board-create-dialog-modes {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  padding: 0.2rem;
  width: fit-content;
  max-width: 100%;
  align-self: flex-start;
  border: 1px solid color-mix(in oklab, var(--bo-colour-energy) 45%, var(--bo-border-soft));
  border-radius: 999px;
  background: color-mix(in oklab, var(--bo-colour-energy) 14%, var(--bo-surface-base));
  margin-bottom: 0.75rem;
}

.board-create-dialog-modes :deep(.btn--tab) {
  flex: 0 0 auto;
}
</style>
