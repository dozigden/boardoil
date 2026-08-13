<template>
  <header :class="rootClasses">
    <div class="board-main-controls">
      <div class="board-search-pane">
        <label class="board-search-field">
          <input
            :value="searchText"
            type="search"
            placeholder="Search"
            maxlength="200"
            @input="emit('update:searchText', ($event.target as HTMLInputElement).value)"
          />
        </label>
      </div>

      <div class="board-controls-pane">
        <BoardCardFilterPicker
          v-if="availableTagNames.length > 0 || availableSlicks.length > 0 || availableCardTypes.length > 0"
          :available-tag-names="availableTagNames"
          :available-slicks="availableSlicks"
          :available-card-types="availableCardTypes"
          :tag-filter-states="tagFilterStates"
          :slick-filter-states="slickFilterStates"
          :card-type-filter-states="cardTypeFilterStates"
          :has-active-filters="hasActiveOptionFilters"
          :open="pickerOpen"
          @update:tag-filter-states="emit('update:tagFilterStates', $event)"
          @update:slick-filter-states="emit('update:slickFilterStates', $event)"
          @update:card-type-filter-states="emit('update:cardTypeFilterStates', $event)"
          @update:open="emit('update:pickerOpen', $event)"
        />

        <div class="board-filters-summary">
          <button
            type="button"
            class="btn btn--secondary board-clear-filters"
            aria-label="Clear card filters"
            title="Clear filters"
            :disabled="!hasActiveFilters"
            @click="emit('clear')"
          >
            <X :size="16" aria-hidden="true" />
            <span class="board-clear-filters-label">Clear</span>
          </button>
        </div>
      </div>
    </div>

    <div v-if="showSelectionToggle" class="board-selection-pane">
      <div v-if="selectionMode" class="btn-group board-selection-edit-group">
        <button
          type="button"
          class="btn board-selection-edit-main"
          :class="{ 'btn--secondary': selectedCount === 0 }"
          :disabled="disableBulkEditAction"
          @click="emit('openBulkEdit')"
        >
          Edit
        </button>
        <BoDropdown
          label="More selected card actions"
          :icon="ChevronDown"
          :icon-size="14"
          icon-only
          align="right"
          button-class="board-selection-edit-caret"
          :disabled="disableSelectionMenuAction"
        >
          <template #default="{ close }">
            <button type="button" class="bo-dropdown-item" @click="openInvertSelectionFromMenu(close)">
              <span class="bo-dropdown-item-main">Invert selection</span>
            </button>
            <span class="bo-dropdown-divider" aria-hidden="true"></span>
            <button type="button" class="bo-dropdown-item" :disabled="selectedCount === 0" @click="openBulkDeleteFromMenu(close)">
              <span class="bo-dropdown-item-main board-selection-delete-item">Delete selected</span>
            </button>
          </template>
        </BoDropdown>
      </div>
      <label
        class="board-selection-toggle"
        :title="selectionMode ? 'Done selecting cards' : 'Select cards'"
      >
        <input
          type="checkbox"
          class="board-selection-toggle-input"
          :checked="selectionMode"
          aria-label="Toggle card selection mode"
          @change="emit('toggleSelectionMode')"
        />
        <span class="board-selection-toggle-switch" aria-hidden="true" />
        <span class="board-selection-toggle-label">
          Select
        </span>
        <span v-if="selectionMode && selectedCount > 0" class="board-selection-toggle-count">
          {{ selectedCount }}
        </span>
      </label>
    </div>
  </header>
</template>

<script setup lang="ts">
import { ChevronDown, X } from 'lucide-vue-next';
import { computed } from 'vue';
import BoDropdown from '../../shared/components/BoDropdown.vue';
import type { CardType, Slick } from '../../shared/types/boardTypes';
import type { TagFilterStateMap } from '../../shared/types/tagFilterTypes';
import BoardCardFilterPicker from './BoardCardFilterPicker.vue';

const props = withDefaults(defineProps<{
  searchText: string;
  availableTagNames: string[];
  availableSlicks?: Slick[];
  availableCardTypes?: CardType[];
  tagFilterStates: TagFilterStateMap;
  slickFilterStates?: TagFilterStateMap;
  cardTypeFilterStates?: TagFilterStateMap;
  pickerOpen: boolean;
  hasActiveFilters: boolean;
  selectionMode?: boolean;
  selectedCount?: number;
  disableBulkEditAction?: boolean;
  disableSelectionMenuAction?: boolean;
  showSelectionToggle?: boolean;
  embedded?: boolean;
}>(), {
  selectionMode: false,
  selectedCount: 0,
  disableBulkEditAction: false,
  disableSelectionMenuAction: false,
  showSelectionToggle: true,
  embedded: false,
  availableSlicks: () => [],
  availableCardTypes: () => [],
  slickFilterStates: () => ({}),
  cardTypeFilterStates: () => ({})
});

const emit = defineEmits<{
  'update:searchText': [value: string];
  'update:tagFilterStates': [value: TagFilterStateMap];
  'update:slickFilterStates': [value: TagFilterStateMap];
  'update:cardTypeFilterStates': [value: TagFilterStateMap];
  'update:pickerOpen': [value: boolean];
  clear: [];
  toggleSelectionMode: [];
  openBulkEdit: [];
  openBulkDelete: [];
  invertSelection: [];
}>();

function openBulkDeleteFromMenu(close: () => void) {
  close();
  emit('openBulkDelete');
}

function openInvertSelectionFromMenu(close: () => void) {
  close();
  emit('invertSelection');
}

const rootClasses = computed(() => (
  props.embedded
    ? ['board-filters', 'board-filters--embedded']
    : ['panel', 'panel--compact', 'board-filters']
));

