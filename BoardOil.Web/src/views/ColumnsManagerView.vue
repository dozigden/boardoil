<template>
  <template v-if="board">
    <section class="toolbar columns-manager-toolbar">
      <button type="button" class="btn" aria-label="Add column" title="Add column" @click="openNewColumnDraft">
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
        <div class="column-create-actions">
          <button type="button" class="btn" aria-label="Save new column" title="Save new column" @click="saveNewColumnDraft">
            Save
          </button>
          <button type="button" class="btn btn--secondary" aria-label="Cancel new column" title="Cancel" @click="closeNewColumnDraft">
            Cancel
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
          <h3 class="column-manager-title">{{ column.title }}</h3>
          <div class="column-manager-actions">
            <button type="button" class="btn btn--secondary" @click="openColumnEditor(column.id)">
              Edit
            </button>
            <div
              class="column-drag-handle"
              role="img"
              aria-label="Drag to reorder column"
              title="Drag to reorder"
              draggable="true"
              @dragstart="onColumnDragStart(column.id, $event)"
              @dragend="onColumnDragEnd"
            >
              <GripVertical :size="16" aria-hidden="true" />
            </div>
          </div>
        </div>
      </article>
    </section>
  </template>
</template>

<script setup lang="ts">
import { GripVertical, Plus } from 'lucide-vue-next';
import { storeToRefs } from 'pinia';
import { nextTick, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useBoardStore } from '../stores/boardStore';

const newColumnDraftTitle = ref<string | null>(null);
const newColumnDraftInput = ref<HTMLInputElement | null>(null);
const draggingColumnId = ref<number | null>(null);
const dragOverColumnId = ref<number | null>(null);

const route = useRoute();
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
  const boardId = resolveBoardId();
  if (boardId === null) {
    return;
  }

  await router.push({ name: 'columns-column', params: { boardId, columnId } });
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

function resolveBoardId() {
  const parsed = Number.parseInt(String(route.params.boardId ?? ''), 10);
  return Number.isFinite(parsed) ? parsed : null;
}

watch(
  () => route.params.boardId,
  async () => {
    const boardId = resolveBoardId();
    if (boardId === null) {
      await router.replace({ name: 'boards' });
      return;
    }

    const loaded = await boardStore.initialize(boardId);
    if (!loaded && resolveBoardId() === boardId) {
      await router.replace({ name: 'boards' });
    }
  },
  { immediate: true }
);
</script>
