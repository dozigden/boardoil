<template>
  <div
    class="card"
    :class="{
      'card--selected': selected,
      'card--dragging': isDragging,
      'card--drop-before': dropIndicator === 'before',
      'card--drop-after': dropIndicator === 'after'
    }"
    :style="cardStyle"
    :draggable="!selectionMode"
    :role="selectionMode ? 'checkbox' : 'button'"
    :aria-checked="selectionMode ? selected : undefined"
    tabindex="0"
    @click="handlePrimaryAction"
    @keydown.enter.prevent="handlePrimaryAction"
    @keydown.space.prevent="handlePrimaryAction"
    @dragstart="onDragStart"
    @dragend="onDragEnd"
  >
    <div class="card-header">
      <strong class="card-title">
        <span
          v-if="selectionMode"
          class="card-selection-indicator"
          :class="{ 'card-selection-indicator--selected': selected }"
          aria-hidden="true"
        >
          {{ selected ? '✓' : '' }}
        </span>
        <span class="card-title-text">{{ resolvedCardTypeEmoji ? `${resolvedCardTypeEmoji} ` : '' }}{{ card.title }}</span>
      </strong>
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

    <p v-if="card.assignedUserName" class="card-assigned-to">Assigned to {{ card.assignedUserName }}</p>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';
import type { Card as BoardCard } from '../../shared/types/boardTypes';
import { useCardTypeStore } from '../stores/cardTypeStore';
import { getCardSurfaceStyle } from '../../shared/utils/cardTypeStyles';
import Tag from './Tag.vue';

const props = withDefaults(defineProps<{
  card: BoardCard;
  columnId: number;
  dropIndicator?: 'none' | 'before' | 'after';
  selectionMode?: boolean;
  selected?: boolean;
}>(), {
  dropIndicator: 'none',
  selectionMode: false,
  selected: false
});

const emit = defineEmits<{
  'start-drag': [cardId: number, fromColumnId: number];
  'end-drag': [];
  'edit-card': [cardId: number];
  'toggle-select': [cardId: number];
}>();

const cardTypeStore = useCardTypeStore();
const isDragging = ref(false);
const resolvedCardType = computed(() => cardTypeStore.getCardTypeById(props.card.cardTypeId));
const resolvedCardTypeEmoji = computed(() => resolvedCardType.value?.emoji ?? null);
const cardStyle = computed(() => getCardSurfaceStyle(resolvedCardType.value));

function onDragStart(event: DragEvent) {
  if (props.selectionMode) {
    event.preventDefault();
    return;
  }

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

function handlePrimaryAction() {
  if (isDragging.value) {
    return;
  }

  if (props.selectionMode) {
    emit('toggle-select', props.card.id);
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

.card--selected {
  border-color: color-mix(in oklab, var(--bo-colour-brand) 58%, var(--bo-border-default));
  background: color-mix(in oklab, var(--bo-colour-brand) 4%, var(--bo-surface-base));
  box-shadow:
    inset 6px 0 0 color-mix(in oklab, var(--bo-colour-brand) 84%, var(--bo-colour-brand-strong)),
    inset 0 0 0 1px color-mix(in oklab, var(--bo-colour-brand) 26%, transparent);
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
  display: inline-flex;
  align-items: flex-start;
  gap: 0.45rem;
  min-width: 0;
  line-height: 1.25;
}

.card-title-text {
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

.card-assigned-to {
  margin: 0.4rem 0 0;
  font-size: 0.82rem;
  color: var(--bo-ink-muted);
}

.card-selection-indicator {
  width: 1rem;
  height: 1rem;
  border-radius: 999px;
  border: 1px solid var(--bo-border-default);
  color: transparent;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 0.72rem;
  line-height: 1;
  margin-top: 0.04rem;
  flex: 0 0 auto;
}

.card-selection-indicator--selected {
  border-color: var(--bo-colour-brand);
  background: var(--bo-colour-brand);
  color: var(--bo-ink-on-brand);
}
</style>
