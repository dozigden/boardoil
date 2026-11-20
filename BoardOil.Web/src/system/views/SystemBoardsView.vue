<template>
  <section class="entity-rows-page">
    <nav class="system-boards-tabs" role="tablist" aria-label="System boards">
      <RouterLink
        :to="{ name: 'system-admin-boards' }"
        class="system-boards-tab"
        :class="{ 'is-active': !props.showDeleted }"
        role="tab"
        :aria-selected="!props.showDeleted"
      >
        Boards
      </RouterLink>
      <RouterLink
        :to="{ name: 'system-admin-deleted-boards' }"
        class="system-boards-tab"
        :class="{ 'is-active': props.showDeleted }"
        role="tab"
        :aria-selected="props.showDeleted"
      >
        Deleted Boards
      </RouterLink>
    </nav>

    <template v-if="!props.showDeleted">
      <p v-if="boards.length === 0 && !busy" class="entity-rows-empty">No boards yet.</p>

      <ul v-if="boards.length > 0" class="entity-rows-list">
        <li v-for="board in boards" :key="board.id" class="entity-row">
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
        </li>
      </ul>
    </template>
    <template v-else>
      <p v-if="deletedBoards.length === 0 && !busy" class="entity-rows-empty">No deleted boards.</p>

      <ul v-if="deletedBoards.length > 0" class="entity-rows-list">
        <li v-for="board in deletedBoards" :key="board.id" class="entity-row">
          <div class="entity-row-main">
            <span class="badge">#{{ board.id }}</span>
            <span class="entity-row-title">{{ board.name }}</span>
            <time class="deleted-board-time" :datetime="board.deletedAtUtc">
              {{ new Date(board.deletedAtUtc).toLocaleString() }}
            </time>
          </div>
          <div class="entity-row-actions">
            <button
              type="button"
              class="btn btn--secondary"
              :disabled="busy"
              :aria-label="`Restore board ${board.name}`"
              @click="systemBoardStore.restoreBoard(board.id)"
            >
              Restore
            </button>
            <button
              type="button"
              class="btn btn--danger"
              :disabled="busy || purgingBoardIds.includes(board.id)"
              :aria-label="`Purge board ${board.name}`"
              @click="purgeBoard(board)"
            >
              Purge
            </button>
          </div>
        </li>
      </ul>
    </template>
  </section>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { onMounted, onUnmounted } from 'vue';
import { RouterLink, useRoute, useRouter } from 'vue-router';
import { useConfirm } from '../../shared/composables/useConfirm';
import type { DeletedBoardSummary } from '../../shared/types/boardTypes';
import { useSystemBoardStore } from '../stores/systemBoardStore';

const props = defineProps<{ showDeleted?: boolean }>();
const route = useRoute();
const router = useRouter();
const { confirm } = useConfirm();
const systemBoardStore = useSystemBoardStore();
const { boards, deletedBoards, busy, purgingBoardIds } = storeToRefs(systemBoardStore);

onMounted(async () => {
  await systemBoardStore.loadBoards();
});

onUnmounted(() => {
  systemBoardStore.dispose();
});

async function openMembers(boardId: number) {
  await router.push({ name: 'system-admin-board-members', params: { boardId } });
}

async function purgeBoard(board: DeletedBoardSummary) {
  const accepted = await confirm({
    title: 'Permanently delete board',
    message: `Permanently delete "${board.name}" and its contents? This cannot be undone.`,
    confirmLabel: 'Purge board',
    danger: true
  });
  if (!accepted || route.name !== 'system-admin-deleted-boards') return;

  await systemBoardStore.queueBoardPurge(board.id);
}
</script>

<style scoped>
.system-boards-tabs {
  display: flex;
  align-items: flex-end;
  gap: 0.2rem;
  overflow-x: auto;
  overflow-y: hidden;
  border-bottom: 1px solid var(--bo-border-default);
}

.system-boards-tab {
  flex: 0 0 auto;
  margin-bottom: -1px;
  padding: 0.65rem 0.7rem 0.55rem;
  border-bottom: 3px solid transparent;
  color: var(--bo-ink-muted);
  font-weight: 650;
  text-decoration: none;
}

.system-boards-tab:hover,
.system-boards-tab:focus-visible {
  color: var(--bo-link);
}

.system-boards-tab:focus-visible {
  outline: 2px solid var(--bo-focus-ring);
  outline-offset: -2px;
}

.system-boards-tab.is-active {
  border-bottom-color: var(--bo-colour-energy);
  color: var(--bo-ink-strong);
}

.deleted-board-time {
  color: var(--bo-ink-muted);
  font-size: 0.85rem;
}
</style>
