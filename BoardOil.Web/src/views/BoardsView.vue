<template>
  <section class="boards-view">
    <header class="boards-header">
      <h2>Boards</h2>
      <button
        v-if="isAdmin"
        type="button"
        class="btn"
        :disabled="busy"
        @click="openCreateDialog"
      >
        Create board
      </button>
    </header>

    <p v-if="boards.length === 0" class="boards-empty">No boards yet.</p>

    <ul v-else class="boards-list">
      <li v-for="board in boards" :key="`${board.id}-${board.name}`" class="boards-item">
        <div class="boards-row">
          <button type="button" class="btn boards-open" @click="openBoard(board.id)">
            <span class="card-id boards-meta">#{{ board.id }}</span>
            <span class="boards-name">{{ board.name }}</span>
          </button>
          <div v-if="isAdmin" class="boards-actions">
            <button
              type="button"
              class="btn btn--ghost boards-action boards-action-icon"
              :disabled="busy"
              aria-label="Rename board"
              title="Rename board"
              @click="openRenameDialog(board)"
            >
              <Pencil :size="16" aria-hidden="true" />
            </button>
            <button
              type="button"
              class="btn btn--ghost boards-action boards-action-danger boards-action-icon"
              :disabled="busy"
              aria-label="Delete board"
              title="Delete board"
              @click="confirmDeleteBoard(board)"
            >
              <Trash2 :size="16" aria-hidden="true" />
            </button>
          </div>
        </div>
      </li>
    </ul>

    <BoardCreateDialog
      :open="isCreateDialogOpen"
      :busy="busy"
      @close="closeCreateDialog"
      @submit="submitCreateBoard"
    />
    <BoardRenameDialog
      :open="editingBoard !== null"
      :busy="busy"
      :initial-name="editingBoard?.name ?? ''"
      @close="closeRenameDialog"
      @submit="submitRenameBoard"
    />
  </section>
</template>

<script setup lang="ts">
import { Pencil, Trash2 } from 'lucide-vue-next';
import { storeToRefs } from 'pinia';
import { onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import BoardCreateDialog from '../components/BoardCreateDialog.vue';
import BoardRenameDialog from '../components/BoardRenameDialog.vue';
import { useAuthStore } from '../stores/authStore';
import { useBoardCatalogueStore } from '../stores/boardCatalogueStore';
import type { BoardSummary } from '../types/boardTypes';

const router = useRouter();
const authStore = useAuthStore();
const boardCatalogueStore = useBoardCatalogueStore();
const { isAdmin } = storeToRefs(authStore);
const { boards, busy } = storeToRefs(boardCatalogueStore);

const isCreateDialogOpen = ref(false);
const editingBoard = ref<BoardSummary | null>(null);

onMounted(async () => {
  await boardCatalogueStore.loadBoards();
});

async function openBoard(boardId: number) {
  await router.push({ name: 'board', params: { boardId } });
}

function openCreateDialog() {
  isCreateDialogOpen.value = true;
}

function closeCreateDialog() {
  isCreateDialogOpen.value = false;
}

function openRenameDialog(board: BoardSummary) {
  editingBoard.value = { ...board };
}

function closeRenameDialog() {
  editingBoard.value = null;
}

async function submitCreateBoard(payload: { name: string }) {
  const created = await boardCatalogueStore.createBoard(payload.name);
  if (!created) {
    return;
  }

  isCreateDialogOpen.value = false;
  await router.push({ name: 'board', params: { boardId: created.id } });
}

async function submitRenameBoard(payload: { name: string }) {
  const board = editingBoard.value;
  if (!board) {
    return;
  }

  const updated = await boardCatalogueStore.saveBoard(board.id, payload.name);
  if (!updated) {
    return;
  }

  editingBoard.value = null;
}

async function confirmDeleteBoard(board: BoardSummary) {
  const shouldDelete = window.confirm(`Delete board "${board.name}"? This will permanently remove its columns and cards.`);
  if (!shouldDelete) {
    return;
  }

  await boardCatalogueStore.deleteBoard(board.id);
}
</script>
