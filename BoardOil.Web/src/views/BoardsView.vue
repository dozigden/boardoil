<template>
  <section class="boards-view">
    <header class="boards-header">
      <div>
        <h2>Boards</h2>
        <p>Select a board to open it.</p>
      </div>
      <button
        v-if="isAdmin"
        type="button"
        :disabled="busy"
        @click="openCreateDialog"
      >
        Create board
      </button>
    </header>

    <p v-if="boards.length === 0" class="boards-empty">No boards yet.</p>

    <ul v-else class="boards-list">
      <li v-for="board in boards" :key="`${board.id}-${board.name}`" class="boards-item">
        <button type="button" class="boards-open" @click="openBoard(board.id)">
          <span class="boards-name">{{ board.name }}</span>
          <span class="card-id boards-meta">#{{ board.id }}</span>
        </button>
      </li>
    </ul>

    <BoardCreateDialog
      :open="isCreateDialogOpen"
      :busy="busy"
      @close="closeCreateDialog"
      @submit="submitCreateBoard"
    />
  </section>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import BoardCreateDialog from '../components/BoardCreateDialog.vue';
import { useAuthStore } from '../stores/authStore';
import { useBoardCatalogueStore } from '../stores/boardCatalogueStore';

const router = useRouter();
const authStore = useAuthStore();
const boardCatalogueStore = useBoardCatalogueStore();
const { isAdmin } = storeToRefs(authStore);
const { boards, busy } = storeToRefs(boardCatalogueStore);

const isCreateDialogOpen = ref(false);

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

async function submitCreateBoard(payload: { name: string }) {
  const created = await boardCatalogueStore.createBoard(payload.name);
  if (!created) {
    return;
  }

  isCreateDialogOpen.value = false;
  await router.push({ name: 'board', params: { boardId: created.id } });
}
</script>
