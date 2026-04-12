<template>
  <BoDropdown
    class="emoji-picker-dropdown"
    label="Emoji picker"
    :text="selectedEmoji ?? placeholder"
    :disabled="disabled"
    panel-role="dialog"
    popup="dialog"
  >
    <template #default="{ close }">
      <emoji-picker class="emoji-picker-dropdown-picker" @emoji-click="event => handleEmojiClick(event, close)" />
      <div class="emoji-picker-dropdown-actions">
        <button
          type="button"
          class="btn btn--secondary"
          :disabled="disabled || !selectedEmoji"
          @click="clearEmoji(close)"
        >
          Clear
        </button>
      </div>
    </template>
  </BoDropdown>
</template>

<script setup lang="ts">
import 'emoji-picker-element';
import { computed } from 'vue';
import BoDropdown from './BoDropdown.vue';
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

const selectedEmoji = computed(() => normaliseTagEmojiForRender(props.modelValue));

function handleEmojiClick(event: Event, close?: () => void) {
  const emojiEvent = event as CustomEvent<{ unicode?: string }>;
  const emoji = normaliseTagEmojiForRender(emojiEvent.detail?.unicode);
  if (!emoji) {
    return;
  }

  emit('update:modelValue', emoji);
  close?.();
}

function clearEmoji(close?: () => void) {
  if (props.disabled || !selectedEmoji.value) {
    return;
  }

  emit('update:modelValue', null);
  close?.();
}
</script>

<style scoped>
.emoji-picker-dropdown :deep(.bo-dropdown-panel) {
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

.emoji-picker-dropdown :deep(.bo-dropdown-content) {
  gap: 0;
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