const hasActiveOptionFilters = computed(() =>
  Object.keys(props.tagFilterStates).length > 0
  || Object.keys(props.slickFilterStates).length > 0
  || Object.keys(props.cardTypeFilterStates).length > 0
);
</script>

<style scoped>
.board-filters {
  --bo-board-filter-control-height: 2.3rem;
  margin-top: 0;
  width: 100%;
  display: flex;
  align-items: center;
  gap: 0.6rem;
}

.board-filters:not(.board-filters--embedded) {
  margin-inline: 1.5rem;
}

.board-filters--embedded {
  margin-inline: 0;
  padding: 0.45rem 0.6rem;
}

.board-main-controls {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  min-width: 0;
  flex: 1 1 auto;
}

.board-search-pane {
  display: flex;
  align-items: center;
  width: min(100%, 22rem);
  flex: 0 1 22rem;
}

.board-search-field {
  flex: 0 1 auto;
  width: 100%;
  min-width: 0;
}

.board-controls-pane {
  display: flex;
  align-items: center;
  justify-content: flex-start;
  gap: 0.6rem;
  flex: 0 1 auto;
}

.board-filters-summary {
  display: flex;
  align-items: center;
  justify-content: flex-start;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.board-clear-filters {
  min-height: var(--bo-board-filter-control-height);
  padding: 0 0.65rem;
}

.board-selection-toggle {
  min-height: var(--bo-board-filter-control-height);
  padding: 0 0.3rem;
  display: inline-flex;
  align-items: center;
  gap: 0.45rem;
  cursor: pointer;
  user-select: none;
}

.board-selection-toggle-input {
  position: absolute;
  width: 1px;
  height: 1px;
  opacity: 0;
  pointer-events: none;
}

.board-selection-toggle-switch {
  position: relative;
  width: 2.2rem;
  height: 1.25rem;
  border-radius: 999px;
  background: color-mix(in srgb, var(--bo-border-default) 85%, transparent);
  border: 1px solid var(--bo-border-default);
  transition: background-color 140ms ease, border-color 140ms ease;
}

.board-selection-toggle-switch::after {
  content: '';
  position: absolute;
  top: 1px;
  left: 1px;
  width: calc(1.25rem - 4px);
  height: calc(1.25rem - 4px);
  border-radius: 50%;
  background: var(--bo-surface-panel);
  box-shadow: 0 1px 2px color-mix(in srgb, var(--bo-mix-dark) 20%, transparent);
  transition: transform 140ms ease;
}

.board-selection-toggle-input:checked + .board-selection-toggle-switch {
  background: color-mix(in srgb, var(--bo-colour-brand) 84%, var(--bo-colour-brand-strong));
  border-color: var(--bo-colour-brand);
}

.board-selection-toggle-input:checked + .board-selection-toggle-switch::after {
  transform: translateX(0.95rem);
}

.board-selection-toggle:focus-within {
  outline: 2px solid var(--bo-focus-ring);
  outline-offset: 2px;
  border-radius: 10px;
}

.board-selection-toggle-input:checked ~ .board-selection-toggle-label {
  color: var(--bo-selection-accent);
}

.board-selection-toggle-label {
  font-size: 0.9rem;
  font-weight: 600;
  color: var(--bo-text-default, var(--bo-ink-default));
}

.board-selection-pane {
  margin-left: auto;
  flex: 0 0 auto;
  display: inline-flex;
  align-items: center;
  gap: 0.45rem;
}

.board-selection-edit-group {
  min-height: var(--bo-board-filter-control-height);
}

.board-selection-edit-main {
  min-height: var(--bo-board-filter-control-height);
  padding: 0 0.65rem;
}

:deep(.board-selection-edit-caret) {
  --bo-btn-bg: var(--bo-colour-brand);
  --bo-btn-border: var(--bo-colour-brand);
  --bo-btn-ink: var(--bo-ink-on-brand);
  --bo-btn-bg-hover: var(--bo-colour-brand-strong);
  --bo-btn-border-hover: var(--bo-colour-brand-strong);
  --bo-btn-ink-hover: var(--bo-ink-on-brand);
  min-height: var(--bo-board-filter-control-height);
  min-width: 2rem;
  padding: 0;
  line-height: 1;
}

.board-selection-delete-item {
  color: var(--bo-colour-danger-strong);
}

.board-selection-toggle-count {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 1.25rem;
  height: 1.25rem;
  padding: 0 0.34rem;
  border-radius: 999px;
  background: var(--bo-surface-brand);
  border: 1px solid var(--bo-border-brand);
  color: var(--bo-colour-brand-strong);
  font-size: 0.76rem;
  line-height: 1;
}

.board-search-field input {
  min-height: var(--bo-board-filter-control-height);
}

@media (max-width: 767px) {
  .board-filters:not(.board-filters--embedded) {
    margin-inline: 0;
    gap: 0.5rem;
    align-items: center;
    border-top: none;
    border-left: none;
    border-right: none;
    border-radius: 0 0 10px 10px;
    padding: 0.5rem 0.75rem;
  }

  .board-filters--embedded {
    padding: 0.1rem 0.4rem 0.12rem;
    gap: 0.35rem;
  }

  .board-main-controls {
    gap: 0.35rem;
  }

  .board-search-pane {
    width: 100%;
    flex-basis: auto;
  }

  .board-controls-pane {
    align-items: center;
    justify-content: flex-start;
    gap: 0.35rem;
    min-width: 0;
    flex-wrap: nowrap;
  }

  .board-filters-summary {
    flex: 0 0 auto;
  }

  .board-clear-filters {
    padding: 0 0.5rem;
  }

  .board-selection-pane {
    padding-left: 0.3rem;
  }

  .board-selection-toggle-label {
    display: none;
  }

}
</style>
