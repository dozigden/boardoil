<template>
  <dialog ref="dialogRef" class="card-modal" :class="`card-modal-${size}`" @cancel.prevent="emit('close')" @click="onDialogClick">
    <form v-if="open" class="editor card-modal-content" @submit.prevent="emit('submit')">
      <button type="button" class="btn btn--ghost card-modal-close" :aria-label="closeLabel" :title="closeLabel" @click="emit('close')">
        <X :size="18" aria-hidden="true" />
      </button>
      <h3 class="card-modal-title">
        <slot name="title">{{ title }}</slot>
      </h3>
      <slot />
      <slot name="actions" />
    </form>
  </dialog>
</template>

<script setup lang="ts">
import { X } from 'lucide-vue-next';
import { nextTick, onBeforeUnmount, ref, watch } from 'vue';

const props = withDefaults(defineProps<{
  open: boolean;
  title: string;
  size?: 'md' | 'fill';
  closeLabel?: string;
}>(), {
  size: 'md',
  closeLabel: 'Cancel'
});

const emit = defineEmits<{
  close: [];
  submit: [];
}>();

const dialogRef = ref<HTMLDialogElement | null>(null);

function onDialogClick(event: MouseEvent) {
  if (event.target === dialogRef.value) {
    emit('close');
  }
}

async function syncDialogState() {
  await nextTick();
  const dialog = dialogRef.value;
  if (!dialog || !dialog.isConnected) {
    return;
  }

  if (props.open) {
    if (!dialog.open) {
      try {
        dialog.showModal();
      } catch {
        dialog.show();
      }
    }
    return;
  }

  if (dialog.open) {
    dialog.close();
  }
}

watch(
  () => props.open,
  () => {
    void syncDialogState();
  },
  { immediate: true, flush: 'post' }
);

watch(
  dialogRef,
  () => {
    void syncDialogState();
  },
  { flush: 'post' }
);

onBeforeUnmount(() => {
  const dialog = dialogRef.value;
  if (dialog?.open) {
    dialog.close();
  }
});
</script>

<style>
.card-modal {
  border: none;
  border-radius: 14px;
  padding: 0;
  background: transparent;
}

.card-modal.card-modal-md {
  width: min(34rem, calc(100vw - 2rem));
}

.card-modal.card-modal-fill {
  width: calc(100vw - 6rem);
  height: calc(100vh - 6rem);
  height: calc(100dvh - 6rem);
  max-width: none;
  max-height: none;
}

.card-modal.card-modal-fill .card-modal-content {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  height: 100%;
  max-height: 100%;
  overflow: auto;
}

.card-modal::backdrop {
  background: rgba(53, 22, 90, 0.45);
}

.card-modal-content {
  position: relative;
  margin: 0;
  background: var(--bo-surface-base);
  border: 1px solid var(--bo-border-soft);
  border-radius: 14px;
  padding: 1rem;
}

.card-modal-close {
  position: absolute;
  top: 0.65rem;
  right: 0.65rem;
  width: auto;
  border: none;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 0.3rem;
}

.card-modal-title {
  margin: 0 0 0.75rem;
  color: var(--bo-link);
}

.card-modal-actions {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: 0.25rem;
}

.card-modal-actions-left {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
}

.card-modal-save,
.card-modal-cancel,
.card-modal-delete {
  width: auto;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.4rem;
  padding: 0.5rem;
}
</style>
