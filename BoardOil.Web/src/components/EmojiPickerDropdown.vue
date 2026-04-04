<template>
  <div ref="containerRef" class="emoji-picker-dropdown">
    <button
      type="button"
      class="btn btn--secondary emoji-picker-dropdown-trigger"
      :disabled="disabled"
      :aria-expanded="open"
      aria-haspopup="dialog"
      @click="toggleOpen"
    >
      <span>{{ selectedEmoji ?? placeholder }}</span>
    </button>
    <div v-if="open" class="emoji-picker-dropdown-panel">
      <emoji-picker class="emoji-picker-dropdown-picker" @emoji-click="handleEmojiClick" />
      <div class="emoji-picker-dropdown-actions">
        <button type="button" class="btn btn--secondary" :disabled="disabled || !selectedEmoji" @click="clearEmoji">
          Clear
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import 'emoji-picker-element';
import { computed, ref, watch } from 'vue';
import { useClickOutside } from '../composables/useClickOutside';
import { normaliseTagEmojiForRender } from '../utils/tagStyles';

const props = withDefaults(defineProps<{
  modelValue: string | null;
  disabled?: boolean;
  placeholder?: string;
}>(), {
  disabled: false,
  placeholder: 'Select emoji'
});

const emit = defineEmits<{
  'update:modelValue': [value: string | null];
}>();

const open = ref(false);
const containerRef = ref<HTMLElement | null>(null);

const selectedEmoji = computed(() => normaliseTagEmojiForRender(props.modelValue));

function toggleOpen() {
  if (props.disabled) {
    return;
  }

  open.value = !open.value;
}

function handleEmojiClick(event: Event) {
  const emojiEvent = event as CustomEvent<{ unicode?: string }>;
  const emoji = normaliseTagEmojiForRender(emojiEvent.detail?.unicode);
  if (!emoji) {
    return;
  }

  emit('update:modelValue', emoji);
  open.value = false;
}

function clearEmoji() {
  if (props.disabled || !selectedEmoji.value) {
    return;
  }

  emit('update:modelValue', null);
  open.value = false;
}

useClickOutside(containerRef, () => {
  open.value = false;
}, () => open.value);

watch(
  () => props.disabled,
  nextDisabled => {
    if (nextDisabled) {
      open.value = false;
    }
  }
);

</script>

<style scoped>
.emoji-picker-dropdown {
  position: relative;
}

.emoji-picker-dropdown-trigger {
  width: auto;
}

.emoji-picker-dropdown-panel {
  position: absolute;
  top: calc(100% + 0.35rem);
  left: 0;
  width: 22rem;
  max-width: min(22rem, calc(100vw - 3rem));
  border: 1px solid var(--bo-border-soft);
  border-radius: 0.8rem;
  overflow: hidden;
  background: var(--bo-surface-panel-strong);
  box-shadow: var(--bo-shadow-pop);
  z-index: 4;
}

.emoji-picker-dropdown-picker {
  display: block;
  width: 100%;
  min-height: 16rem;
  max-height: 18rem;
}

.emoji-picker-dropdown-actions {
  display: flex;
  justify-content: flex-end;
  padding: 0.4rem 0.45rem;
  border-top: 1px solid var(--bo-border-soft);
  background: var(--bo-surface-base);
}
</style>
