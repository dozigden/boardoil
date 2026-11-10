<template>
  <section class="entity-rows-page">
    <header class="entity-rows-header">
      <h2>Boards</h2>
      <button
        v-if="isAuthenticated"
        type="button"
        class="btn"
        :disabled="busy"
        @click="openCreateDialog"
      >
        Create board
      </button>
    </header>

    <p v-if="boards.length === 0" class="entity-rows-empty">No boards yet.</p>

    <ul v-else class="entity-rows-list">
      <li v-for="board in boards" :key="`${board.id}-${board.name}`">
        <div class="entity-row">
          <button
            type="button"
            class="entity-row-main entity-row-main-button"
            :disabled="busy"
            :aria-label="`Configure board ${board.name}`"
            @click="openBoard(board.id)"
          >
            <span class="badge">#{{ board.id }}</span>
            <span class="entity-row-title">{{ board.name }}</span>
          </button>
          <div class="entity-row-actions">
            <BoDropdown
              v-if="board.currentUserRole === 'Owner'"
              align="right"
              icon-only
              label="Board actions"
              :icon="MoreVertical"
              :disabled="busy"
            >
              <template #default="{ close }">
                <button type="button" class="bo-dropdown-item" @click="openBoardFromMenu(board.id, close)">
                  View
                </button>
                <button type="button" class="bo-dropdown-item" @click="openConfigurationFromMenu(board.id, close)">
                  Configuration
                </button>
                <span class="bo-dropdown-divider" aria-hidden="true"></span>
                <button type="button" class="bo-dropdown-item" @click="openDeleteFromMenu(board.id, close)">
                  Delete
                </button>
              </template>
            </BoDropdown>
          </div>
        </div>
      </li>
    </ul>

    <BoardCreateDialog
      v-if="isCreateDialogOpen"
      @close="closeCreateDialog"
      @saved="boardCreated"
    />
  </section>
</template>

<script setup lang="ts">
import { MoreVertical } from '@lucide/vue';
import { storeToRefs } from 'pinia';
import { onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import BoardCreateDialog from '../components/BoardCreateDialog.vue';
import BoDropdown from '../../shared/components/BoDropdown.vue';
import type { BoardSummary } from '../../shared/types/boardTypes';
import { useAuthStore } from '../../shared/stores/authStore';
import { useBoardCatalogueStore } from '../../shared/stores/boardCatalogueStore';

const router = useRouter();
const authStore = useAuthStore();
const boardCatalogueStore = useBoardCatalogueStore();
const { isAuthenticated } = storeToRefs(authStore);
const { boards, busy } = storeToRefs(boardCatalogueStore);

const isCreateDialogOpen = ref(false);
onMounted(async () => {
  await boardCatalogueStore.loadBoards();
});

async function openBoard(boardId: number) {
  await router.push({ name: 'board', params: { boardId } });
}

async function openBoardConfiguration(boardId: number) {
  await router.push({ name: 'board-details', params: { boardId } });
}

function openCreateDialog() {
  isCreateDialogOpen.value = true;
}

function closeCreateDialog() {
  isCreateDialogOpen.value = false;
}

async function boardCreated(created: BoardSummary) {
  isCreateDialogOpen.value = false;
  await router.push({ name: 'board', params: { boardId: created.id } });
}

async function openConfigurationFromMenu(boardId: number, close: () => void) {
  close();
  await openBoardConfiguration(boardId);
}

async function openBoardFromMenu(boardId: number, close: () => void) {
  close();
  await openBoard(boardId);
}

async function openDeleteFromMenu(boardId: number, close: () => void) {
  close();
  await router.push({ name: 'board-delete', params: { boardId } });
}
</script>

<style scoped>
.entity-rows-page {
  margin-top: 0;
}
</style>
