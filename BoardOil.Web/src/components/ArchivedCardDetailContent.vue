<template>
  <section class="card-editor-layout archived-card-detail-layout">
    <div class="card-editor-main">
      <div class="archived-card-detail-title-row">
        <span v-if="card.cardTypeEmoji" class="archived-card-detail-emoji">{{ card.cardTypeEmoji }}</span>
        <h3 class="archived-card-detail-title">#{{ archivedCard.originalCardId }} {{ card.title }}</h3>
      </div>

      <section class="card-editor-description-field archived-card-detail-description">
        <span class="card-editor-field-label">Description</span>
        <MdViewer
          :model-value="descriptionForDisplay"
          aria-label="Archived card description"
        />
      </section>
    </div>

    <aside class="card-editor-options archived-card-detail-options" aria-label="Archived card details">
      <div class="card-editor-option-section">
        <span class="card-editor-field-label">Tags</span>
        <span v-if="card.tagNames.length > 0" class="archived-card-tags">
          <Tag
            v-for="tagName in card.tagNames"
            :key="`detail-${archivedCard.id}-${tagName}`"
            class="archived-card-tag"
            :tagName="tagName"
            enable-fallback
          />
        </span>
        <span v-else class="archived-card-tags-empty">-</span>
      </div>

      <div class="card-editor-option-section">
        <span class="card-editor-field-label">Column</span>
        <span>{{ columnLabel }}</span>
      </div>

      <div class="card-editor-option-section">
        <span class="card-editor-field-label">Type</span>
        <span>{{ cardTypeLabel }}</span>
      </div>

      <div class="card-editor-option-section">
        <span class="card-editor-field-label">Archived</span>
        <span>{{ formatDateTime(archivedCard.archivedAtUtc) }}</span>
      </div>

      <div class="card-editor-option-section">
        <span class="card-editor-field-label">Created</span>
        <span>{{ formatDateTime(card.createdAtUtc) }}</span>
      </div>

      <div class="card-editor-option-section">
        <span class="card-editor-field-label">Updated</span>
        <span>{{ formatDateTime(card.updatedAtUtc) }}</span>
      </div>
    </aside>
  </section>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import MdViewer from './MdViewer.vue';
import Tag from './Tag.vue';
import type { ArchivedCard } from '../types/boardTypes';

const props = defineProps<{
  archivedCard: ArchivedCard;
  columnTitle?: string | null;
}>();

const card = computed(() => props.archivedCard.card);

const cardTypeLabel = computed(() => {
  const value = card.value;
  return value.cardTypeEmoji ? `${value.cardTypeEmoji} ${value.cardTypeName}` : value.cardTypeName;
});

const columnLabel = computed(() => {
  const value = props.columnTitle?.trim();
  if (value && value.length > 0) {
    return value;
  }

  return `#${card.value.boardColumnId}`;
});

const descriptionForDisplay = computed(() => {
  const value = card.value.description.trim();
  return value.length > 0 ? value : 'No description.';
});

function formatDateTime(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.valueOf())) {
    return value;
  }

  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short'
  }).format(date);
}
</script>

<style scoped>
.card-editor-layout {
  display: grid;
  grid-template-columns: minmax(0, 3fr) minmax(14rem, 1fr);
  gap: 0.85rem;
  flex: 1 1 0;
  min-height: 0;
  overflow: hidden;
}

.card-editor-main {
  display: flex;
  flex-direction: column;
  gap: 0.7rem;
  min-width: 0;
  min-height: 0;
  overflow: hidden;
}

.archived-card-detail-title-row {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
}

.archived-card-detail-emoji {
  font-size: 1.05rem;
  line-height: 1;
}

.archived-card-detail-title {
  margin: 0;
  font-size: 1.08rem;
  line-height: 1.25;
}

.card-editor-description-field {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  flex: 1 1 0;
  min-height: 0;
  overflow: hidden;
}

.card-editor-options {
  display: flex;
  flex-direction: column;
  gap: 0.65rem;
  min-width: 0;
  min-height: 0;
  border-left: 1px solid var(--bo-border-soft);
  padding-left: 0.85rem;
  overflow: auto;
}

.card-editor-option-section {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
  min-width: 0;
}

.card-editor-field-label {
  font-size: 0.85rem;
  color: var(--bo-ink-muted);
}

.archived-card-tags {
  display: inline-flex;
  flex-wrap: wrap;
  gap: 0.3rem;
}

.archived-card-tag {
  border: 1px solid var(--bo-border-soft);
  border-radius: 999px;
  padding: 0.15rem 0.5rem;
  font-size: 0.8rem;
  color: var(--bo-ink-muted);
  background: var(--bo-surface-energy);
}

.archived-card-tags-empty {
  color: var(--bo-ink-subtle);
}

@media (max-width: 900px) {
  .card-editor-layout {
    grid-template-columns: minmax(0, 1fr);
    grid-template-rows: minmax(0, 1fr) auto;
  }

  .card-editor-options {
    border-left: none;
    border-top: 1px solid var(--bo-border-soft);
    padding-left: 0;
    padding-top: 0.75rem;
  }
}

@media (max-width: 720px) {
  .card-editor-layout {
    gap: 0.6rem;
  }

  .card-editor-options {
    gap: 0.55rem;
    padding-top: 0.6rem;
  }
}
</style>
