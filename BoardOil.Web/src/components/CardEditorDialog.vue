<template>
  <dialog ref="dialogRef" class="card-modal" @cancel.prevent="emit('close')" @click="onDialogClick">
    <form v-if="card" class="editor card-modal-content" @submit.prevent="emit('save')">
      <button type="button" class="ghost card-modal-close" aria-label="Cancel editing" title="Cancel" @click="emit('close')">
        <X :size="18" aria-hidden="true" />
      </button>
      <h3 class="card-modal-title">{{ card.id }}</h3>
      <label>
        Title
        <input
          :value="draft?.title ?? card.title"
          maxlength="200"
          @focus="emit('announce-typing', 'title')"
          @blur="emit('stop-typing', 'title')"
          @input="emit('update-draft', 'title', ($event.target as HTMLInputElement).value)"
        />
      </label>

      <label>
        Description
        <textarea
          :value="draft?.description ?? card.description"
          maxlength="5000"
          @focus="emit('announce-typing', 'description')"
          @blur="emit('stop-typing', 'description')"
          @input="emit('update-draft', 'description', ($event.target as HTMLTextAreaElement).value)"
        />
      </label>

      <div class="editor-actions card-modal-actions">
        <button type="button" class="danger card-modal-delete" aria-label="Delete card" title="Delete card" @click="emit('delete')">
          <Trash2 :size="16" aria-hidden="true" />
        </button>
        <div class="card-modal-actions-left">
          <button type="submit" class="card-modal-save" aria-label="Save card" title="Save card">
            <Check :size="16" aria-hidden="true" />
            <span>Save</span>
          </button>
          <button type="button" class="ghost card-modal-cancel" aria-label="Cancel editing" title="Cancel" @click="emit('close')">
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
import { ref, watch } from 'vue';
import type { Card as BoardCard } from '../types/boardTypes';

const props = defineProps<{
  open: boolean;
  card: BoardCard | null;
  draft: { title: string; description: string } | null;
}>();

const emit = defineEmits<{
  close: [];
  save: [];
  delete: [];
  'announce-typing': [field: 'title' | 'description'];
  'stop-typing': [field: 'title' | 'description'];
  'update-draft': [field: 'title' | 'description', value: string];
}>();

const dialogRef = ref<HTMLDialogElement | null>(null);

function onDialogClick(event: MouseEvent) {
  if (event.target === dialogRef.value) {
    emit('close');
  }
}

watch(
  () => [props.open, props.card?.id] as const,
  ([open, cardId]) => {
    const dialog = dialogRef.value;
    if (!dialog) {
      return;
    }

    if (open && cardId !== undefined && !dialog.open) {
      dialog.showModal();
      return;
    }

    if ((!open || cardId === undefined) && dialog.open) {
      dialog.close();
    }
  },
  { immediate: true }
);
</script>
