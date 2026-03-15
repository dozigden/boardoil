<template>
  <div
    class="card"
    draggable="true"
    @dragstart="emit('start-drag', card.id, columnId)"
    @dragover.prevent
    @drop="emit('drop-card', columnId, index)"
  >
    <div class="card-header">
      <button type="button" class="card-title-trigger" @click="emit('edit-card', card.id)">
        <strong>{{ card.title }}</strong>
      </button>
    </div>

    <p class="description">{{ card.description }}</p>

    <div v-if="typingSummary(card.id).length > 0" class="typing">
      Typing: {{ typingSummary(card.id).join(', ') }}
    </div>
  </div>
</template>

<script setup lang="ts">
import type { Card as BoardCard } from '../types/boardTypes';

defineProps<{
  card: BoardCard;
  columnId: number;
  index: number;
  typingSummary: (cardId: number) => string[];
}>();

const emit = defineEmits<{
  'start-drag': [cardId: number, fromColumnId: number];
  'drop-card': [targetColumnId: number, position: number];
  'edit-card': [cardId: number];
}>();
</script>
