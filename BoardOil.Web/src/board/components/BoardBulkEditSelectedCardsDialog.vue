<template>
  <ModalDialog
    :open="open"
    title="Bulk Edit Selected Cards"
    close-label="Close bulk edit"
    @close="emit('close')"
  >
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
      @update:states="emit('update:filterStates', $event)"
    />

    <section class="card-modal-actions">
      <div class="card-modal-actions-left">
        <button type="button" class="btn btn--secondary" :disabled="isSaving" @click="emit('close')">
          Cancel
        </button>
      </div>
      <button type="button" class="btn" :disabled="isSaving || selectedCount === 0 || !hasChanges" @click="emit('confirm')">
        {{ isSaving ? 'Applying...' : 'Apply edits' }}
      </button>
    </section>
  </ModalDialog>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import ModalDialog from '../../shared/components/ModalDialog.vue';
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
  hasChanges: boolean;
}>();

const emit = defineEmits<{
  close: [];
  confirm: [];
  'update:filterStates': [value: TagFilterStateMap];
  'update:targetColumnId': [value: number | null];
}>();

const targetColumnValue = computed(() => props.targetColumnId === null ? '' : String(props.targetColumnId));

function onColumnChange(event: Event) {
  const value = (event.target as HTMLSelectElement).value;
  if (!value) {
    emit('update:targetColumnId', null);
    return;
  }

  const parsed = Number.parseInt(value, 10);
  emit('update:targetColumnId', Number.isFinite(parsed) ? parsed : null);
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

.bulk-edit-tags {
  max-height: min(46vh, 360px);
  margin-bottom: 0.1rem;
}
</style>
