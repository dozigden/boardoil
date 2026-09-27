<template>
  <div ref="root" class="searchable-select">
    <div class="searchable-select-field">
      <input :id="id" ref="input" :value="displayText" role="combobox" type="text" autocomplete="off"
        :disabled="disabled" :aria-label="label" aria-autocomplete="list" :aria-expanded="expanded"
        :aria-controls="listId" :aria-activedescendant="activeOptionId" :placeholder="placeholder"
        @focus="onFocus" @click="open" @input="onInput" @keydown="onKeydown" @blur="close" />
      <button type="button" class="btn btn--secondary btn--icon searchable-select-toggle" tabindex="-1"
        :disabled="disabled" :aria-label="`Show ${label.toLowerCase()} options`" :aria-expanded="expanded"
        @pointerdown.prevent @click="toggle"><ChevronDown :size="16" aria-hidden="true" /></button>
    </div>
    <div :id="listId" ref="panel" popover="manual" role="listbox" :aria-label="`${label} options`"
      class="searchable-select-options" :style="panelStyle" @pointerdown.prevent>
      <div v-for="(option, index) in filteredOptions" :id="optionId(index)" :key="option.value"
        role="option" class="searchable-select-option" :aria-selected="option.value === model"
        :class="{ 'is-active': index === activeIndex }" @click="select(option.value)" @pointermove="activate(index)">
        <span>{{ option.label }}</span><Check v-if="option.value === model" :size="16" aria-hidden="true" />
      </div>
      <p v-if="!filteredOptions.length" class="searchable-select-empty">No matching options.</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { Check, ChevronDown } from '@lucide/vue';
import { computed, nextTick, onBeforeUnmount, onMounted, ref, useId, watch } from 'vue';
import { useClickOutside } from '../composables/useClickOutside';

const props = withDefaults(defineProps<{
  id: string;
  label: string;
  options: { value: string; label: string }[];
  disabled?: boolean;
  placeholder?: string;
}>(), { disabled: false, placeholder: 'Select an option' });
const model = defineModel<string>({ required: true });
const root = ref<HTMLElement | null>(null);
const input = ref<HTMLInputElement | null>(null);
const panel = ref<HTMLElement | null>(null);
const listId = `searchable-select-${useId()}`;
const expanded = ref(false);
const filtering = ref(false);
const query = ref('');
const activeIndex = ref(-1);
const panelStyle = ref<Record<string, string>>({});
const selected = computed(() => props.options.find(option => option.value === model.value));
const displayText = computed(() => filtering.value ? query.value : selected.value?.label ?? '');
const filteredOptions = computed(() => {
  if (!filtering.value) return props.options;
  const search = query.value.trim().toLowerCase().replace(/_/g, ' ');
  return props.options.filter(option => `${option.label} ${option.value}`.toLowerCase().replace(/_/g, ' ').includes(search));
});
const activeOptionId = computed(() => expanded.value && activeIndex.value >= 0 ? optionId(activeIndex.value) : undefined);
function optionId(index: number) { return `${listId}-${index}`; }
function positionPanel() {
  if (!expanded.value || !root.value || !panel.value) return;
  const field = root.value.getBoundingClientRect();
  const below = window.innerHeight - field.bottom - 12;
  const above = field.top - 12;
  const upwards = below < 220 && above > below;
  const height = Math.max(0, Math.min(260, upwards ? above : below));
  panelStyle.value = {
    left: `${field.left}px`, width: `${field.width}px`, maxHeight: `${height}px`,
    top: upwards ? 'auto' : `${field.bottom + 4}px`,
    bottom: upwards ? `${window.innerHeight - field.top + 4}px` : 'auto'
  };
}
async function open() {
  if (props.disabled || expanded.value) return;
  expanded.value = true;
  activeIndex.value = filteredOptions.value.findIndex(option => option.value === model.value);
  await nextTick();
  if (!expanded.value || !panel.value?.isConnected) return;
  panel.value.showPopover();
  positionPanel();
  scrollToActive();
}
function close() {
  expanded.value = false;
  filtering.value = false;
  query.value = '';
  activeIndex.value = -1;
  if (panel.value?.matches(':popover-open')) panel.value.hidePopover();
}
function onFocus() { void open(); input.value?.select(); }
function toggle() {
  if (expanded.value) { close(); return; }
  input.value?.focus();
  void open();
}
function onInput(event: Event) {
  query.value = (event.target as HTMLInputElement).value;
  filtering.value = true;
  void open();
  activeIndex.value = filteredOptions.value.length ? 0 : -1;
  void nextTick(scrollToActive);
}
function select(value: string) { model.value = value; close(); }
function activate(index: number) { activeIndex.value = index; }
function scrollToActive() { document.getElementById(optionId(activeIndex.value))?.scrollIntoView({ block: 'nearest' }); }
function onKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape' && expanded.value) {
    event.preventDefault();
    event.stopPropagation();
    close();
  } else if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
    event.preventDefault();
    if (!expanded.value) { void open(); return; }
    const count = filteredOptions.value.length;
    if (!count) return;
    if (event.key === 'ArrowDown') activeIndex.value = (activeIndex.value + 1) % count;
    else activeIndex.value = activeIndex.value <= 0 ? count - 1 : activeIndex.value - 1;
    void nextTick(scrollToActive);
  } else if (event.key === 'Enter' && expanded.value) {
    event.preventDefault();
    const option = filteredOptions.value[activeIndex.value];
    if (option) select(option.value);
  }
}
useClickOutside(root, close, expanded);
watch(() => props.disabled, disabled => { if (disabled) close(); });
onMounted(() => {
  window.addEventListener('resize', positionPanel);
  window.addEventListener('scroll', positionPanel, true);
});
onBeforeUnmount(() => {
  close();
  window.removeEventListener('resize', positionPanel);
  window.removeEventListener('scroll', positionPanel, true);
});
</script>

<style scoped>
.searchable-select { min-width: 0; }
.searchable-select-field { display: flex; position: relative; }
.searchable-select-field input { box-sizing: border-box; width: 100%; padding-right: 2.5rem; min-width: 0; }
.searchable-select-toggle { position: absolute; right: 0.2rem; top: 50%; transform: translateY(-50%); border: 0; background: transparent; }
.searchable-select-options { position: fixed; margin: 0; box-sizing: border-box; padding: 0.25rem; overflow-y: auto; border: 1px solid var(--bo-border-default); border-radius: 8px; background: var(--bo-surface-panel); color: var(--bo-ink-default); box-shadow: 0 6px 20px #0003; }
.searchable-select-option { display: flex; align-items: center; justify-content: space-between; gap: 0.5rem; padding: 0.5rem; border-radius: 4px; cursor: pointer; overflow-wrap: anywhere; }
.searchable-select-option svg { flex-shrink: 0; }
.searchable-select-option.is-active { background: var(--bo-surface-energy); }
.searchable-select-empty { margin: 0; padding: 0.5rem; color: var(--bo-ink-muted); }
</style>
