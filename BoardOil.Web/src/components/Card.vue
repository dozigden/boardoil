<template>
  <div
    class="card"
    draggable="true"
    @dragstart="emit('start-drag', card.id, columnId)"
    @dragover.prevent
    @drop="emit('drop-card', columnId, index)"
  >
    <div class="card-header">
      <strong>{{ card.title }}</strong>
      <button class="ghost" @click="emit('toggle-card-editor', card.id)">
        {{ isEditing ? 'Close' : 'Edit' }}
      </button>
    </div>

    <p class="description">{{ card.description }}</p>

    <div v-if="typingSummary(card.id).length > 0" class="typing">
      Typing: {{ typingSummary(card.id).join(', ') }}
    </div>

    <div v-if="isEditing" class="editor">
      <label>
        Title
        <input
          :value="draft?.title ?? card.title"
          maxlength="200"
          @focus="emit('announce-typing', card.id, 'title')"
          @blur="emit('stop-typing', card.id, 'title')"
          @input="emit('update-card-draft', card.id, 'title', ($event.target as HTMLInputElement).value)"
        />
      </label>

      <label>
        Description
        <textarea
          :value="draft?.description ?? card.description"
          maxlength="5000"
          @focus="emit('announce-typing', card.id, 'description')"
          @blur="emit('stop-typing', card.id, 'description')"
          @input="emit('update-card-draft', card.id, 'description', ($event.target as HTMLTextAreaElement).value)"
        />
      </label>

      <div class="editor-actions">
        <button @click="emit('save-card', card.id)">Save</button>
        <button class="danger" @click="emit('delete-card', card.id)">Delete</button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { Card as BoardCard } from '../types/boardTypes';

defineProps<{
  card: BoardCard;
  columnId: number;
  index: number;
  isEditing: boolean;
  draft?: { title: string; description: string };
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
}>();
</script>
