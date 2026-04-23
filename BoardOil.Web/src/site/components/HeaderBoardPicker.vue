<template>
  <div v-if="shouldRender" class="board-switcher">
    <RouterLink
      v-if="currentBoardTarget"
      :to="currentBoardTarget"
      class="board-current-link"
      aria-label="Open current board"
    >
      <span class="board-current-name">{{ currentBoardName }}</span>
    </RouterLink>
    <BoDropdown
      class="board-switcher-dropdown"
      align="right"
      icon-only
      label="Switch board"
      :icon="ChevronDown"
    >
      <template #default="{ close }">
        <RouterLink
          v-for="boardSummary in switcherBoards"
          :key="boardSummary.id"
          :to="{ name: 'board', params: { boardId: boardSummary.id } }"
          class="bo-dropdown-item"
          @click="close"
        >
          <span class="bo-dropdown-item-main">{{ boardSummary.name }}</span>
          <span class="badge bo-dropdown-item-meta">#{{ boardSummary.id }}</span>
        </RouterLink>
        <span class="bo-dropdown-divider" aria-hidden="true"></span>
        <RouterLink to="/boards" class="bo-dropdown-item" @click="close">Manage Boards</RouterLink>
      </template>
    </BoDropdown>
  </div>
</template>

<script setup lang="ts">
import { ChevronDown } from 'lucide-vue-next';
import { computed } from 'vue';
import type { Board, BoardSummary } from '../../shared/types/boardTypes';
import BoDropdown from '../../shared/components/BoDropdown.vue';
import { getCurrentBoardName, getCurrentBoardTarget, getSortedBoards } from './appHeaderNavigation';

const props = defineProps<{
  isAuthenticated: boolean;
  board: Board | null;
  boards: BoardSummary[];
  currentBoardId: number | null;
}>();

const currentBoardName = computed(() => getCurrentBoardName(props.board, props.boards, props.currentBoardId));
const currentBoardTarget = computed(() => getCurrentBoardTarget(props.currentBoardId));
const switcherBoards = computed(() => getSortedBoards(props.boards));
const shouldRender = computed(() => props.isAuthenticated && props.currentBoardId !== null && currentBoardName.value !== '');
</script>

<style scoped>
.board-current-link {
  display: inline-flex;
  align-items: center;
  min-width: 0;
  max-width: min(24rem, 58vw);
  height: 2rem;
  padding: 0.35rem 0.3rem 0.35rem 0.1rem;
  border: none;
  border-radius: 8px;
  background: transparent;
  color: var(--bo-link);
  text-decoration: none;
  vertical-align: middle;
}

.board-current-link:hover,
.board-current-link:focus-visible {
  background: var(--bo-surface-energy);
}

.board-current-link:focus-visible {
  outline: 2px solid var(--bo-colour-energy);
  outline-offset: 2px;
}

.board-current-name {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-weight: 700;
}

@media (max-width: 720px) {
  .board-current-link {
    flex: 1 1 auto;
    min-width: 0;
    max-width: 100%;
    height: 1.85rem;
    padding: 0.2rem 0.2rem 0.2rem 0.1rem;
  }
}
</style>
