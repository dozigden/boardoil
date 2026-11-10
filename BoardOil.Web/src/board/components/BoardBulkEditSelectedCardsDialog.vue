<template>
  <FormDialog :definition="form" @close="emit('close')" @saved="emit('saved')">
    <template #before-fields>
      <p class="bulk-edit-summary">Edit {{ selectedCardIds.length }} selected card{{ selectedCardIds.length === 1 ? '' : 's' }}.</p>
    </template>
  </FormDialog>
</template>

<script setup lang="ts">
import FormDialog from '../../shared/forms/FormDialog.vue';
import { createBulkEditForm } from '../forms/bulkEditForm';
import { useCardStore } from '../stores/cardStore';
import { useTagStore } from '../stores/tagStore';

const props = defineProps<{
  boardId: number;
  selectedCardIds: number[];
  columns: Array<{ id: number; title: string }>;
  slicks: Array<{ name: string }>;
}>();
const emit = defineEmits<{ close: []; saved: []; 'update:busy': [value: boolean] }>();
const cards = useCardStore();
const tags = useTagStore();
const targetBoardId = props.boardId;
const form = createBulkEditForm({
  columns: () => props.columns,
  slicks: () => props.slicks,
  tagNames: () => tags.tags.map(tag => tag.name),
  selectedCount: () => props.selectedCardIds.length,
  isCurrent: () => cards.activeBoardId === targetBoardId && props.boardId === targetBoardId,
  save: async options => {
    emit('update:busy', true);
    try {
      return await cards.bulkEditCards([...props.selectedCardIds], options);
    } finally {
      emit('update:busy', false);
    }
  }
});
</script>

<style scoped>
.bulk-edit-summary { margin: 0 0 0.75rem; color: var(--bo-ink); }
</style>
