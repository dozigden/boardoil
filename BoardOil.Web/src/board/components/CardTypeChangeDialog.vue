<template>
  <FixedChromeDialog
    :open="true"
    title="Change card type"
    class="type-change-dialog"
    @close="emit('cancel')"
    @submit="emit('confirm')"
  >
    <template #headerContent>
      <div class="type-change-headings">
        <div><span>Current</span>{{ preview.originalTypeName }}</div>
        <ArrowRight class="type-change-arrow" :size="20" aria-hidden="true" />
        <div><span>New</span>{{ preview.destinationTypeName }}</div>
      </div>
    </template>
    <table class="type-change-table" aria-label="Custom field changes">
      <thead class="type-change-table-headings">
        <tr>
          <th scope="col">Current: {{ preview.originalTypeName }}</th>
          <th scope="col">New: {{ preview.destinationTypeName }}</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="(row, index) in preview.rows" :key="index">
          <td>
            <template v-if="row.original">
              <div class="field-name">{{ row.original.emoji }} {{ row.original.name }}</div>
              <div class="field-type">{{ typeLabel(row.original.dataType) }}</div>
              <div class="field-value" :class="{ 'field-value--empty': row.original.value === null }">{{ formatValue(row.original.value) }}</div>
            </template>
          </td>
          <td>
            <template v-if="row.destination">
              <div class="field-name">{{ row.destination.emoji }} {{ row.destination.name }}</div>
              <div class="field-type">{{ typeLabel(row.destination.dataType) }}</div>
              <div class="field-value" :class="{ 'field-value--empty': row.destination.value === null }">{{ formatValue(row.destination.value) }}</div>
              <div v-if="row.losesValue" class="field-loss field-loss--replacement"><TriangleAlert :size="14" aria-hidden="true" /><span>Original value removed · saved to comments</span></div>
            </template>
            <span v-else-if="row.losesValue" class="field-loss"><TriangleAlert :size="14" aria-hidden="true" /><span>Removed · saved to comments</span></span>
            <span v-else class="field-removed">Removed · empty</span>
          </td>
        </tr>
      </tbody>
    </table>
    <template #actions>
      <div class="type-change-actions">
        <button class="btn" type="submit"><Check :size="18" aria-hidden="true" />Confirm &amp; Save</button>
        <button class="btn btn--secondary" type="button" @click="emit('cancel')"><X :size="18" aria-hidden="true" />Cancel</button>
      </div>
    </template>
  </FixedChromeDialog>
</template>

<script setup lang="ts">
import { ArrowRight, Check, TriangleAlert, X } from '@lucide/vue';
import FixedChromeDialog from '../../shared/components/FixedChromeDialog.vue';
import type { CustomFieldDefinition, CardTypeChangePreview } from '../../shared/types/customFieldTypes';

defineProps<{ preview: CardTypeChangePreview }>();
const emit = defineEmits<{ confirm: []; cancel: [] }>();

function typeLabel(dataType: CustomFieldDefinition['dataType']) {
  switch (dataType) {
    case 'integer': return 'Number';
    case 'date': return 'Date';
    case 'text': return 'Text';
  }
}

function formatValue(value: string | number | null) {
  return value === null ? 'Empty' : String(value);
}
</script>

<style scoped>
.type-change-dialog {
  width: min(52rem, calc(100vw - 2rem));
}
.type-change-table {
  width: 100%;
  table-layout: fixed;
  align-self: start;
  border-collapse: collapse;
}
.type-change-table td {
  width: 50%;
  padding: 0.85rem 1rem;
  text-align: left;
  vertical-align: top;
  overflow-wrap: anywhere;
}
.type-change-headings {
  position: relative;
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
  margin: 0.75rem -2.5rem 0 0;
  font-size: 1.05rem;
  font-weight: 600;
  color: var(--bo-ink-strong);
}
.type-change-headings > div {
  padding: 0 1rem;
  overflow-wrap: anywhere;
}
.type-change-headings span {
  display: block;
  font-weight: normal;
  font-size: 0.8rem;
  color: var(--bo-ink-subtle);
  margin-bottom: 0.25rem;
}
.type-change-arrow {
  position: absolute;
  left: 50%;
  top: 50%;
  transform: translate(-50%, -50%);
  color: var(--bo-ink-subtle);
}
.type-change-table-headings {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip-path: inset(50%);
  white-space: nowrap;
}
.type-change-table tr > :first-child {
  border-right: 1px solid var(--bo-border-soft);
}
.type-change-table td {
  border-top: 1px solid var(--bo-border-soft);
}
.field-name { font-weight: 600; }
.field-type {
  color: var(--bo-ink-subtle);
  font-size: 0.8rem;
  margin: 0.15rem 0 0.45rem;
}
.field-value--empty,
.field-removed { color: var(--bo-ink-subtle); font-style: italic; }
.field-loss {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  padding: 0.35rem 0.5rem;
  border: 1px solid var(--bo-toast-warning-border);
  border-radius: 6px;
  background: var(--bo-toast-warning-bg);
  color: var(--bo-toast-warning-text);
  font-size: 0.8rem;
}
.field-loss--replacement { margin-top: 0.5rem; }
.field-loss svg { flex-shrink: 0; }
.type-change-actions { display: flex; justify-content: flex-end; gap: 0.5rem; }
@media (max-width: 720px) {
  .type-change-dialog { width: 100vw; }
}
@media (max-width: 560px) {
  .type-change-headings > div,
  .type-change-table td { padding-left: 0.5rem; padding-right: 0.5rem; }
}
</style>
