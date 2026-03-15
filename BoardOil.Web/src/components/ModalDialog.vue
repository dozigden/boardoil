<template>
  <dialog ref="dialogRef" class="card-modal" @cancel.prevent="emit('close')" @click="onDialogClick">
    <form v-if="open" class="editor card-modal-content" @submit.prevent="emit('submit')">
      <button type="button" class="ghost card-modal-close" :aria-label="closeLabel" :title="closeLabel" @click="emit('close')">
        <X :size="18" aria-hidden="true" />
      </button>
      <h3 class="card-modal-title">{{ title }}</h3>
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
  closeLabel?: string;
}>(), {
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
