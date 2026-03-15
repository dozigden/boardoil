<template>
  <dialog ref="dialogRef" class="card-modal" @cancel.prevent="closeColumnEditor" @click="onDialogClick">
    <form v-if="editingColumn" class="editor card-modal-content" @submit.prevent="saveColumn">
      <button type="button" class="ghost card-modal-close" aria-label="Cancel editing" title="Cancel" @click="closeColumnEditor">
        <X :size="18" aria-hidden="true" />
      </button>
      <h3 class="card-modal-title">Edit Column</h3>
      <label>
        Title
        <input
          :value="columnDraftTitle ?? editingColumn.title"
          maxlength="200"
          @input="updateColumnDraft(($event.target as HTMLInputElement).value)"
        />
      </label>

      <div class="editor-actions card-modal-actions">
        <button type="button" class="danger card-modal-delete" aria-label="Delete column" title="Delete column" @click="deleteEditingColumn">
          <Trash2 :size="16" aria-hidden="true" />
        </button>
        <div class="card-modal-actions-left">
          <button type="submit" class="card-modal-save" aria-label="Save column" title="Save column">
            <Check :size="16" aria-hidden="true" />
            <span>Save</span>
          </button>
          <button type="button" class="ghost card-modal-cancel" aria-label="Cancel editing" title="Cancel" @click="closeColumnEditor">
            <X :size="16" aria-hidden="true" />
            <span>Cancel</span>
          </button>
        </div>
      </div>
    </form>
  </dialog>
</template>

<script setup lang="ts">
import { Check, Trash2, X } from 'lucide-vue-next';
import { storeToRefs } from 'pinia';
import { computed, nextTick, onBeforeUnmount, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useBoardStore } from '../stores/boardStore';

const dialogRef = ref<HTMLDialogElement | null>(null);
const route = useRoute();
const router = useRouter();
const boardStore = useBoardStore();
const { board } = storeToRefs(boardStore);
const { saveColumn: saveColumnAction, deleteColumn } = boardStore;
const columnDraftTitle = ref<string | null>(null);

const routeColumnId = computed<number | null>(() => {
  const raw = route.params.columnId;
  const parsed = typeof raw === 'string' ? Number.parseInt(raw, 10) : Number.NaN;
  return Number.isFinite(parsed) ? parsed : null;
});

const editingColumn = computed(() => boardStore.getColumnById(routeColumnId.value));

function onDialogClick(event: MouseEvent) {
  if (event.target === dialogRef.value) {
    void closeColumnEditor();
  }
}

async function closeColumnEditor() {
  await router.push({ name: 'columns' });
}

function updateColumnDraft(value: string) {
  columnDraftTitle.value = value;
}

async function saveColumn() {
  const columnId = routeColumnId.value;
  const title = columnDraftTitle.value;
  if (columnId === null || title === null || !title.trim()) {
    return;
  }

  await saveColumnAction(columnId, title);
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
  [routeColumnId, editingColumn, board],
  ([nextColumnId, nextColumn, nextBoard], [previousColumnId]) => {
    if (nextColumnId === null) {
      void router.replace({ name: 'columns' });
      return;
    }

    if (!nextBoard) {
      return;
    }

    if (!nextColumn) {
      void router.replace({ name: 'columns' });
      return;
    }

    if (previousColumnId !== nextColumnId || columnDraftTitle.value === null) {
      columnDraftTitle.value = nextColumn.title;
    }
  },
  { immediate: true }
);

watch(
  [editingColumn, dialogRef],
  async ([nextColumn]) => {
    await nextTick();
    const dialog = dialogRef.value;
    if (!dialog) {
      return;
    }

    if (nextColumn) {
      if (!dialog.open) {
        dialog.showModal();
      }
      return;
    }

    if (dialog.open) {
      dialog.close();
    }
  },
  { immediate: true, flush: 'post' }
);

onBeforeUnmount(() => {
  const dialog = dialogRef.value;
  if (dialog?.open) {
    dialog.close();
  }
});
</script>
