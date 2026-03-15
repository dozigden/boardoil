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
</template>

<script setup lang="ts">
import { Check, Plus, X } from 'lucide-vue-next';
import { storeToRefs } from 'pinia';
import { nextTick, ref } from 'vue';
import { useRouter } from 'vue-router';
import Card from '../components/Card.vue';
import { useBoardStore } from '../stores/boardStore';

const newCardDraftTitles = ref<Record<number, string>>({});
const newCardDraftInputs = ref<Record<number, HTMLInputElement | null>>({});

const router = useRouter();
const boardStore = useBoardStore();
const { board, typingSummary } = storeToRefs(boardStore);
const { createCard, startDrag, dropCard } = boardStore;

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

async function openCardEditor(cardId: number) {
  await router.push({ name: 'board-card', params: { cardId } });
}
</script>
