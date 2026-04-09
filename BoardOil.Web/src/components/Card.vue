<template>
  <div
    class="card"
    :class="{
      'card--dragging': isDragging,
      'card--drop-before': dropIndicator === 'before',
      'card--drop-after': dropIndicator === 'after'
    }"
    :style="cardStyle"
    draggable="true"
    role="button"
    tabindex="0"
    @click="openEditor"
    @keydown.enter.prevent="openEditor"
    @keydown.space.prevent="openEditor"
    @dragstart="onDragStart"
    @dragend="onDragEnd"
  >
    <div class="card-header">
      <strong class="card-title">{{ resolvedCardTypeEmoji ? `${resolvedCardTypeEmoji} ` : '' }}{{ card.title }}</strong>
      <span class="card-id">#{{ card.id }}</span>
    </div>

    <div v-if="card.tags.length > 0" class="card-tags tag-group" aria-label="Card tags">
      <Tag
        v-for="tag in card.tags"
        :key="tag.id"
        :tag-id="tag.id"
      >
      </Tag>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';
import type { Card as BoardCard } from '../types/boardTypes';
import { useCardTypeStore } from '../stores/cardTypeStore';
import { getCardSurfaceStyle } from '../utils/cardTypeStyles';
import Tag from './Tag.vue';

const props = defineProps<{
  card: BoardCard;
  columnId: number;
  dropIndicator?: 'none' | 'before' | 'after';
}>();

const emit = defineEmits<{
  'start-drag': [cardId: number, fromColumnId: number];
  'end-drag': [];
  'edit-card': [cardId: number];
}>();

const cardTypeStore = useCardTypeStore();
const isDragging = ref(false);
const resolvedCardType = computed(() => cardTypeStore.getCardTypeById(props.card.cardTypeId));
const resolvedCardTypeEmoji = computed(() => resolvedCardType.value?.emoji ?? null);
const cardStyle = computed(() => getCardSurfaceStyle(resolvedCardType.value));

function onDragStart(event: DragEvent) {
  if (event.dataTransfer) {
    event.dataTransfer.effectAllowed = 'move';
    event.dataTransfer.setData('text/plain', String(props.card.id));
  }

  isDragging.value = true;
  emit('start-drag', props.card.id, props.columnId);
}

function onDragEnd() {
  emit('end-drag');

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

<style scoped>
.card {
  border: 1px solid var(--bo-border-soft);
  border-radius: 12px;
  padding: 0.6rem;
  background: var(--bo-surface-base);
  margin-bottom: 0.5rem;
  cursor: pointer;
  position: relative;
}

.card--dragging {
  opacity: 0.7;
  border-style: dashed;
}

.card--drop-before::before,
.card--drop-after::after {
  content: '';
  position: absolute;
  left: 0.25rem;
  right: 0.25rem;
  height: 3px;
  border-radius: 999px;
  background: var(--bo-focus-ring);
  box-shadow: 0 0 0 2px color-mix(in srgb, var(--bo-focus-ring) 30%, transparent);
}

.card--drop-before::before {
  top: -0.45rem;
}

.card--drop-after::after {
  bottom: -0.45rem;
}

.card:focus-visible {
  outline: 2px solid var(--bo-focus-ring);
  outline-offset: 2px;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 0.5rem;
  margin-bottom: 0.55rem;
}

.card-title {
  min-width: 0;
  line-height: 1.25;
  overflow-wrap: anywhere;
}

.card-id {
  flex: 0 0 auto;
  font-weight: 600;
  line-height: 1.25;
}

.card-tags {
  margin-top: 0.3rem;
}
</style>
