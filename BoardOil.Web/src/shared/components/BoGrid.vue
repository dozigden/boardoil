<template>
  <div class="bo-grid-shell">
    <div class="bo-grid-viewport" :class="{ 'bo-grid-viewport--sticky': hasStickyHeader }" :style="stickyViewportStyle">
      <div class="bo-grid" :class="{ 'bo-grid--sticky-header': hasStickyHeader }" :style="gridStyle" role="table">
        <div class="bo-grid-head" role="rowgroup">
          <div class="bo-grid-head-row bo-grid-grid-row" role="row">
            <div
              v-for="column in columns"
              :key="column.key"
              role="columnheader"
              :class="['bo-grid-head-cell', 'bo-grid-cell', getCellClass(column)]"
            >
              <span>{{ column.label ?? column.key }}</span>
            </div>
          </div>
        </div>

        <div class="bo-grid-body" role="rowgroup">
          <div v-if="isLoading" class="bo-grid-row bo-grid-grid-row" role="row">
            <div class="bo-grid-status bo-grid-cell" role="cell">Loading...</div>
          </div>

          <div v-else-if="items.length === 0" class="bo-grid-row bo-grid-grid-row" role="row">
            <div class="bo-grid-status bo-grid-cell" role="cell">
              <div class="bo-grid-empty-copy">{{ emptyText }}</div>
            </div>
          </div>

          <template v-else>
            <div
              v-for="item in itemsForDisplay"
              :key="String(item.key)"
              class="bo-grid-row bo-grid-grid-row"
              :class="{ 'bo-grid-row--clickable': rowClickable }"
              :tabindex="rowClickable ? 0 : undefined"
              role="row"
              @click="onRowClicked(item.data, $event)"
              @keydown.enter.prevent="onRowClicked(item.data, $event)"
              @keydown.space.prevent="onRowClicked(item.data, $event)"
            >
              <div
                v-for="column in columns"
                :key="`${String(item.key)}-${column.key}`"
                role="cell"
                :class="['bo-grid-cell', getCellClass(column)]"
                :data-label="column.label ?? column.key"
              >
                <div class="bo-grid-cell-value">
                  <slot
                    :name="`cell(${column.key})`"
                    :value="item.data[column.key]"
                    :row="item.data"
                  />
                </div>
              </div>
            </div>
          </template>
        </div>
      </div>
    </div>

    <footer class="bo-grid-bottom-bar">
      <p class="bo-grid-pagination-summary">
        <template v-if="paginationTotalCount > 0">
          Showing {{ paginationRangeStart }}-{{ paginationRangeEnd }} of {{ paginationTotalCount }}
        </template>
      </p>
      <div class="bo-grid-pagination-controls">
        <button type="button" class="btn btn--secondary" :disabled="!canGoPrevious" @click="onPreviousPage">Previous</button>
        <button type="button" class="btn btn--secondary" :disabled="!canGoNext" @click="onNextPage">Next</button>
      </div>
    </footer>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';

type GridColumn = {
  key: string;
  label?: string;
  width?: string;
  rowKeyColumn?: boolean;
  align?: 'start' | 'end';
};

type RowRecord = Record<string, unknown>;
type RowKey = string | number;

type ItemForDisplay = {
  key: RowKey;
  data: RowRecord;
};

const props = withDefaults(defineProps<{
  columns: GridColumn[];
  items: RowRecord[];
  isLoading?: boolean;
  stickyHeader?: string | null;
  emptyText?: string;
  rowClickable?: boolean;
  totalCount: number;
  offset: number;
  limit: number;
}>(), {
  isLoading: false,
  stickyHeader: null,
  emptyText: 'No results found.',
  rowClickable: false
});

const emit = defineEmits<{
  'row-clicked': [row: RowRecord];
  'previous-page': [];
  'next-page': [];
}>();

const rowKeyField = computed(() => {
  const rowKeyColumn = props.columns.find(c => c.rowKeyColumn === true);
  if (!rowKeyColumn) {
    throw new Error('BoGrid requires one column with rowKeyColumn=true.');
  }

  return rowKeyColumn.key;
});
const hasStickyHeader = computed(() => props.stickyHeader !== null && props.stickyHeader !== '');
const stickyViewportStyle = computed(() => {
  if (!hasStickyHeader.value) {
    return undefined;
  }

  return { maxHeight: props.stickyHeader ?? '300px' };
});
const gridTemplateColumns = computed(() => {
  if (props.columns.length === 0) {
    return 'minmax(0, 1fr)';
  }

  return props.columns
    .map(column => {
      if (typeof column.width === 'string' && column.width.trim().length > 0) {
        return column.width;
      }

      return 'minmax(0, 1fr)';
    })
    .join(' ');
});
const gridStyle = computed(() => ({
  '--bo-grid-grid-template-columns': gridTemplateColumns.value
}));
const itemsForDisplay = computed<ItemForDisplay[]>(() => {
  return props.items.map((row, index) => {
    return {
      key: getRowKey(row, index),
      data: row
    };
  });
});
const paginationTotalCount = computed(() => Math.max(0, props.totalCount));
const paginationOffset = computed(() => Math.max(0, props.offset));
const paginationRangeStart = computed(() => {
  const totalCount = paginationTotalCount.value;
  if (totalCount === 0) {
    return 0;
  }

  const clampedOffset = Math.min(paginationOffset.value, totalCount - 1);
  return clampedOffset + 1;
});
const paginationRangeEnd = computed(() => {
  const totalCount = paginationTotalCount.value;
  if (totalCount === 0) {
    return 0;
  }

  return Math.min(Math.max(paginationOffset.value + props.items.length, 0), totalCount);
});
const canGoPrevious = computed(() => paginationOffset.value > 0);
const canGoNext = computed(() =>
  paginationOffset.value + props.items.length < paginationTotalCount.value
);

