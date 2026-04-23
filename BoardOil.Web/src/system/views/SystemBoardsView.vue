<template>
  <section class="entity-rows-page">
    <header class="entity-rows-header">
      <h2>Boards</h2>
    </header>

    <p v-if="boards.length === 0" class="entity-rows-empty">No boards yet.</p>

    <ul v-else class="entity-rows-list">
      <li v-for="board in boards" :key="`${board.id}-${board.name}`">
        <div class="entity-row">
          <button
            type="button"
            class="entity-row-main entity-row-main-button"
            :disabled="busy"
            :aria-label="`Manage members for board ${board.name}`"
            @click="openMembers(board.id)"
          >
            <span class="badge">#{{ board.id }}</span>
            <span class="entity-row-title">{{ board.name }}</span>
          </button>
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
