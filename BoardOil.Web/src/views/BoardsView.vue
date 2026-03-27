<template>
  <section class="boards-view">
    <header class="boards-header">
      <h2>Boards</h2>
      <p>Select a board to open it.</p>
    </header>

    <form v-if="isAdmin" class="boards-create" @submit.prevent="submitCreateBoard">
      <label class="boards-create-label">
        New board name
        <input
          v-model="newBoardName"
          maxlength="120"
          placeholder="Roadmap"
          :disabled="busy"
        />
      </label>
      <button type="submit" :disabled="busy || !newBoardName.trim()">Create Board</button>
    </form>

    <p v-if="boards.length === 0" class="boards-empty">No boards yet.</p>

    <ul v-else class="boards-list">
      <li v-for="board in boards" :key="`${board.id}-${board.name}`" class="boards-item">
        <button type="button" class="boards-open" @click="openBoard(board.id)">
          <span class="boards-name">{{ board.name }}</span>
          <span class="card-id boards-meta">#{{ board.id }}</span>
        </button>
      </li>
    </ul>
  </section>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import { useAuthStore } from '../stores/authStore';
import { useBoardCatalogueStore } from '../stores/boardCatalogueStore';

const router = useRouter();
const authStore = useAuthStore();
const boardCatalogueStore = useBoardCatalogueStore();
const { isAdmin } = storeToRefs(authStore);
const { boards, busy } = storeToRefs(boardCatalogueStore);

const newBoardName = ref('');

onMounted(async () => {
  await boardCatalogueStore.loadBoards();
});

async function openBoard(boardId: number) {
  await router.push({ name: 'board', params: { boardId } });
}

async function submitCreateBoard() {
  const created = await boardCatalogueStore.createBoard(newBoardName.value.trim());
  if (!created) {
    return;
  }

  newBoardName.value = '';
  await router.push({ name: 'board', params: { boardId: created.id } });
}
</script>
