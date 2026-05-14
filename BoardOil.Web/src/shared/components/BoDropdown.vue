<template>
  <div
    ref="rootRef"
    class="bo-dropdown"
    :class="`bo-dropdown--align-${align}`"
    :data-placement="verticalPlacement"
    @keydown.escape.prevent.stop="close"
  >
    <button
      ref="triggerRef"
      type="button"
      class="btn btn--secondary bo-dropdown-trigger"
      :class="[buttonClass, { 'btn--icon': isIconOnly }]"
      :disabled="disabled"
      :aria-expanded="isOpen"
      :aria-controls="menuId"
      :aria-label="triggerAriaLabel"
      :title="label"
      :aria-haspopup="popup"
      @click="toggleOpen"
    >
      <slot name="icon">
        <component v-if="icon" :is="icon" :size="iconSize" aria-hidden="true" />
      </slot>
      <span v-if="triggerText">{{ triggerText }}</span>
    </button>
    <Teleport to="body">
      <div
        v-if="isOpen"
        ref="panelRef"
        :id="menuId"
        class="bo-dropdown-panel"
        :role="panelRole"
        :aria-label="label"
        :style="panelStyle"
      >
        <div class="bo-dropdown-content">
          <slot :close="close" :open="isOpen" />
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue';
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
  buttonClass?: string | string[] | Record<string, boolean> | null;
  panelRole?: string;
  popup?: boolean | 'menu' | 'dialog';
}>(), {
  align: 'left',
  iconOnly: false,
  disabled: false,
  text: null,
  icon: null,
  iconSize: 18,
  buttonClass: null,
  panelRole: 'menu',
  popup: 'menu'
});

const rootRef = ref<HTMLElement | null>(null);
const triggerRef = ref<HTMLElement | null>(null);
const panelRef = ref<HTMLElement | null>(null);
const isOpen = ref(false);
const verticalPlacement = ref<'top' | 'bottom'>('bottom');
const panelTop = ref(0);
const panelLeft = ref(0);
const panelMaxHeightPx = ref<number | null>(null);
const menuId = `bo-dropdown-${Math.random().toString(36).slice(2, 10)}`;
const triggerText = computed(() => {
  if (props.text !== null) {
    return props.text;
  }

  return props.iconOnly ? null : props.label;
});
const isIconOnly = computed(() => props.iconOnly || !triggerText.value);
const triggerAriaLabel = computed(() => (isIconOnly.value ? props.label : undefined));
const panelStyle = computed(() => ({
  top: `${panelTop.value}px`,
  left: `${panelLeft.value}px`,
  '--bo-dropdown-max-height': panelMaxHeightPx.value === null ? undefined : `${panelMaxHeightPx.value}px`
}));

function setOpen(next: boolean) {
  if (props.disabled && next) {
    return;
  }

  isOpen.value = next;
}

function toggleOpen() {
  setOpen(!isOpen.value);
}

function close() {
  setOpen(false);
}

function resetPlacementState() {
  verticalPlacement.value = 'bottom';
  panelTop.value = 0;
  panelLeft.value = 0;
  panelMaxHeightPx.value = null;
}

function updatePlacement() {
  const trigger = triggerRef.value;
  const panel = panelRef.value;
  if (!trigger || !panel) {
    return;
  }

  const viewportPadding = 12;
  const gap = 6;
  const triggerRect = trigger.getBoundingClientRect();
  const panelRect = panel.getBoundingClientRect();
  const viewportWidth = window.innerWidth;
  const viewportHeight = window.innerHeight;

  const spaceBelow = viewportHeight - triggerRect.bottom - gap - viewportPadding;
  const spaceAbove = triggerRect.top - gap - viewportPadding;
  const fitsBelow = panelRect.height <= spaceBelow;
  const fitsAbove = panelRect.height <= spaceAbove;
  const shouldOpenUp = !fitsBelow && (fitsAbove || spaceAbove > spaceBelow);
  verticalPlacement.value = shouldOpenUp ? 'top' : 'bottom';

  panelMaxHeightPx.value = Math.max(120, Math.floor(shouldOpenUp ? spaceAbove : spaceBelow));

  const measuredRect = panel.getBoundingClientRect();
  const panelWidth = measuredRect.width;
  const panelHeight = measuredRect.height;

  let nextLeft = triggerRect.left;
  if (props.align === 'right') {
    nextLeft = triggerRect.right - panelWidth;
  }
  if (props.align === 'center') {
    nextLeft = triggerRect.left + ((triggerRect.width - panelWidth) / 2);
  }

  const minLeft = viewportPadding;
  const maxLeft = viewportWidth - viewportPadding - panelWidth;
  panelLeft.value = Math.round(Math.min(Math.max(nextLeft, minLeft), Math.max(minLeft, maxLeft)));

  panelTop.value = Math.round(
    shouldOpenUp
      ? triggerRect.top - gap - panelHeight
      : triggerRect.bottom + gap
  );
}

watch(isOpen, async open => {
  if (!open) {
    resetPlacementState();
    return;
  }

  await nextTick();
  updatePlacement();
});

useClickOutside(
  () => [rootRef.value, panelRef.value].filter((element): element is HTMLElement => element !== null),
  close,
  () => isOpen.value
);

watch(
  () => props.disabled,
  nextDisabled => {
    if (nextDisabled) {
      isOpen.value = false;
    }
  }
);

onMounted(() => {
  window.addEventListener('resize', updatePlacement);
  window.addEventListener('scroll', updatePlacement, true);
});

onBeforeUnmount(() => {
  window.removeEventListener('resize', updatePlacement);
  window.removeEventListener('scroll', updatePlacement, true);
});
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
  position: fixed;
  min-width: 11rem;
  max-width: calc(100vw - 1.5rem);
  max-height: var(--bo-dropdown-max-height, min(56vh, 22rem));
  overflow: auto;
  background: var(--bo-surface-base);
  border: 1px solid var(--bo-border-default);
  border-radius: 8px;
  padding: 0.35rem;
  box-shadow: var(--bo-shadow-pop);
  z-index: 10;
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
