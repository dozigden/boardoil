<template>
  <FormDialog v-if="form" :definition="form" @close="close" @saved="close" />
</template>

<script setup lang="ts">
import { useRoute, useRouter } from 'vue-router';
import FormDialog from '../../shared/forms/FormDialog.vue';
import { createColumnForm } from '../forms/columnForm';
import { useBoardStore } from '../stores/boardStore';

const route = useRoute();
const router = useRouter();
const store = useBoardStore();
const boardId = store.currentBoardId!;
const column = store.getColumnById(Number(route.params.columnId));
const form = column ? createColumnForm(column, {
  save: values => store.saveColumn(column.id, values),
  delete: () => store.deleteColumn(column.id),
  canEdit: () => store.isCurrentUserOwner,
  isCurrent: () => store.currentBoardId === boardId
}) : null;
if (!form) void router.replace({ name: 'columns', params: { boardId } });

async function close() {
  await router.push({ name: 'columns', params: { boardId } });
}
</script>
