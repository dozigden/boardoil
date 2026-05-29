<template>
  <ModalDialog :open="editingColumn !== null" title="Edit Column" close-label="Cancel editing" @close="closeColumnEditor" @submit="saveColumn">
    <template v-if="editingColumn">
      <label>
        Title
        <input
          v-model="columnDraft.title"
          maxlength="200"
          autocomplete="off"
          data-lpignore="true"
        />
      </label>
    </template>
    <template #actions>
      <div v-if="editingColumn" class="editor-actions card-modal-actions">
        <button type="button" class="btn btn--danger" aria-label="Delete column" title="Delete column" @click="deleteEditingColumn">
          <Trash2 :size="16" aria-hidden="true" />
        </button>
        <div class="card-modal-actions-left">
          <button type="submit" class="btn" aria-label="Save column" title="Save column">
            <Check :size="16" aria-hidden="true" />
            <span>Save</span>
          </button>
          <button type="button" class="btn btn--secondary" aria-label="Cancel editing" title="Cancel" @click="closeColumnEditor">
            <X :size="16" aria-hidden="true" />
            <span>Cancel</span>
          </button>
        </div>
      </div>
    </template>
  </ModalDialog>
</template>

<script setup lang="ts">
import { Check, Trash2, X } from 'lucide-vue-next';
import { storeToRefs } from 'pinia';
import { computed, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import ModalDialog from '../../shared/components/ModalDialog.vue';
import type { ColumnEditModel } from '../../shared/types/boardTypes';
import { useBoardStore } from '../stores/boardStore';

const route = useRoute();
const router = useRouter();
const boardStore = useBoardStore();
const { board } = storeToRefs(boardStore);
const { saveColumn: saveColumnAction, deleteColumn } = boardStore;
const columnDraft = ref<ColumnEditModel>({
  title: ''
});

const routeColumnId = computed<number | null>(() => {
  const raw = route.params.columnId;
  const parsed = typeof raw === 'string' ? Number.parseInt(raw, 10) : Number.NaN;
  return Number.isFinite(parsed) ? parsed : null;
});

const routeBoardId = computed<number | null>(() => {
  const raw = route.params.boardId;
  const parsed = typeof raw === 'string' ? Number.parseInt(raw, 10) : Number.NaN;
  return Number.isFinite(parsed) ? parsed : null;
});

const editingColumn = computed(() => boardStore.getColumnById(routeColumnId.value));

async function closeColumnEditor() {
  const boardId = routeBoardId.value;
  if (boardId === null) {
    await router.push({ name: 'boards' });
    return;
  }

  await router.push({ name: 'columns', params: { boardId } });
}

async function saveColumn() {
  const columnId = routeColumnId.value;
  if (columnId === null || !columnDraft.value.title.trim()) {
    return;
  }

  const saveModel: ColumnEditModel = {
    title: columnDraft.value.title.trim()
  };
  await saveColumnAction(columnId, saveModel);
  await closeColumnEditor();
}

async function deleteEditingColumn() {
  const columnId = routeColumnId.value;
  if (columnId === null) {
    return;
  }

  await deleteColumn(columnId);
  await closeColumnEditor();
}

watch(
  [routeBoardId, routeColumnId, editingColumn, board],
  ([nextBoardId, nextColumnId, nextColumn, nextBoard], [, previousColumnId]) => {
    if (nextBoardId === null) {
      void router.replace({ name: 'boards' });
      return;
    }

    if (nextColumnId === null) {
      void router.replace({ name: 'columns', params: { boardId: nextBoardId } });
      return;
    }

    if (!nextBoard) {
      return;
    }

    if (!nextColumn) {
      void router.replace({ name: 'columns', params: { boardId: nextBoardId } });
      return;
    }

    if (previousColumnId !== nextColumnId) {
      columnDraft.value = {
        title: nextColumn.title
      };
    }
  },
  { immediate: true }
);
</script>
