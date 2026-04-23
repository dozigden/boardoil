<template>
  <section class="board-details-page">
    <h2>Details</h2>

    <form class="board-details-form" @submit.prevent="saveBoardDetails">
      <label>
        Board name
        <input
          v-model="boardNameDraft"
          :disabled="!isOwner || busy"
          maxlength="120"
          required
        />
      </label>
      <label>
        Description (optional)
        <textarea
          v-model="boardDescriptionDraft"
          :disabled="!isOwner || busy"
          maxlength="5000"
          rows="8"
        ></textarea>
      </label>

      <p v-if="!isOwner" class="board-details-owner-note">Owner permission required to edit this board.</p>

      <div class="board-details-actions">
        <button type="submit" class="btn" :disabled="!canSave">
          Save details
        </button>
        <button type="button" class="btn btn--secondary" :disabled="busy || !hasChanges" @click="resetDraft">
          Reset
        </button>
      </div>
    </form>

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
import { computed, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { createBoardApi } from '../../shared/api/boardApi';
import { useBoardCatalogueStore } from '../../shared/stores/boardCatalogueStore';
import { useBoardStore } from '../stores/boardStore';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';

const route = useRoute();
const router = useRouter();
const boardStore = useBoardStore();
const boardCatalogueStore = useBoardCatalogueStore();
const feedbackStore = useUiFeedbackStore();
const boardApi = createBoardApi();
const { board } = storeToRefs(boardStore);
const { applyBoardSummaryUpdate } = boardStore;
const { busy } = storeToRefs(boardCatalogueStore);
const boardNameDraft = ref('');
const boardDescriptionDraft = ref('');
const exporting = ref(false);

const boardId = computed(() => resolveBoardId());
const boardName = computed(() => board.value?.name ?? '');
const boardDescription = computed(() => board.value?.description ?? '');
const isOwner = computed(() => board.value?.currentUserRole === 'Owner');
const hasChanges = computed(() =>
  boardNameDraft.value.trim() !== boardName.value.trim()
  || boardDescriptionDraft.value.trim() !== boardDescription.value.trim());
const canSave = computed(() => isOwner.value && !busy.value && boardNameDraft.value.trim().length > 0 && hasChanges.value);
const canExport = computed(() => isOwner.value && !exporting.value);

watch(
  boardId,
  async nextBoardId => {
    if (nextBoardId === null) {
      await router.replace({ name: 'boards' });
      return;
    }

    const loaded = await boardStore.initialize(nextBoardId);
    if (!loaded && boardId.value === nextBoardId) {
      await router.replace({ name: 'boards' });
      return;
    }

    boardNameDraft.value = board.value?.name ?? '';
    boardDescriptionDraft.value = board.value?.description ?? '';
  },
  { immediate: true }
);

function resetDraft() {
  boardNameDraft.value = boardName.value;
  boardDescriptionDraft.value = boardDescription.value;
}

async function saveBoardDetails() {
  const nextBoardId = boardId.value;
  if (nextBoardId === null || !canSave.value) {
    return;
  }

  const saved = await boardCatalogueStore.saveBoard(
    nextBoardId,
    boardNameDraft.value.trim(),
    boardDescriptionDraft.value.trim());
  if (!saved) {
    return;
  }

  if (board.value?.id === nextBoardId) {
    applyBoardSummaryUpdate(saved);
  }

  boardNameDraft.value = saved.name;
  boardDescriptionDraft.value = saved.description;
}

async function exportBoardPackage() {
  const nextBoardId = boardId.value;
  if (nextBoardId === null || !canExport.value) {
    return;
  }

  exporting.value = true;
  try {
    const result = await boardApi.exportBoard(nextBoardId);
    if (!result.ok) {
      feedbackStore.setError(result.error.message);
      return;
    }

    feedbackStore.clearError();
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

function resolveBoardId() {
  const parsed = Number.parseInt(String(route.params.boardId ?? ''), 10);
  return Number.isFinite(parsed) ? parsed : null;
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

.board-details-form {
  display: grid;
  gap: 0.75rem;
  padding: 0.9rem;
  border: 1px solid var(--bo-border-soft);
  border-radius: 12px;
  background: var(--bo-surface-panel);
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
