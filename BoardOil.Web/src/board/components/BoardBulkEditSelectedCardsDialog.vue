<template>
  <FixedChromeDialog
    :open="open"
    title="Bulk Edit Selected Cards"
    close-label="Close bulk edit"
    @close="emit('close')"
  >
    <div class="bulk-edit-body">
      <p class="bulk-edit-summary">
        Edit {{ selectedCount }} selected card{{ selectedCount === 1 ? '' : 's' }}.
      </p>

      <section class="bulk-edit-field">
        <label for="bulk-edit-column" class="bulk-edit-field-label">Column</label>
        <select
          id="bulk-edit-column"
          :value="targetColumnValue"
          class="bulk-edit-column-select"
          :disabled="isSaving"
          @change="onColumnChange"
        >
          <option value="">No change</option>
          <option v-for="column in columns" :key="column.id" :value="String(column.id)">
            {{ column.title }}
          </option>
        </select>
      </section>

      <section class="bulk-edit-field">
        <label for="bulk-edit-slick-operation" class="bulk-edit-field-label">Slick</label>
        <select
          id="bulk-edit-slick-operation"
          :value="slickOperation"
          class="bulk-edit-slick-operation-select"
          :disabled="isSaving"
          @change="onSlickOperationChange"
        >
          <option value="none">No change</option>
          <option value="clear">No slick</option>
          <option value="set">Set slick</option>
        </select>
      </section>

      <section v-if="slickOperation === 'set'" class="bulk-edit-field">
        <label for="bulk-edit-slick-name" class="bulk-edit-field-label">Slick to set</label>
        <select
          id="bulk-edit-slick-name"
          :value="targetSlickNameValue"
          class="bulk-edit-slick-name-select"
          :disabled="isSaving"
          @change="onSlickNameChange"
        >
          <option value="">Select slick</option>
          <option v-for="slick in slicks" :key="slick.id" :value="slick.name">
            {{ slick.name }}
          </option>
        </select>
      </section>

      <TagTriStateMatrix
        v-if="availableTagNames.length > 0"
        class="bulk-edit-tags"
        :available-tag-names="availableTagNames"
        :states="filterStates"
        :labels="{ left: 'Remove', middle: 'No change', right: 'Add' }"
        :ariaLabel="'Tag bulk edit matrix'"
        left-action-prefix="Mark for remove"
        middle-action-prefix="Mark unchanged"
        right-action-prefix="Mark for add"
        :disabled="isSaving"
        :fluid="true"
        :show-directional-cursor="false"
        :enable-bounce="true"
        @update:states="onFilterStatesChange"
      />
    </div>

    <template #actions>
      <section class="fixed-chrome-dialog-actions">
        <div class="fixed-chrome-dialog-actions-left">
          <button type="button" class="btn btn--secondary" :disabled="isSaving" @click="emit('close')">
            Cancel
          </button>
        </div>
        <button type="button" class="btn" :disabled="isSaving || selectedCount === 0 || !hasChanges" @click="emit('confirm')">
          {{ isSaving ? 'Applying...' : 'Apply edits' }}
        </button>
      </section>
    </template>
  </FixedChromeDialog>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import FixedChromeDialog from '../../shared/components/FixedChromeDialog.vue';
import type { BulkEditSlickOperation } from '../../shared/types/bulkEditTypes';
import type { TagFilterStateMap } from '../../shared/types/tagFilterTypes';
import TagTriStateMatrix from './TagTriStateMatrix.vue';

const props = defineProps<{
  open: boolean;
  selectedCount: number;
  isSaving: boolean;
  availableTagNames: string[];
  columns: Array<{ id: number; title: string }>;
  filterStates: TagFilterStateMap;
  targetColumnId: number | null;
  slicks: Array<{ id: number; name: string }>;
  slickOperation: BulkEditSlickOperation;
  targetSlickName: string | null;
  hasChanges: boolean;
}>();

const emit = defineEmits<{
  close: [];
  confirm: [];
  'update:filterStates': [value: TagFilterStateMap];
  'update:targetColumnId': [value: number | null];
  'update:slickOperation': [value: BulkEditSlickOperation];
  'update:targetSlickName': [value: string | null];
}>();

const targetColumnValue = computed(() => props.targetColumnId === null ? '' : String(props.targetColumnId));
const targetSlickNameValue = computed(() => props.targetSlickName ?? '');

function onFilterStatesChange(value: TagFilterStateMap) {
  emit('update:filterStates', value);
}

function onColumnChange(event: Event) {
  const value = (event.target as HTMLSelectElement).value;
  if (!value) {
    emit('update:targetColumnId', null);
    return;
  }

  const parsed = Number.parseInt(value, 10);
  emit('update:targetColumnId', Number.isFinite(parsed) ? parsed : null);
}

function onSlickOperationChange(event: Event) {
  const value = (event.target as HTMLSelectElement).value;
  if (value !== 'none' && value !== 'clear' && value !== 'set') {
    return;
  }

  emit('update:slickOperation', value);
  if (value !== 'set') {
    emit('update:targetSlickName', null);
  }
}

function onSlickNameChange(event: Event) {
  const value = (event.target as HTMLSelectElement).value;
  if (value.length === 0) {
    emit('update:targetSlickName', null);
    return;
  }

  emit('update:targetSlickName', value);
}
</script>

<style scoped>
.bulk-edit-summary {
  margin: 0 0 0.75rem;
  color: var(--bo-ink);
}

.bulk-edit-field {
  margin-bottom: 0.8rem;
  display: grid;
  gap: 0.3rem;
}

.bulk-edit-field-label {
  font-size: 0.85rem;
  color: var(--bo-ink-muted);
}

.bulk-edit-column-select {
  min-height: 2.1rem;
}

.bulk-edit-slick-operation-select,
.bulk-edit-slick-name-select {
  min-height: 2.1rem;
}

.bulk-edit-tags {
  max-height: min(46vh, 360px);
  margin-bottom: 0.1rem;
}
</style>
