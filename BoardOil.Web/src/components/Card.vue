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
      <div class="card-title-with-pill">
        <strong>{{ card.title }}</strong>
        <span v-if="typingSummary(card.id)" class="typing-pill" aria-label="Someone is typing">...</span>
      </div>
      <span class="card-id">#{{ card.id }}</span>
    </div>

    <p class="description">{{ card.description }}</p>

    <div v-if="card.tagNames.length > 0" class="card-tags" aria-label="Card tags">
      <Tag
        v-for="tagName in card.tagNames"
        :key="tagName"
        :tag-name="tagName"
      >
      </Tag>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import type { Card as BoardCard } from '../types/boardTypes';
import Tag from './Tag.vue';

const props = defineProps<{
  card: BoardCard;
  columnId: number;
  index: number;
  typingSummary: (cardId: number) => boolean;
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
