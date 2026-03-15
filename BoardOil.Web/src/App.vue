<template>
  <main class="app-shell">
    <AppHeader :current-view="currentView" @change-view="goToView" />

    <p v-if="errorMessage" class="error">{{ errorMessage }}</p>

    <BoardView
      v-if="board && currentView === 'board'"
      :columns="board.columns"
      :busy="busy"
      :editing-card-id="editingCardId"
      :card-drafts="cardDrafts"
      :new-card-titles="newCardTitles"
      :typing-summary="typingSummary"
      @start-drag="startDrag"
      @drop-card="dropCard"
      @toggle-card-editor="toggleCardEditor"
      @announce-typing="announceTyping"
      @stop-typing="stopTyping"
      @update-card-draft="updateCardDraft"
      @save-card="saveCard"
      @delete-card="deleteCard"
      @create-card="createCard"
      @update-new-card-title="updateNewCardTitle"
    />

    <ColumnsManagerView
      v-if="board && currentView === 'columns'"
      :columns="board.columns"
      :busy="busy"
      :new-column-title="newColumnTitle"
      :column-title-drafts="columnTitleDrafts"
      @create-column="createColumn"
      @update-new-column-title="updateNewColumnTitle"
      @update-column-draft="updateColumnDraft"
      @save-column="saveColumn"
      @delete-column="deleteColumn"
    />
  </main>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { onMounted, onUnmounted, ref } from 'vue';
import AppHeader from './components/AppHeader.vue';
import BoardView from './components/BoardView.vue';
import ColumnsManagerView from './components/ColumnsManagerView.vue';
import { useViewHash } from './composables/useViewHash';
import { useBoardStore } from './stores/boardStore';
import { useUiFeedbackStore } from './stores/uiFeedbackStore';
import type { Card } from './types/boardTypes';

const { currentView, goToView } = useViewHash();
const newColumnTitle = ref('');
const newCardTitles = ref<Record<number, string>>({});
const columnTitleDrafts = ref<Record<number, string>>({});
const cardDrafts = ref<Record<number, { title: string; description: string }>>({});
const editingCardId = ref<number | null>(null);

const boardStore = useBoardStore();
const feedbackStore = useUiFeedbackStore();
const { board, busy, typingSummary } = storeToRefs(boardStore);
const { errorMessage } = storeToRefs(feedbackStore);
const {
  createColumn: createColumnAction,
  saveColumn: saveColumnAction,
  deleteColumn,
  createCard: createCardAction,
  saveCard: saveCardAction,
  deleteCard,
  startDrag,
  dropCard,
  announceTyping,
  stopTyping
} = boardStore;

onMounted(async () => {
  await boardStore.initialize();
});

onUnmounted(async () => {
  await boardStore.dispose();
});

function updateNewColumnTitle(value: string) {
  newColumnTitle.value = value;
}

async function createColumn() {
  await createColumnAction(newColumnTitle.value);
  newColumnTitle.value = '';
}

function updateColumnDraft(columnId: number, value: string) {
  columnTitleDrafts.value[columnId] = value;
}

async function saveColumn(columnId: number) {
  const title = columnTitleDrafts.value[columnId];
  if (title === undefined) {
    return;
  }

  await saveColumnAction(columnId, title);
}

function updateNewCardTitle(columnId: number, value: string) {
  newCardTitles.value[columnId] = value;
}

async function createCard(columnId: number) {
  const title = newCardTitles.value[columnId] ?? '';
  await createCardAction(columnId, title);
  newCardTitles.value[columnId] = '';
}

function toggleCardEditor(cardId: number) {
  if (editingCardId.value === cardId) {
    editingCardId.value = null;
    return;
  }

  const card = findCard(cardId);
  if (!card) {
    return;
  }

  cardDrafts.value[cardId] = { title: card.title, description: card.description };
  editingCardId.value = cardId;
}

function updateCardDraft(cardId: number, field: 'title' | 'description', value: string) {
  const existing = cardDrafts.value[cardId] ?? { title: '', description: '' };
  cardDrafts.value[cardId] = { ...existing, [field]: value };
  announceTyping(cardId, field);
}

async function saveCard(cardId: number) {
  const draft = cardDrafts.value[cardId];
  if (!draft) {
    return;
  }

  await saveCardAction(cardId, draft.title, draft.description);
  editingCardId.value = null;
}

function findCard(cardId: number): Card | null {
  return board.value?.columns.flatMap(x => x.cards).find(x => x.id === cardId) ?? null;
}
</script>
