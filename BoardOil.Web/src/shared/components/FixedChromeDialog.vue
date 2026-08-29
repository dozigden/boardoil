<template>
  <dialog
    ref="dialogRef"
    class="fixed-chrome-dialog"
    :class="[`fixed-chrome-dialog--${size}`, `fixed-chrome-dialog--body-${bodyMode}`]"
    :aria-labelledby="titleId"
    @cancel.prevent="emit('close')"
    @click="onDialogClick"
  >
    <form v-if="open" class="editor fixed-chrome-dialog__surface" @submit.prevent="emit('submit')">
      <header class="fixed-chrome-dialog__header">
        <div class="fixed-chrome-dialog__header-actions">
          <slot name="headerActions" />
        </div>
        <button
          type="button"
          class="btn btn--secondary btn--icon fixed-chrome-dialog__close"
          :aria-label="closeLabel"
          :title="closeLabel"
          @click="emit('close')"
        >
          <X :size="18" aria-hidden="true" />
        </button>
        <h3 :id="titleId" class="fixed-chrome-dialog__title">
          <slot name="title">{{ title }}</slot>
        </h3>
      </header>

      <div class="fixed-chrome-dialog__body">
        <slot />
      </div>

      <footer v-if="$slots.actions" class="fixed-chrome-dialog__footer">
        <slot name="actions" />
      </footer>
    </form>
  </dialog>
</template>

<script setup lang="ts">
import { X } from 'lucide-vue-next';
import { nextTick, onBeforeUnmount, ref, useId, watch } from 'vue';

const props = withDefaults(defineProps<{
  open: boolean;
  title: string;
  size?: 'md' | 'fill';
  closeLabel?: string;
  bodyMode?: 'scroll' | 'managed';
}>(), {
  size: 'md',
  closeLabel: 'Cancel',
  bodyMode: 'scroll'
});

const emit = defineEmits<{
  close: [];
  submit: [];
}>();

const dialogRef = ref<HTMLDialogElement | null>(null);
const titleId = `fixed-chrome-dialog-title-${useId()}`;

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

<style scoped>
.fixed-chrome-dialog {
  border: none;
  border-radius: 14px;
  padding: 0;
  background: transparent;
  overflow: visible;
  max-height: calc(100vh - 2rem);
  max-height: calc(100dvh - 2rem);
}

.fixed-chrome-dialog--md {
  width: min(34rem, calc(100vw - 2rem));
}

.fixed-chrome-dialog--fill {
  width: calc(100vw - 6rem);
  height: calc(100vh - 6rem);
  height: calc(100dvh - 6rem);
  max-width: none;
  max-height: none;
}

.fixed-chrome-dialog::backdrop {
  background: var(--bo-overlay-backdrop);
}

.fixed-chrome-dialog__surface {
  position: relative;
  display: grid;
  grid-template-rows: auto minmax(0, 1fr) auto;
  gap: 0;
  min-height: 0;
  max-height: calc(100vh - 2rem);
  max-height: calc(100dvh - 2rem);
  margin: 0;
  padding: 0;
  overflow: hidden;
  border: 1px solid var(--bo-border-soft);
  border-radius: 14px;
  background: var(--bo-surface-base);
  color: var(--bo-ink-strong);
}

.fixed-chrome-dialog--fill .fixed-chrome-dialog__surface {
  height: 100%;
  max-height: 100%;
}

.fixed-chrome-dialog__header {
  position: relative;
  min-width: 0;
  padding: 1rem 3.5rem 0.75rem 1rem;
}

.fixed-chrome-dialog__header-actions {
  position: absolute;
  top: 0.65rem;
  right: 3.45rem;
  z-index: 1;
  display: inline-flex;
  align-items: center;
}

.fixed-chrome-dialog__close {
  position: absolute;
  top: 0.65rem;
  right: 0.65rem;
}

.fixed-chrome-dialog__title {
  margin: 0;
  color: var(--bo-link);
}

.fixed-chrome-dialog__body {
  display: grid;
  gap: 0.5rem;
  min-height: 0;
  padding: 0 1rem 0.75rem;
  overscroll-behavior: contain;
}

.fixed-chrome-dialog--body-scroll .fixed-chrome-dialog__body {
  overflow-y: auto;
}

.fixed-chrome-dialog--body-managed .fixed-chrome-dialog__body {
  overflow: hidden;
}

.fixed-chrome-dialog__footer {
  padding: 0 1rem 1rem;
}

@media (max-width: 720px) {
  .fixed-chrome-dialog,
  .fixed-chrome-dialog--md,
  .fixed-chrome-dialog--fill {
    width: 100vw;
    height: 100vh;
    height: 100dvh;
    max-width: none;
    max-height: none;
    margin: 0;
    border-radius: 0;
  }

  .fixed-chrome-dialog__surface {
    height: 100%;
    max-height: 100%;
    border-radius: 0;
  }
}
</style>
