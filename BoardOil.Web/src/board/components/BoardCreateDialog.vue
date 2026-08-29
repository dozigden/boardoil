<template>
  <FixedChromeDialog :open="open" title="Create Board" close-label="Cancel creation" @close="emit('close')" @submit="submit">
    <div class="btn-tab-list board-create-dialog-modes" role="tablist" aria-label="Board create mode">
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
    </div>

    <label v-if="mode === 'blank'">
      Board name
      <input v-model="boardName" :disabled="busy" maxlength="120" autocomplete="off" data-lpignore="true" required />
    </label>
    <label v-if="mode === 'blank'">
      Description (optional)
      <textarea v-model="boardDescription" :disabled="busy" maxlength="5000" rows="4"></textarea>
    </label>
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
          autocomplete="off"
          data-lpignore="true"
          placeholder="Leave empty to use package board name"
        />
      </label>
    </template>

    <template #actions>
      <div class="fixed-chrome-dialog-actions">
        <div class="fixed-chrome-dialog-actions-left">
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
  </FixedChromeDialog>
</template>

<script setup lang="ts">
import { Check, X } from 'lucide-vue-next';
import { computed, ref, watch } from 'vue';
import FixedChromeDialog from '../../shared/components/FixedChromeDialog.vue';
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
const packageFile = ref<File | null>(null);
const packageFileName = ref('');
const packageBoardNameOverride = ref('');

const submitLabel = computed(() => (mode.value === 'blank' ? 'Create board' : 'Import board'));
const canSubmit = computed(() =>
  canSubmitBoardCreateDraft({
    mode: mode.value,
    boardName: boardName.value,
    boardDescription: boardDescription.value,
    packageFile: packageFile.value,
    packageBoardNameOverride: packageBoardNameOverride.value
  }, props.busy));

function resetDraft() {
  mode.value = 'blank';
  boardName.value = '';
  boardDescription.value = '';
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
  width: fit-content;
  max-width: 100%;
  align-self: flex-start;
  margin-bottom: 0.75rem;
}

.board-create-dialog-file-name {
  margin: -0.2rem 0 0.2rem;
  font-size: 0.85rem;
  color: var(--bo-ink-muted);
  word-break: break-word;
}

</style>
