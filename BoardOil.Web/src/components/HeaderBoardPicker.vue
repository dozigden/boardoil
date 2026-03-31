<template>
  <div v-if="shouldRender" class="board-switcher">
    <details v-if="showBoardSwitcher" ref="switcher">
      <summary class="board-switcher-trigger" aria-label="Switch board">
        <span class="board-switcher-name">{{ currentBoardName }}</span>
        <ChevronDown :size="16" aria-hidden="true" class="board-switcher-icon" />
      </summary>
      <nav class="board-switcher-panel" aria-label="Switch board">
        <RouterLink
          v-for="boardSummary in otherBoards"
          :key="boardSummary.id"
          :to="{ name: 'board', params: { boardId: boardSummary.id } }"
          class="board-switcher-item"
          @click="closeSwitcher"
        >
          <span class="board-switcher-item-name">{{ boardSummary.name }}</span>
          <span class="badge board-switcher-item-meta">#{{ boardSummary.id }}</span>
        </RouterLink>
      </nav>
    </details>
    <div v-else class="board-current" aria-label="Current board">
      <span class="board-current-name">{{ currentBoardName }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ChevronDown } from 'lucide-vue-next';
import { computed, ref } from 'vue';
import type { Board, BoardSummary } from '../types/boardTypes';
import { getCurrentBoardName, getOtherBoards } from './appHeaderNavigation';

const props = defineProps<{
  isAuthenticated: boolean;
  board: Board | null;
  boards: BoardSummary[];
  currentBoardId: number | null;
}>();

const switcher = ref<HTMLDetailsElement | null>(null);
const currentBoardName = computed(() => getCurrentBoardName(props.board, props.boards, props.currentBoardId));
const otherBoards = computed(() => getOtherBoards(props.boards, props.currentBoardId));
const showBoardSwitcher = computed(() => props.isAuthenticated && props.currentBoardId !== null && props.boards.length > 1);
const shouldRender = computed(() => props.isAuthenticated && props.currentBoardId !== null && currentBoardName.value !== '');

function closeSwitcher() {
  if (switcher.value) {
    switcher.value.open = false;
  }
}
</script>

<style scoped>
.board-switcher {
  display: inline-flex;
  align-items: center;
  margin-left: 1rem;
}

.board-switcher > details {
  position: relative;
  display: inline-flex;
  align-items: center;
}

.board-switcher-trigger {
  display: inline-flex;
  align-items: center;
  gap: 0.45rem;
  min-width: min(20rem, 52vw);
  max-width: min(24rem, 58vw);
  height: 2rem;
  padding: 0.35rem 0.7rem;
  list-style: none;
  cursor: pointer;
  border: 1px solid var(--bo-border-brand);
  border-radius: 999px;
  background: var(--bo-surface-panel);
  color: var(--bo-link);
  user-select: none;
}

.board-current {
  display: inline-flex;
  align-items: center;
  gap: 0.45rem;
  min-width: min(20rem, 52vw);
  max-width: min(24rem, 58vw);
  height: 2rem;
  padding: 0.35rem 0.7rem;
  border: 1px solid var(--bo-border-brand);
  border-radius: 999px;
  background: var(--bo-surface-panel);
  color: var(--bo-link);
}

.board-switcher-trigger::-webkit-details-marker {
  display: none;
}

.board-switcher-name {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-weight: 700;
}

.board-switcher-icon {
  flex: 0 0 auto;
  margin-left: auto;
}

.board-current-name {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-weight: 700;
}

.board-switcher-panel {
  position: absolute;
  right: 0;
  top: calc(100% + 0.35rem);
  min-width: min(20rem, 80vw);
  max-width: min(24rem, 80vw);
  background: var(--bo-surface-base);
  border: 1px solid var(--bo-border-default);
  border-radius: 8px;
  padding: 0.35rem;
  box-shadow: var(--bo-shadow-pop);
  z-index: 10;
}

.board-switcher-item {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 0.75rem;
  text-decoration: none;
  color: var(--bo-ink-default);
  border-radius: 6px;
  padding: 0.45rem 0.55rem;
}

.board-switcher-item-name {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.board-switcher-item-meta {
  flex: 0 0 auto;
  color: var(--bo-ink-subtle);
}

.board-switcher-item:hover,
.board-switcher-item:focus-visible {
  background: var(--bo-surface-panel);
  color: var(--bo-link);
}

.board-switcher-trigger:hover,
.board-switcher-trigger:focus-visible {
  background: var(--bo-surface-brand);
  border-color: var(--bo-border-brand);
  color: var(--bo-link);
}

@media (max-width: 720px) {
  .board-switcher {
    flex: 1 1 100%;
  }

  .board-switcher-trigger {
    width: 100%;
    min-width: 0;
    max-width: none;
  }

  .board-current {
    width: 100%;
    min-width: 0;
    max-width: none;
  }

  .board-switcher-panel {
    left: 0;
    right: auto;
    width: 100%;
    max-width: none;
  }
}
</style>
