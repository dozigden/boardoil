<template>
  <div v-if="shouldRender" class="board-switcher">
    <RouterLink
      v-if="currentBoardTarget"
      :to="currentBoardTarget"
      class="board-current-link"
      aria-label="Open current board"
      @click="closeSwitcher"
    >
      <span class="board-current-name">{{ currentBoardName }}</span>
    </RouterLink>
    <details v-if="showBoardSwitcher" ref="switcher" class="board-switcher-menu">
      <summary class="board-switcher-trigger" aria-label="Switch board">
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
  </div>
</template>

<script setup lang="ts">
import { ChevronDown } from 'lucide-vue-next';
import { computed, ref } from 'vue';
import type { Board, BoardSummary } from '../types/boardTypes';
import { useClickOutside } from '../composables/useClickOutside';
import { getCurrentBoardName, getCurrentBoardTarget, getOtherBoards } from './appHeaderNavigation';

const props = defineProps<{
  isAuthenticated: boolean;
  board: Board | null;
  boards: BoardSummary[];
  currentBoardId: number | null;
}>();

const switcher = ref<HTMLDetailsElement | null>(null);
const currentBoardName = computed(() => getCurrentBoardName(props.board, props.boards, props.currentBoardId));
const currentBoardTarget = computed(() => getCurrentBoardTarget(props.currentBoardId));
const otherBoards = computed(() => getOtherBoards(props.boards, props.currentBoardId));
const showBoardSwitcher = computed(() => props.isAuthenticated && props.currentBoardId !== null && props.boards.length > 1);
const shouldRender = computed(() => props.isAuthenticated && props.currentBoardId !== null && currentBoardName.value !== '');

function closeSwitcher() {
  if (switcher.value) {
    switcher.value.open = false;
  }
}

useClickOutside(switcher, closeSwitcher, () => showBoardSwitcher.value && switcher.value?.open === true);
</script>

<style scoped>
.board-switcher {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  margin-left: 1rem;
}

.board-switcher-menu {
  position: relative;
  display: inline-flex;
  align-items: center;
}

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
}

.board-current-link:hover,
.board-current-link:focus-visible {
  background: var(--bo-surface-energy);
}

.board-current-link:focus-visible {
  outline: 2px solid var(--bo-colour-energy);
  outline-offset: 2px;
}

.board-switcher-trigger {
  display: inline-flex;
  align-items: center;
  height: 2rem;
  padding: 0.35rem 0.5rem;
  list-style: none;
  cursor: pointer;
  border: 1px solid var(--bo-border-brand);
  border-radius: 999px;
  background: var(--bo-surface-panel);
  color: var(--bo-link);
  user-select: none;
}

.board-switcher-trigger::-webkit-details-marker {
  display: none;
}

.board-switcher-icon {
  flex: 0 0 auto;
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
    flex: 1 1 auto;
    min-width: 0;
    margin-left: 0.25rem;
    gap: 0.2rem;
  }

  .board-current-link {
    flex: 1 1 auto;
    min-width: 0;
    max-width: 100%;
    height: 1.85rem;
    padding: 0.2rem 0.2rem 0.2rem 0.1rem;
  }

  .board-switcher-trigger {
    flex: 0 0 auto;
    height: 1.85rem;
    padding: 0.25rem 0.45rem;
  }

  .board-switcher-panel {
    left: auto;
    right: 0;
    width: min(18rem, 92vw);
    max-width: min(18rem, 92vw);
  }
}
</style>
