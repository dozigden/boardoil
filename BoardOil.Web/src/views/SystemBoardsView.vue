<template>
  <section class="entity-rows-page">
    <header class="entity-rows-header">
      <h2>Boards</h2>
    </header>

    <p v-if="boards.length === 0" class="entity-rows-empty">No boards yet.</p>

    <ul v-else class="entity-rows-list">
      <li v-for="board in boards" :key="`${board.id}-${board.name}`">
        <div class="entity-row">
          <div class="entity-row-main">
            <span class="badge">#{{ board.id }}</span>
            <span class="entity-row-title">{{ board.name }}</span>
          </div>
          <div class="entity-row-actions">
            <button
              type="button"
              class="btn btn--secondary"
              :disabled="busy"
              @click="openMembers(board.id)"
            >
              Manage members
            </button>
          </div>
        </div>
      </li>
    </ul>
  </section>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { onMounted, onUnmounted } from 'vue';
import { useRouter } from 'vue-router';
import { useSystemBoardStore } from '../stores/systemBoardStore';

const router = useRouter();
const systemBoardStore = useSystemBoardStore();
const { boards, busy } = storeToRefs(systemBoardStore);

onMounted(async () => {
  await systemBoardStore.loadBoards();
});

onUnmounted(() => {
  systemBoardStore.dispose();
});

async function openMembers(boardId: number) {
  await router.push({ name: 'system-admin-board-members', params: { boardId } });
}
</script>
