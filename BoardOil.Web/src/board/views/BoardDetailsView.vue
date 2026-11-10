<template>
  <section class="board-details-page">
    <FormPage :definition="form" discard-message="Discard your board details changes and leave?">
      <template #after-fields="{ busy, dirty, reset }">
        <p v-if="!isOwner" class="board-details-owner-note">Owner permission required to edit this board.</p>
        <div class="board-details-actions">
          <button type="button" class="btn btn--secondary" :disabled="busy || !dirty" @click="reset">Reset</button>
        </div>
      </template>
    </FormPage>

    <section class="board-export-section">
      <h3>Export board</h3>
      <p>Download a ZIP file.</p>
      <p v-if="!isOwner" class="board-details-owner-note">Owner permission required to export this board.</p>
      <div class="board-details-actions">
        <button type="button" class="btn" :disabled="!canExport" @click="exportBoardPackage">
          {{ exporting ? 'Exporting...' : 'Export' }}
        </button>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed, ref } from 'vue';
import { createBoardApi } from '../../shared/api/boardApi';
import FormPage from '../../shared/forms/FormPage.vue';
import { createBoardDetailsForm } from '../forms/boardDetailsForm';
import { useBoardStore } from '../stores/boardStore';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';

const boardStore = useBoardStore();
const feedbackStore = useUiFeedbackStore();
const boardApi = createBoardApi();
const { board } = storeToRefs(boardStore);
const boardId = boardStore.currentBoardId!;
const exporting = ref(false);
const isOwner = computed(() => boardStore.isCurrentUserOwner);
const canExport = computed(() => isOwner.value && !exporting.value);
const form = createBoardDetailsForm(board.value!, {
  save: values => boardStore.saveBoard(boardId, values),
  canEdit: () => isOwner.value,
  isCurrent: () => boardStore.currentBoardId === boardId,
  supportsAttachments: boardApi.supportsAttachments
});

async function exportBoardPackage() {
  if (!canExport.value) {
    return;
  }
  const nextBoardId = boardId;

  exporting.value = true;
  try {
    const result = await boardApi.exportBoard(nextBoardId);
    if (!result.ok) {
      feedbackStore.setError(result.error.message, 'boardExport');
      return;
    }

    feedbackStore.clearError('boardExport');
    const objectUrl = URL.createObjectURL(result.data.blob);
    const anchor = document.createElement('a');
    anchor.href = objectUrl;
    anchor.download = result.data.fileName;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(objectUrl);
  } finally {
    exporting.value = false;
  }
}

</script>

<style scoped>
.board-details-page {
  display: grid;
  gap: 0.8rem;
  max-width: 34rem;
}

.board-details-page h2 {
  margin: 0;
}

.board-details-owner-note {
  margin: 0;
  color: var(--bo-ink-muted);
}

.board-details-actions {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.board-export-section {
  display: grid;
  gap: 0.5rem;
  padding: 0.9rem;
  border: 1px solid var(--bo-border-soft);
  border-radius: 12px;
  background: var(--bo-surface-panel);
}

.board-export-section h3,
.board-export-section p {
  margin: 0;
}
</style>
