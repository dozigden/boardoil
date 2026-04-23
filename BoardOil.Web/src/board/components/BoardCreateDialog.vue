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
        :class="{ 'is-active': mode === 'package' }"
        role="tab"
        :aria-selected="mode === 'package'"
        :disabled="busy"
        @click="mode = 'package'"
      >
        Import package
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
    <label v-if="mode === 'blank'">
      Description (optional)
      <textarea v-model="boardDescription" :disabled="busy" maxlength="5000" rows="4"></textarea>
    </label>
    <template v-else-if="mode === 'tasksmd'">
      <label>
        tasksmd URL
        <input v-model="tasksMdUrl" :disabled="busy" maxlength="2000" required type="url" placeholder="https://tasks.example.net/" />
      </label>
    </template>
    <template v-else>
      <label>
        Package ZIP file
        <input
          type="file"
          :disabled="busy"
          accept=".zip,application/zip"
          required
          @change="handlePackageFileChanged"
        />
      </label>
      <p v-if="packageFileName" class="board-create-dialog-file-name">{{ packageFileName }}</p>
      <label>
        Board name override (optional)
        <input
          v-model="packageBoardNameOverride"
          :disabled="busy"
          maxlength="120"
          placeholder="Leave empty to use package board name"
        />
      </label>
    </template>

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
import ModalDialog from '../../shared/components/ModalDialog.vue';
import {
  buildBoardCreateSubmitPayload,
  canSubmitBoardCreateDraft,
  type BoardCreateDialogSubmitPayload,
  type BoardCreateMode
} from './boardCreateDialogModel';

const props = defineProps<{
  open: boolean;
  busy: boolean;
}>();

const emit = defineEmits<{
  close: [];
  submit: [payload: BoardCreateDialogSubmitPayload];
}>();

const mode = ref<BoardCreateMode>('blank');
const boardName = ref('');
const boardDescription = ref('');
const tasksMdUrl = ref('');
const packageFile = ref<File | null>(null);
const packageFileName = ref('');
const packageBoardNameOverride = ref('');

const submitLabel = computed(() => (mode.value === 'blank' ? 'Create board' : 'Import board'));
const canSubmit = computed(() =>
  canSubmitBoardCreateDraft({
    mode: mode.value,
    boardName: boardName.value,
    boardDescription: boardDescription.value,
    tasksMdUrl: tasksMdUrl.value,
    packageFile: packageFile.value,
    packageBoardNameOverride: packageBoardNameOverride.value
  }, props.busy));

function resetDraft() {
  mode.value = 'blank';
  boardName.value = '';
  boardDescription.value = '';
  tasksMdUrl.value = '';
  packageFile.value = null;
  packageFileName.value = '';
  packageBoardNameOverride.value = '';
}

function handlePackageFileChanged(event: Event) {
  const input = event.target as HTMLInputElement | null;
  const selectedFile = input?.files?.[0] ?? null;
  packageFile.value = selectedFile;
  packageFileName.value = selectedFile?.name ?? '';
}

function submit() {
  const payload = buildBoardCreateSubmitPayload({
    mode: mode.value,
    boardName: boardName.value,
    boardDescription: boardDescription.value,
    tasksMdUrl: tasksMdUrl.value,
    packageFile: packageFile.value,
    packageBoardNameOverride: packageBoardNameOverride.value
  });
  if (!payload) {
    return;
  }

  emit('submit', payload);
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

.board-create-dialog-file-name {
  margin: -0.2rem 0 0.2rem;
  font-size: 0.85rem;
  color: var(--bo-ink-muted);
  word-break: break-word;
}
</style>
