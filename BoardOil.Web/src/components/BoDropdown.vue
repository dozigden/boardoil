<template>
  <div
    ref="rootRef"
    class="bo-dropdown"
    :class="`bo-dropdown--align-${align}`"
    @keydown.escape.prevent.stop="close"
  >
    <button
      type="button"
      class="btn btn--secondary bo-dropdown-trigger"
      :class="{ 'btn--icon': isIconOnly }"
      :disabled="disabled"
      :aria-expanded="isOpen"
      :aria-controls="menuId"
      :aria-label="triggerAriaLabel"
      :title="label"
      aria-haspopup="menu"
      @click="toggleOpen"
    >
      <component v-if="icon" :is="icon" :size="iconSize" aria-hidden="true" />
      <span v-if="triggerText">{{ triggerText }}</span>
    </button>
    <div v-if="isOpen" :id="menuId" class="bo-dropdown-panel" role="menu" :aria-label="menuAriaLabel">
      <div class="bo-dropdown-content">
        <slot :close="close" :open="isOpen" />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import type { Component } from 'vue';
import { useClickOutside } from '../composables/useClickOutside';

const props = withDefaults(defineProps<{
  label: string;
  text?: string | null;
  icon?: Component | null;
  iconSize?: number;
  align?: 'left' | 'right' | 'center';
  iconOnly?: boolean;
  disabled?: boolean;
  open?: boolean | null;
}>(), {
  align: 'left',
  iconOnly: false,
  disabled: false,
  text: null,
  icon: null,
  iconSize: 18,
  open: null
});

const emit = defineEmits<{
  'update:open': [open: boolean];
}>();

const rootRef = ref<HTMLElement | null>(null);
const internalOpen = ref(false);
const menuId = `bo-dropdown-${Math.random().toString(36).slice(2, 10)}`;
const isControlled = computed(() => props.open !== null);
const isOpen = computed(() => (isControlled.value ? props.open === true : internalOpen.value));
const triggerText = computed(() => {
  if (props.text !== null) {
    return props.text;
  }

  return props.iconOnly ? null : props.label;
});
const isIconOnly = computed(() => props.iconOnly || !triggerText.value);
const triggerAriaLabel = computed(() => (isIconOnly.value ? props.label : undefined));
const menuAriaLabel = computed(() => {
  const normalised = props.label.trim().toLowerCase();
  if (normalised.includes('menu')) {
    return props.label;
  }

  return `${props.label} menu`;
});

function setOpen(next: boolean) {
  if (props.disabled && next) {
    return;
  }

  if (!isControlled.value) {
    internalOpen.value = next;
  }

  emit('update:open', next);
}

function toggleOpen() {
  setOpen(!isOpen.value);
}

function close() {
  setOpen(false);
}

useClickOutside(rootRef, close, () => isOpen.value);

watch(
  () => props.disabled,
  nextDisabled => {
    if (nextDisabled) {
      internalOpen.value = false;
      emit('update:open', false);
    }
  }
);
</script>

<style scoped>
.bo-dropdown {
  position: relative;
  display: inline-flex;
  align-items: center;
  vertical-align: middle;
}

.bo-dropdown-trigger {
  user-select: none;
  gap: 0.4rem;
}

.bo-dropdown-panel {
  position: absolute;
  top: calc(100% + 0.35rem);
  min-width: 11rem;
  background: var(--bo-surface-base);
  border: 1px solid var(--bo-border-default);
  border-radius: 8px;
  padding: 0.35rem;
  box-shadow: var(--bo-shadow-pop);
  z-index: 10;
}

.bo-dropdown--align-left .bo-dropdown-panel {
  left: 0;
}

.bo-dropdown--align-right .bo-dropdown-panel {
  right: 0;
}

.bo-dropdown--align-center .bo-dropdown-panel {
  left: 50%;
  transform: translateX(-50%);
}

.bo-dropdown-content {
  display: grid;
  gap: 0.1rem;
}

:deep(.bo-dropdown-item) {
  width: 100%;
  text-align: left;
  white-space: nowrap;
  text-decoration: none;
  color: var(--bo-ink-default);
  border-radius: 6px;
  padding: 0.45rem 0.55rem;
  border: 1px solid transparent;
  background: var(--bo-surface-base);
  font: inherit;
  cursor: pointer;
}

:deep(.bo-dropdown-item:hover),
:deep(.bo-dropdown-item:focus-visible) {
  background: var(--bo-surface-energy);
  color: var(--bo-colour-energy);
}

:deep(.bo-dropdown-item:disabled) {
  opacity: 0.6;
  cursor: not-allowed;
}

:deep(.bo-dropdown-divider) {
  height: 1px;
  background: var(--bo-border-soft);
  margin: 0.2rem 0.15rem;
}

:deep(.bo-dropdown-item:has(.bo-dropdown-item-meta)) {
  display: flex;
  align-items: center;
  gap: 0.6rem;
}

:deep(.bo-dropdown-item .bo-dropdown-item-main) {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

:deep(.bo-dropdown-item .bo-dropdown-item-meta) {
  margin-left: auto;
  flex: 0 0 auto;
}
</style>
