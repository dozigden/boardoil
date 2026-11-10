<template>
  <FormDialog :definition="form" @close="emit('close')" @saved="saved" />
</template>

<script setup lang="ts">
import FormDialog from '../../shared/forms/FormDialog.vue';
import { useBoardCatalogueStore } from '../../shared/stores/boardCatalogueStore';
import type { BoardSummary } from '../../shared/types/boardTypes';
import { createBoardForm } from '../forms/boardCreateForm';

const emit = defineEmits<{ close: []; saved: [value: BoardSummary] }>();
const catalogue = useBoardCatalogueStore();
const form = createBoardForm({
  boards: () => catalogue.boards,
  create: catalogue.createBoard,
  clone: catalogue.cloneBoard,
  import: catalogue.importBoardPackage
});
function saved(board: BoardSummary) {
  emit('saved', board);
}
</script>
