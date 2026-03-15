<template>
  <main class="app-shell">
    <header class="app-header">
      <h1>BoardOil</h1>
      <p>Live board for small trusted teams.</p>
      <nav class="view-switch">
        <button
          :class="{ active: currentView === 'board' }"
          type="button"
          @click="goToView('board')"
        >
          Board
        </button>
        <button
          :class="{ active: currentView === 'columns' }"
          type="button"
          @click="goToView('columns')"
        >
          Manage Columns
        </button>
      </nav>
    </header>

    <section v-if="currentView === 'columns'" class="toolbar">
      <form class="create-column" @submit.prevent="createColumn">
        <input v-model="newColumnTitle" type="text" maxlength="200" placeholder="New column title" />
        <button type="submit" :disabled="busy">Add Column</button>
      </form>
    </section>

    <p v-if="errorMessage" class="error">{{ errorMessage }}</p>

    <section v-if="board && currentView === 'board'" class="board">
      <article
        v-for="column in board.columns"
        :key="column.id"
        class="column"
        @dragover.prevent
        @drop="dropCard(column.id, column.cards.length)"
      >
        <header class="column-header">
          <h2 class="column-name">{{ column.title }}</h2>
        </header>

        <div
          v-for="(card, index) in column.cards"
          :key="card.id"
          class="card"
          draggable="true"
          @dragstart="startDrag(card.id, column.id)"
          @dragover.prevent
          @drop="dropCard(column.id, index)"
        >
          <div class="card-header">
            <strong>{{ card.title }}</strong>
            <button class="ghost" @click="toggleCardEditor(card.id)">
              {{ editingCardId === card.id ? 'Close' : 'Edit' }}
            </button>
          </div>

          <p class="description">{{ card.description }}</p>

          <div v-if="typingSummary(card.id).length > 0" class="typing">
            Typing: {{ typingSummary(card.id).join(', ') }}
          </div>

          <div v-if="editingCardId === card.id" class="editor">
            <label>
              Title
              <input
                :value="cardDrafts[card.id]?.title ?? card.title"
                maxlength="200"
                @focus="announceTyping(card.id, 'title')"
                @blur="stopTyping(card.id, 'title')"
                @input="updateCardDraft(card.id, 'title', ($event.target as HTMLInputElement).value)"
              />
            </label>

            <label>
              Description
              <textarea
                :value="cardDrafts[card.id]?.description ?? card.description"
                maxlength="5000"
                @focus="announceTyping(card.id, 'description')"
                @blur="stopTyping(card.id, 'description')"
                @input="updateCardDraft(card.id, 'description', ($event.target as HTMLTextAreaElement).value)"
              />
            </label>

            <div class="editor-actions">
              <button @click="saveCard(card.id)">Save</button>
              <button class="danger" @click="deleteCard(card.id)">Delete</button>
            </div>
          </div>
        </div>

        <form class="create-card" @submit.prevent="createCard(column.id)">
          <input
            :value="newCardTitles[column.id] ?? ''"
            type="text"
            maxlength="200"
            placeholder="New card title"
            @input="updateNewCardTitle(column.id, ($event.target as HTMLInputElement).value)"
          />
          <button type="submit" :disabled="busy">Add Card</button>
        </form>
      </article>
    </section>

    <section v-if="board && currentView === 'columns'" class="column-manager">
      <article v-for="column in board.columns" :key="column.id" class="column-manager-item">
        <label>
          Column title
          <input
            class="column-title"
            :value="columnTitleDrafts[column.id] ?? column.title"
            maxlength="200"
            @input="updateColumnDraft(column.id, ($event.target as HTMLInputElement).value)"
          />
        </label>
        <div class="column-actions">
          <button @click="saveColumn(column.id)">Save</button>
          <button class="danger" @click="deleteColumn(column.id)">Delete</button>
        </div>
      </article>
    </section>
  </main>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { onMounted, onUnmounted, ref } from 'vue';
import { useBoardStore } from './stores/boardStore';
import { useUiFeedbackStore } from './stores/uiFeedbackStore';
import type { Card } from './types/boardTypes';

type ViewMode = 'board' | 'columns';

const currentView = ref<ViewMode>(parseViewFromHash(window.location.hash));
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
  window.addEventListener('hashchange', syncViewFromHash);
  await boardStore.initialize();
});

onUnmounted(async () => {
  window.removeEventListener('hashchange', syncViewFromHash);
  await boardStore.dispose();
});

function parseViewFromHash(hash: string): ViewMode {
  return hash === '#columns' ? 'columns' : 'board';
}

function syncViewFromHash() {
  currentView.value = parseViewFromHash(window.location.hash);
}

function goToView(view: ViewMode) {
  const hash = view === 'columns' ? '#columns' : '#board';
  if (window.location.hash !== hash) {
    window.location.hash = hash;
  } else {
    currentView.value = view;
  }
}

async function createColumn() {
  const created = await createColumnAction(newColumnTitle.value);
  if (created) {
    newColumnTitle.value = '';
  }
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
  const created = await createCardAction(columnId, title);
  if (created) {
    newCardTitles.value[columnId] = '';
  }
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
