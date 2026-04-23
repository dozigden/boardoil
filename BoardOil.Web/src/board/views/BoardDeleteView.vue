<template>
  <section class="board-delete-page">
    <h2>Delete Board</h2>
    <p class="board-delete-warning">
      This will permanently delete the board, including all columns and cards.
    </p>

    <section class="board-delete-panel">
      <p class="board-delete-confirm-copy">
        Type <strong>{{ boardName }}</strong> to confirm deletion.
      </p>

      <label>
        Confirm board name
        <input
          v-model="confirmationName"
          :disabled="!isOwner || busy"
          maxlength="120"
          autocomplete="off"
          required
        />
      </label>

      <p v-if="!isOwner" class="board-delete-owner-note">Owner permission required to delete this board.</p>

      <div class="board-delete-actions">
        <button
          type="button"
          class="btn btn--danger"
          :disabled="!canDelete"
          @click="deleteBoard"
        >
          Delete board
        </button>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useBoardCatalogueStore } from '../../shared/stores/boardCatalogueStore';
import { useBoardStore } from '../stores/boardStore';

const route = useRoute();
const router = useRouter();
const boardStore = useBoardStore();
const boardCatalogueStore = useBoardCatalogueStore();
const { board } = storeToRefs(boardStore);
const { busy } = storeToRefs(boardCatalogueStore);
const confirmationName = ref('');

const boardId = computed(() => resolveBoardId());
const boardName = computed(() => board.value?.name ?? 'this board');
const isOwner = computed(() => board.value?.currentUserRole === 'Owner');
const canDelete = computed(() => isOwner.value && !busy.value && confirmationName.value.trim() === boardName.value.trim());

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

    confirmationName.value = '';
  },
  { immediate: true }
);

async function deleteBoard() {
  const nextBoardId = boardId.value;
  if (nextBoardId === null || !canDelete.value) {
    return;
  }

  const deleted = await boardCatalogueStore.deleteBoard(nextBoardId);
  if (!deleted) {
    return;
  }

  await boardStore.dispose();
  await router.push({ name: 'boards' });
}

function resolveBoardId() {
  const parsed = Number.parseInt(String(route.params.boardId ?? ''), 10);
  return Number.isFinite(parsed) ? parsed : null;
}
</script>

<style scoped>
.board-delete-page {
  display: grid;
  gap: 0.8rem;
  max-width: 36rem;
}

.board-delete-page h2 {
  margin: 0;
}

.board-delete-warning {
  margin: 0;
  color: var(--bo-colour-danger-ink);
  font-weight: 600;
}

.board-delete-panel {
  display: grid;
  gap: 0.75rem;
  padding: 0.9rem;
  border: 1px solid color-mix(in oklab, var(--bo-colour-danger) 55%, var(--bo-border-soft));
  border-radius: 12px;
  background: var(--bo-surface-panel);
}

.board-delete-confirm-copy,
.board-delete-owner-note {
  margin: 0;
}

.board-delete-owner-note {
  color: var(--bo-ink-muted);
}

.board-delete-actions {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}
</style>
