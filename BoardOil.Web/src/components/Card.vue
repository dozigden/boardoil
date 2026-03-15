<template>
  <div
    class="card"
    draggable="true"
    role="button"
    tabindex="0"
    @click="openEditor"
    @keydown.enter.prevent="openEditor"
    @keydown.space.prevent="openEditor"
    @dragstart="onDragStart"
    @dragover.prevent
    @drop="emit('drop-card', columnId, index)"
    @dragend="onDragEnd"
  >
    <div class="card-header">
      <strong>{{ card.title }}</strong>
    </div>

    <p class="description">{{ card.description }}</p>

    <div v-if="typingSummary(card.id).length > 0" class="typing">
      Typing: {{ typingSummary(card.id).join(', ') }}
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import type { Card as BoardCard } from '../types/boardTypes';

const props = defineProps<{
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

const isDragging = ref(false);

function onDragStart() {
  isDragging.value = true;
  emit('start-drag', props.card.id, props.columnId);
}

function onDragEnd() {
  // Avoid opening editor from the click event that can follow a drag.
  setTimeout(() => {
    isDragging.value = false;
  }, 0);
}

function openEditor() {
  if (isDragging.value) {
    return;
  }

  emit('edit-card', props.card.id);
}
</script>
