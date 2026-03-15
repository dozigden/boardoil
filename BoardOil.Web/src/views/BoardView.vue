<template>
  <section v-if="board" class="board">
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

      <Card
        v-for="(card, index) in column.cards"
        :key="card.id"
        :card="card"
        :column-id="column.id"
        :index="index"
        :typing-summary="typingSummary"
        @start-drag="startDrag"
        @drop-card="dropCard"
        @edit-card="openCardEditor"
      />

      <form class="create-card" @submit.prevent="createCardForColumn(column.id)">
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

  <CardEditorDialog
    :open="editingCardId !== null"
    :card="editingCard"
    :draft="editingCardDraft"
    @close="closeCardEditor"
    @save="saveCard"
    @delete="deleteEditingCard"
    @announce-typing="announceEditingCardTyping"
    @stop-typing="stopEditingCardTyping"
    @update-draft="updateEditingCardDraft"
  />
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed, ref, watch } from 'vue';
import Card from '../components/Card.vue';
import CardEditorDialog from '../components/CardEditorDialog.vue';
import { useBoardStore } from '../stores/boardStore';
import type { Card as BoardCard } from '../types/boardTypes';

const newCardTitles = ref<Record<number, string>>({});
const cardDrafts = ref<Record<number, { title: string; description: string }>>({});
const editingCardId = ref<number | null>(null);

const boardStore = useBoardStore();
const { board, busy, typingSummary } = storeToRefs(boardStore);
const { createCard, saveCard: saveCardAction, deleteCard, startDrag, dropCard, announceTyping, stopTyping } =
  boardStore;
const editingCard = computed(() =>
  editingCardId.value === null ? null : board.value?.columns.flatMap(x => x.cards).find(x => x.id === editingCardId.value) ?? null
);
const editingCardDraft = computed(() =>
  editingCardId.value === null ? null : cardDrafts.value[editingCardId.value] ?? null
);

function updateNewCardTitle(columnId: number, value: string) {
  newCardTitles.value[columnId] = value;
}

async function createCardForColumn(columnId: number) {
  const title = newCardTitles.value[columnId] ?? '';
  await createCard(columnId, title);
  newCardTitles.value[columnId] = '';
}

function openCardEditor(cardId: number) {
  if (editingCardId.value === cardId) {
    return;
  }

  const card = findCard(cardId);
  if (!card) {
    return;
  }

  cardDrafts.value[cardId] = { title: card.title, description: card.description };
  editingCardId.value = cardId;
}

function closeCardEditor() {
  const cardId = editingCardId.value;
  if (cardId !== null) {
    stopTyping(cardId, 'title');
    stopTyping(cardId, 'description');
  }

  editingCardId.value = null;
}

function updateCardDraft(cardId: number, field: 'title' | 'description', value: string) {
  const existing = cardDrafts.value[cardId] ?? { title: '', description: '' };
  cardDrafts.value[cardId] = { ...existing, [field]: value };
}

function updateEditingCardDraft(field: 'title' | 'description', value: string) {
  const cardId = editingCardId.value;
  if (cardId === null) {
    return;
  }

  updateCardDraft(cardId, field, value);
}

function announceEditingCardTyping(field: 'title' | 'description') {
  const cardId = editingCardId.value;
  if (cardId === null) {
    return;
  }

  announceTyping(cardId, field);
}

function stopEditingCardTyping(field: 'title' | 'description') {
  const cardId = editingCardId.value;
  if (cardId === null) {
    return;
  }

  stopTyping(cardId, field);
}

async function saveCard() {
  const cardId = editingCardId.value;
  if (cardId === null) {
    return;
  }

  const draft = cardDrafts.value[cardId];
  if (!draft) {
    return;
  }

  await saveCardAction(cardId, draft.title, draft.description);
  closeCardEditor();
}

async function deleteEditingCard() {
  const cardId = editingCardId.value;
  if (cardId === null) {
    return;
  }

  await deleteCard(cardId);
  closeCardEditor();
}

function findCard(cardId: number): BoardCard | null {
  return board.value?.columns.flatMap(x => x.cards).find(x => x.id === cardId) ?? null;
}

watch(editingCard, card => {
  if (editingCardId.value !== null && !card) {
    closeCardEditor();
  }
});
</script>
