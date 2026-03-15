<template>
  <template v-if="board">
    <section class="toolbar">
      <form class="create-column" @submit.prevent="createColumn">
        <input
          :value="newColumnTitle"
          type="text"
          maxlength="200"
          placeholder="New column title"
          @input="updateNewColumnTitle(($event.target as HTMLInputElement).value)"
        />
        <button type="submit" :disabled="busy">Add Column</button>
      </form>
    </section>

    <section class="column-manager">
      <article
        v-for="column in board.columns"
        :key="column.id"
        class="column-manager-item draggable-column"
        :class="{ 'drag-over': dragOverColumnId === column.id }"
        @dragover="onColumnDragOver(column.id, $event)"
        @dragleave="onColumnDragLeave(column.id)"
        @drop="onColumnDrop(column.id, $event)"
      >
        <div class="column-manager-header">
          <button
            type="button"
            class="ghost column-drag-handle"
            aria-label="Drag to reorder column"
            title="Drag to reorder"
            draggable="true"
            @dragstart="onColumnDragStart(column.id, $event)"
            @dragend="onColumnDragEnd"
          >
            <span aria-hidden="true">::</span>
          </button>
        </div>
        <label>
          Column title
          <input
            class="column-title"
            :value="columnTitleDrafts[column.id] ?? column.title"
            maxlength="200"
            @input="updateColumnDraft(column.id, ($event.target as HTMLInputElement).value)"
          />
        </label>
        <div class="column-actions">
          <button @click="saveColumn(column.id)" :disabled="busy">Save</button>
          <button class="danger" @click="deleteColumn(column.id)">Delete</button>
        </div>
      </article>
    </section>
  </template>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { ref } from 'vue';
import { useBoardStore } from '../stores/boardStore';

const newColumnTitle = ref('');
const columnTitleDrafts = ref<Record<number, string>>({});
const draggingColumnId = ref<number | null>(null);
const dragOverColumnId = ref<number | null>(null);

const boardStore = useBoardStore();
const { board, busy } = storeToRefs(boardStore);
const { createColumn: createColumnAction, saveColumn: saveColumnAction, moveColumn: moveColumnAction, deleteColumn } = boardStore;

function updateNewColumnTitle(value: string) {
  newColumnTitle.value = value;
}

async function createColumn() {
  await createColumnAction(newColumnTitle.value);
  newColumnTitle.value = '';
}

function updateColumnDraft(columnId: number, value: string) {
  columnTitleDrafts.value[columnId] = value;
}

async function saveColumn(columnId: number) {
  const title = columnTitleDrafts.value[columnId];
  if (title === undefined) {
    return;
  }

  await saveColumnAction(columnId, title);
}

function onColumnDragStart(columnId: number, event: DragEvent) {
  draggingColumnId.value = columnId;
  if (event.dataTransfer) {
    event.dataTransfer.effectAllowed = 'move';
    event.dataTransfer.setData('text/plain', String(columnId));
  }
}

function onColumnDragOver(columnId: number, event: DragEvent) {
  if (draggingColumnId.value === null || draggingColumnId.value === columnId) {
    return;
  }

  event.preventDefault();
  dragOverColumnId.value = columnId;
}

function onColumnDragLeave(columnId: number) {
  if (dragOverColumnId.value === columnId) {
    dragOverColumnId.value = null;
  }
}

async function onColumnDrop(targetColumnId: number, event: DragEvent) {
  event.preventDefault();
  dragOverColumnId.value = null;

  const draggingId = draggingColumnId.value;
  if (draggingId === null || draggingId === targetColumnId) {
    return;
  }

  const columns = board.value?.columns;
  if (!columns) {
    return;
  }

  const draggingIndex = columns.findIndex(x => x.id === draggingId);
  const targetIndex = columns.findIndex(x => x.id === targetColumnId);
  if (draggingIndex < 0 || targetIndex < 0 || draggingIndex === targetIndex) {
    return;
  }

  await moveColumnAction(draggingId, targetIndex);
}

function onColumnDragEnd() {
  draggingColumnId.value = null;
  dragOverColumnId.value = null;
}
</script>
