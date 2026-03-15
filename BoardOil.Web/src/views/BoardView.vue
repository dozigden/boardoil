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
        <button
          type="button"
          class="ghost column-add-card"
          aria-label="Add card"
          title="Add card"
          @click="openNewCardDraft(column.id)"
        >
          <Plus :size="16" aria-hidden="true" />
        </button>
      </header>

      <article v-if="newCardDraftTitles[column.id] !== undefined" class="card create-card-inline">
        <label class="create-card-inline-label">
          Title
          <input
            :ref="element => setNewCardDraftInput(column.id, element)"
            :value="newCardDraftTitles[column.id]"
            type="text"
            maxlength="200"
            placeholder="New card title"
            @input="updateNewCardDraftTitle(column.id, ($event.target as HTMLInputElement).value)"
            @keydown.enter.prevent="saveNewCardDraft(column.id)"
            @keydown.esc.prevent="closeNewCardDraft(column.id)"
          />
        </label>
        <div class="editor-actions create-card-inline-actions">
          <button type="button" class="create-card-save" aria-label="Save new card" title="Save new card" @click="saveNewCardDraft(column.id)">
            <Check :size="16" aria-hidden="true" />
          </button>
          <button
            type="button"
            class="ghost create-card-cancel"
            aria-label="Cancel new card"
            title="Cancel new card"
            @click="closeNewCardDraft(column.id)"
          >
            <X :size="16" aria-hidden="true" />
          </button>
        </div>
      </article>

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
import { Check, Plus, X } from 'lucide-vue-next';
import { storeToRefs } from 'pinia';
import { computed, nextTick, ref, watch } from 'vue';
import Card from '../components/Card.vue';
import CardEditorDialog from '../components/CardEditorDialog.vue';
import { useBoardStore } from '../stores/boardStore';
import type { Card as BoardCard } from '../types/boardTypes';

const newCardDraftTitles = ref<Record<number, string>>({});
const newCardDraftInputs = ref<Record<number, HTMLInputElement | null>>({});
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

async function openNewCardDraft(columnId: number) {
  if (newCardDraftTitles.value[columnId] !== undefined) {
    newCardDraftInputs.value[columnId]?.focus();
    return;
  }

  newCardDraftTitles.value[columnId] = '';
  await nextTick();
  newCardDraftInputs.value[columnId]?.focus();
}

function updateNewCardDraftTitle(columnId: number, value: string) {
  if (newCardDraftTitles.value[columnId] === undefined) {
    return;
  }

  newCardDraftTitles.value[columnId] = value;
}

function closeNewCardDraft(columnId: number) {
  delete newCardDraftTitles.value[columnId];
  delete newCardDraftInputs.value[columnId];
}

function setNewCardDraftInput(columnId: number, element: unknown) {
  newCardDraftInputs.value[columnId] = element instanceof HTMLInputElement ? element : null;
}

async function saveNewCardDraft(columnId: number) {
  const title = newCardDraftTitles.value[columnId] ?? '';
  if (!title.trim()) {
    return;
  }

  await createCard(columnId, title);
  closeNewCardDraft(columnId);
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
