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
        :is-editing="editingCardId === card.id"
        :draft="cardDrafts[card.id]"
        :typing-summary="typingSummary"
        @start-drag="startDrag"
        @drop-card="dropCard"
        @toggle-card-editor="toggleCardEditor"
        @announce-typing="announceTyping"
        @stop-typing="stopTyping"
        @update-card-draft="updateCardDraft"
        @save-card="saveCard"
        @delete-card="deleteCard"
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
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { ref } from 'vue';
import Card from '../components/Card.vue';
import { useBoardStore } from '../stores/boardStore';
import type { Card as BoardCard } from '../types/boardTypes';

const newCardTitles = ref<Record<number, string>>({});
const cardDrafts = ref<Record<number, { title: string; description: string }>>({});
const editingCardId = ref<number | null>(null);

const boardStore = useBoardStore();
const { board, busy, typingSummary } = storeToRefs(boardStore);
const { createCard, saveCard: saveCardAction, deleteCard, startDrag, dropCard, announceTyping, stopTyping } =
  boardStore;

function updateNewCardTitle(columnId: number, value: string) {
  newCardTitles.value[columnId] = value;
}

async function createCardForColumn(columnId: number) {
  const title = newCardTitles.value[columnId] ?? '';
  await createCard(columnId, title);
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

function findCard(cardId: number): BoardCard | null {
  return board.value?.columns.flatMap(x => x.cards).find(x => x.id === cardId) ?? null;
}
</script>
