<template>
  <template v-if="board">
    <section class="toolbar">
      <button type="button" class="ghost column-add-button" aria-label="Add column" title="Add column" @click="openNewColumnDraft">
        <Plus :size="16" aria-hidden="true" />
        <span>Add Column</span>
      </button>
    </section>

    <section class="column-manager">
      <article v-if="newColumnDraftTitle !== null" class="column-manager-item create-column-inline">
        <label class="create-card-inline-label">
          Column title
          <input
            ref="newColumnDraftInput"
            :value="newColumnDraftTitle"
            maxlength="200"
            placeholder="New column title"
            @input="updateNewColumnDraft(($event.target as HTMLInputElement).value)"
            @keydown.enter.prevent="saveNewColumnDraft"
            @keydown.esc.prevent="closeNewColumnDraft"
          />
        </label>
        <div class="editor-actions create-card-inline-actions">
          <button type="button" class="create-card-save" aria-label="Save new column" title="Save new column" @click="saveNewColumnDraft">
            <Check :size="16" aria-hidden="true" />
          </button>
          <button type="button" class="ghost create-card-cancel" aria-label="Cancel new column" title="Cancel" @click="closeNewColumnDraft">
            <X :size="16" aria-hidden="true" />
          </button>
        </div>
      </article>

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
          <button type="button" class="card-title-trigger column-title-trigger" @click="openColumnEditor(column.id)">
            <strong>{{ column.title }}</strong>
          </button>
          <button
            type="button"
            class="ghost column-drag-handle"
            aria-label="Drag to reorder column"
            title="Drag to reorder"
            draggable="true"
            @dragstart="onColumnDragStart(column.id, $event)"
            @dragend="onColumnDragEnd"
          >
            <GripVertical :size="16" aria-hidden="true" />
          </button>
        </div>
      </article>
    </section>
  </template>
</template>

<script setup lang="ts">
import { Check, GripVertical, Plus, X } from 'lucide-vue-next';
import { storeToRefs } from 'pinia';
import { nextTick, ref } from 'vue';
import { useRouter } from 'vue-router';
import { useBoardStore } from '../stores/boardStore';

const newColumnDraftTitle = ref<string | null>(null);
const newColumnDraftInput = ref<HTMLInputElement | null>(null);
const draggingColumnId = ref<number | null>(null);
const dragOverColumnId = ref<number | null>(null);

const router = useRouter();
const boardStore = useBoardStore();
const { board, busy } = storeToRefs(boardStore);
const { createColumn: createColumnAction, moveColumn: moveColumnAction } = boardStore;

async function openNewColumnDraft() {
  if (newColumnDraftTitle.value !== null) {
    newColumnDraftInput.value?.focus();
    return;
  }

  newColumnDraftTitle.value = '';
  await nextTick();
  newColumnDraftInput.value?.focus();
}

function updateNewColumnDraft(value: string) {
  if (newColumnDraftTitle.value === null) {
    return;
  }

  newColumnDraftTitle.value = value;
}

function closeNewColumnDraft() {
  newColumnDraftTitle.value = null;
  newColumnDraftInput.value = null;
}

async function saveNewColumnDraft() {
  const title = newColumnDraftTitle.value ?? '';
  if (!title.trim()) {
    return;
  }

  await createColumnAction(title);
  closeNewColumnDraft();
}

async function openColumnEditor(columnId: number) {
  await router.push({ name: 'columns-column', params: { columnId } });
}

function onColumnDragStart(columnId: number, event: DragEvent) {
  draggingColumnId.value = columnId;
  if (event.dataTransfer) {
    event.dataTransfer.effectAllowed = 'move';
    event.dataTransfer.setData('text/plain', String(columnId));

    const handle = event.currentTarget instanceof HTMLElement ? event.currentTarget : null;
    const columnCard = handle?.closest('.column-manager-item');
    if (columnCard instanceof HTMLElement) {
      event.dataTransfer.setDragImage(columnCard, Math.floor(columnCard.clientWidth / 2), 20);
    }
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

  const columnsWithoutDragging = columns.filter(x => x.id !== draggingId);
  const insertIndex = targetIndex;
  const positionAfterColumnId = insertIndex === 0
    ? null
    : columnsWithoutDragging[insertIndex - 1]?.id ?? null;

  await moveColumnAction(draggingId, positionAfterColumnId);
}

function onColumnDragEnd() {
  draggingColumnId.value = null;
  dragOverColumnId.value = null;
}
</script>
