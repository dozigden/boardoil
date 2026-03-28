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
          <span class="card-id board-switcher-item-meta">#{{ boardSummary.id }}</span>
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
