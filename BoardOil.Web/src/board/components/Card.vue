<template>
  <div
    class="card"
    :class="[
      cardStyleClasses,
      {
        'card--selection-mode': selectionMode,
        'card--selected': selected,
        'card--dragging': isDragging,
        'card--multi-dragging': isDragging && selectionMode && selectedCount > 1,
        'card--drop-before': dropIndicator === 'before',
        'card--drop-after': dropIndicator === 'after'
      }
    ]"
    :style="cardStyle"
    :draggable="!selectionMode || selected"
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
        <span class="card-title-text"><span v-if="resolvedCardTypeEmoji" class="bo-emoji" aria-hidden="true">{{ resolvedCardTypeEmoji }}</span>{{ resolvedCardTypeEmoji ? ' ' : '' }}{{ card.title }}</span>
      </strong>
      <span class="card-id">#{{ card.id }}</span>
    </div>

    <p v-if="card.assignedUserDisplayName" class="card-assigned-to">
      <UserAvatar
        :image-url="assignedUserImageUrl"
        :display-name="card.assignedUserDisplayName"
        size="md"
        class="card-assigned-avatar"
      />
      <span>{{ card.assignedUserDisplayName }}</span>
    </p>

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
import type { Card as BoardCard } from '../../shared/types/boardTypes';
import { useCardTypeStore } from '../stores/cardTypeStore';
import { getCardSurfaceClassList, getCardSurfaceStyle } from '../../shared/utils/cardTypeStyles';
import { buildApiUrl } from '../../shared/api/config';
import Tag from './Tag.vue';
import UserAvatar from '../../shared/components/UserAvatar.vue';

const props = withDefaults(defineProps<{
  card: BoardCard;
  columnId: number;
  dropIndicator?: 'none' | 'before' | 'after';
  selectionMode?: boolean;
  selected?: boolean;
  selectedCount?: number;
}>(), {
  dropIndicator: 'none',
  selectionMode: false,
  selected: false,
  selectedCount: 0
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
const cardStyleClasses = computed(() => getCardSurfaceClassList(resolvedCardType.value));
const assignedUserImageUrl = computed(() =>
  props.card.assignedUserImageRelativePath ? buildApiUrl(`/images/${props.card.assignedUserImageRelativePath}`) : null
);

function onDragStart(event: DragEvent) {
  if (props.selectionMode && !props.selected) {
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
  border: 1px solid var(--bo-card-surface-border-color, var(--bo-border-soft));
  border-radius: 12px;
  padding: 0.6rem;
  background: var(--bo-card-surface-background, var(--bo-surface-base));
  color: var(--bo-card-surface-color, inherit);
  margin-bottom: 0.5rem;
  cursor: pointer;
  position: relative;
}

.card--selected {
  border-color: color-mix(in oklab, var(--bo-selection-accent) 76%, var(--bo-border-default));
  background: color-mix(in oklab, var(--bo-selection-accent) 10%, var(--bo-surface-base));
  box-shadow:
    inset 0 0 0 999px color-mix(in oklab, var(--bo-selection-accent) 10%, transparent),
    inset 0 0 0 2px color-mix(in oklab, var(--bo-selection-accent) 58%, transparent);
}

.card--selection-mode:hover {
  box-shadow:
    inset 0 0 0 999px color-mix(in oklab, var(--bo-selection-accent) 22%, transparent),
    inset 0 0 0 2px color-mix(in oklab, var(--bo-selection-accent) 82%, transparent);
}

.card.bo-card-style-presets.card--selected {
  border-color: color-mix(in oklab, var(--bo-selection-accent) 76%, var(--bo-card-surface-border-color, var(--bo-border-default)));
  background: color-mix(in oklab, var(--bo-card-surface-background, var(--bo-surface-base)) 82%, var(--bo-selection-accent) 18%);
  color: var(--bo-card-surface-color, inherit);
}

.card--dragging {
  opacity: 0.7;
  border-style: dashed;
}

.card--multi-dragging {
  box-shadow:
    0 0 0 2px color-mix(in oklab, var(--bo-colour-brand) 55%, var(--bo-border-default)),
    10px 10px 0 -2px color-mix(in oklab, var(--bo-surface-base) 88%, var(--bo-border-soft) 12%),
    10px 10px 0 0 color-mix(in oklab, var(--bo-border-default) 78%, transparent),
    20px 20px 0 -4px color-mix(in oklab, var(--bo-surface-base) 88%, var(--bo-border-soft) 12%),
    20px 20px 0 -2px color-mix(in oklab, var(--bo-border-default) 78%, transparent),
    0 8px 20px color-mix(in oklab, var(--bo-colour-brand-strong) 24%, transparent);
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
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.35rem;
  width: 100%;
  min-width: 0;
  margin-top: 0.4rem;
}

.card-tags :deep(.tag) {
  max-width: 100%;
}

.card-assigned-to {
  margin: 0.3rem 0 0;
  font-size: 0.82rem;
  color: inherit;
  display: flex;
  align-items: center;
  gap: 0.3rem;
  width: 100%;
}

.card-assigned-avatar {
  flex-shrink: 0;
}

</style>
