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
    @drop="emit('drop-card', columnId, card.id)"
    @dragend="onDragEnd"
  >
    <div class="card-header">
      <strong>{{ resolvedCardTypeEmoji ? `${resolvedCardTypeEmoji} ` : '' }}{{ card.title }}</strong>
      <span class="badge">#{{ card.id }}</span>
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
import Tag from './Tag.vue';

const props = defineProps<{
  card: BoardCard;
  columnId: number;
}>();

const emit = defineEmits<{
  'start-drag': [cardId: number, fromColumnId: number];
  'drop-card': [targetColumnId: number, targetCardId: number];
  'edit-card': [cardId: number];
}>();

const cardTypeStore = useCardTypeStore();
const isDragging = ref(false);
const resolvedCardTypeEmoji = computed(() => cardTypeStore.getCardTypeById(props.card.cardTypeId)?.emoji ?? null);

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

<style scoped>
.card {
  border: 1px solid var(--bo-border-soft);
  border-radius: 12px;
  padding: 0.6rem;
  background: var(--bo-surface-base);
  margin-bottom: 0.5rem;
  cursor: pointer;
}

.card:focus-visible {
  outline: 2px solid var(--bo-focus-ring);
  outline-offset: 2px;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.55rem;
}

.card-tags {
  margin-top: 0.3rem;
}
</style>
