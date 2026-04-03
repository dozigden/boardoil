<template>
  <section class="board-details-page">
    <h2>Details</h2>

    <form class="board-details-form" @submit.prevent="saveBoardName">
      <label>
        Board name
        <input
          v-model="boardNameDraft"
          :disabled="!isOwner || busy"
          maxlength="120"
          required
        />
      </label>

      <p v-if="!isOwner" class="board-details-owner-note">Owner permission required to rename this board.</p>

      <div class="board-details-actions">
        <button type="submit" class="btn" :disabled="!canSave">
          Save name
        </button>
        <button type="button" class="btn btn--secondary" :disabled="busy || !hasChanges" @click="resetDraft">
          Reset
        </button>
      </div>
    </form>
  </section>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useBoardCatalogueStore } from '../stores/boardCatalogueStore';
import { useBoardStore } from '../stores/boardStore';

const route = useRoute();
const router = useRouter();
const boardStore = useBoardStore();
const boardCatalogueStore = useBoardCatalogueStore();
const { board } = storeToRefs(boardStore);
const { busy } = storeToRefs(boardCatalogueStore);
const boardNameDraft = ref('');

const boardId = computed(() => resolveBoardId());
const boardName = computed(() => board.value?.name ?? '');
const isOwner = computed(() => board.value?.currentUserRole === 'Owner');
const hasChanges = computed(() => boardNameDraft.value.trim() !== boardName.value.trim());
const canSave = computed(() => isOwner.value && !busy.value && boardNameDraft.value.trim().length > 0 && hasChanges.value);

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
  },
  { immediate: true }
);

function resetDraft() {
  boardNameDraft.value = boardName.value;
}

async function saveBoardName() {
  const nextBoardId = boardId.value;
  if (nextBoardId === null || !canSave.value) {
    return;
  }

  const saved = await boardCatalogueStore.saveBoard(nextBoardId, boardNameDraft.value.trim());
  if (!saved) {
    return;
  }

  if (board.value?.id === nextBoardId) {
    board.value = {
      ...board.value,
      name: saved.name,
      updatedAtUtc: saved.updatedAtUtc
    };
  }

  boardNameDraft.value = saved.name;
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
</style>