function onRowClicked(row: RowRecord, event: MouseEvent | KeyboardEvent) {
  if (!props.rowClickable) {
    return;
  }

  const target = event.target;
  if (target instanceof Element && target.closest('button, a, input, select, textarea, label, [role="button"], [data-prevent-row-click="true"]')) {
    return;
  }

  emit('row-clicked', row);
}

function onPreviousPage() {
  if (!canGoPrevious.value) {
    return;
  }

  emit('previous-page');
}

function onNextPage() {
  if (!canGoNext.value) {
    return;
  }

  emit('next-page');
}

function getRowKey(row: RowRecord, index: number): RowKey {
  const rowKeyValue = row[rowKeyField.value];
  if (typeof rowKeyValue === 'string' || typeof rowKeyValue === 'number') {
    return rowKeyValue;
  }

  throw new Error(`Row at index ${index} is missing a valid key value in the "${rowKeyField.value}" field.`);
}

function getCellClass(column: GridColumn) {
  return {
    'bo-grid-cell--align-end': column.align === 'end'
  };
}
</script>

<style scoped>
.bo-grid-shell {
  --bo-grid-cell-padding: 0.6rem 0.7rem;
  --bo-grid-mobile-cell-padding: 0.45rem 0.6rem;
  --bo-grid-footer-padding: 0.45rem 0.6rem;
  --bo-grid-row-divider-width: 1px;
  --bo-grid-mobile-row-divider-width: 2px;
  width: 100%;
  border: 1px solid var(--bo-border-soft);
  border-radius: 12px;
  background: var(--bo-surface-panel);
  overflow: hidden;
  display: flex;
  flex-direction: column;
  min-height: 0;
  height: 100%;
}

.bo-grid-viewport {
  width: 100%;
  min-height: 0;
  flex: 1 1 auto;
}

.bo-grid-viewport--sticky {
  overflow: auto;
}

.bo-grid {
  width: 100%;
  --bo-grid-grid-template-columns: minmax(0, 1fr);
}

.bo-grid-grid-row {
  display: grid;
  grid-template-columns: var(--bo-grid-grid-template-columns);
}

.bo-grid-head-cell {
  background: var(--bo-surface-muted);
  color: var(--bo-ink-strong);
  font-size: 0.86rem;
  font-weight: 700;
}

.bo-grid-head-cell:first-child {
  border-top-left-radius: 11px;
}

.bo-grid-head-cell:last-child {
  border-top-right-radius: 11px;
}

.bo-grid-cell {
  padding: var(--bo-grid-cell-padding);
  border-bottom: var(--bo-grid-row-divider-width) solid var(--bo-border-soft);
  text-align: left;
  min-width: 0;
}

.bo-grid-body .bo-grid-row:last-child .bo-grid-cell {
  border-bottom: none;
}

.bo-grid-bottom-bar {
  border-top: 1px solid var(--bo-border-soft);
  background: var(--bo-surface-muted);
  padding: var(--bo-grid-footer-padding);
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
  position: sticky;
  bottom: 0;
  z-index: 2;
}

.bo-grid-pagination-summary {
  margin: 0;
  color: var(--bo-ink-muted);
  min-width: 0;
}

.bo-grid-pagination-controls {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  flex: 0 0 auto;
}

.bo-grid-row {
  transition: background-color 100ms ease-in;
}

.bo-grid-row--clickable {
  cursor: pointer;
}

.bo-grid-row--clickable:hover {
  background: var(--bo-surface-energy);
}

.bo-grid-row--clickable:focus-visible {
  outline: 2px solid var(--bo-focus-ring);
  outline-offset: -2px;
}

.bo-grid-head-cell.bo-grid-cell--align-end,
.bo-grid-cell--align-end {
  text-align: right;
}

.bo-grid-status {
  grid-column: 1 / -1;
  text-align: center;
  color: var(--bo-ink-muted);
}

.bo-grid-empty-copy {
  padding: 0.6rem 0.2rem;
}

.bo-grid--sticky-header .bo-grid-head {
  position: sticky;
  top: 0;
  z-index: 3;
}

.bo-grid-cell-value {
  min-width: 0;
}

@media (max-width: 767px) {
  .bo-grid-row,
  .bo-grid-cell {
    display: block;
    width: 100%;
  }

  .bo-grid-cell,
  .bo-grid-cell--align-end,
  .bo-grid-cell-value {
    text-align: left;
  }

  .bo-grid-head {
    display: none;
  }

  .bo-grid-body .bo-grid-row + .bo-grid-row {
    border-top: var(--bo-grid-mobile-row-divider-width) solid var(--bo-border-soft);
  }

  .bo-grid-cell {
    border-bottom: none;
    padding: var(--bo-grid-mobile-cell-padding);
  }

  .bo-grid-cell::before {
    content: attr(data-label);
    float: left;
    width: 40%;
    padding-left: 0.5rem;
    padding-bottom: 0.25rem;
    overflow-wrap: break-word;
    font-weight: 700;
    color: var(--bo-ink-muted);
    padding-right: 0.5rem;
  }

  .bo-grid-cell-value {
    display: inline-block;
    width: 60%;
    padding-left: 0.5rem;
  }

  .bo-grid-status::before {
    content: none;
  }

  .bo-grid-status .bo-grid-cell-value,
  .bo-grid-status .bo-grid-empty-copy {
    width: 100%;
    padding-left: 0;
  }

  .bo-grid-bottom-bar {
    flex-direction: column;
    align-items: stretch;
  }

  .bo-grid-pagination-controls {
    justify-content: flex-start;
  }
}
</style>
