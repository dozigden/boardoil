<template>
  <section class="board">
    <article
      v-for="column in columns"
      :key="column.id"
      class="column"
      @dragover.prevent
      @drop="emit('drop-card', column.id, column.cards.length)"
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
        @start-drag="(cardId, fromColumnId) => emit('start-drag', cardId, fromColumnId)"
        @drop-card="(targetColumnId, position) => emit('drop-card', targetColumnId, position)"
        @toggle-card-editor="cardId => emit('toggle-card-editor', cardId)"
        @announce-typing="(cardId, field) => emit('announce-typing', cardId, field)"
        @stop-typing="(cardId, field) => emit('stop-typing', cardId, field)"
        @update-card-draft="(cardId, field, value) => emit('update-card-draft', cardId, field, value)"
        @save-card="cardId => emit('save-card', cardId)"
        @delete-card="cardId => emit('delete-card', cardId)"
      />

      <form class="create-card" @submit.prevent="emit('create-card', column.id)">
        <input
          :value="newCardTitles[column.id] ?? ''"
          type="text"
          maxlength="200"
          placeholder="New card title"
          @input="emit('update-new-card-title', column.id, ($event.target as HTMLInputElement).value)"
        />
        <button type="submit" :disabled="busy">Add Card</button>
      </form>
    </article>
  </section>
</template>

<script setup lang="ts">
import Card from './Card.vue';
import type { BoardColumn } from '../types/boardTypes';

defineProps<{
  columns: BoardColumn[];
  busy: boolean;
  editingCardId: number | null;
  cardDrafts: Record<number, { title: string; description: string }>;
  newCardTitles: Record<number, string>;
  typingSummary: (cardId: number) => string[];
}>();

const emit = defineEmits<{
  'start-drag': [cardId: number, fromColumnId: number];
  'drop-card': [targetColumnId: number, position: number];
  'toggle-card-editor': [cardId: number];
  'announce-typing': [cardId: number, field: 'title' | 'description'];
  'stop-typing': [cardId: number, field: 'title' | 'description'];
  'update-card-draft': [cardId: number, field: 'title' | 'description', value: string];
  'save-card': [cardId: number];
  'delete-card': [cardId: number];
  'create-card': [columnId: number];
  'update-new-card-title': [columnId: number, value: string];
}>();
</script>
