<template>
  <BoDropdown
    class="emoji-picker-dropdown"
    label="Emoji picker"
    :text="selectedEmoji ?? placeholder"
    :disabled="disabled"
    :teleport="teleport"
    panel-role="dialog"
    popup="dialog"
  >
    <template #trigger>
      <span v-if="selectedEmoji" class="bo-emoji">{{ selectedEmoji }}</span>
      <span v-else>{{ placeholder }}</span>
    </template>
    <template #default="{ close }">
      <emoji-picker
        :class="['emoji-picker-dropdown-picker', themeStore.activeTheme]"
        @emoji-click="(event: Event) => handleEmojiClick(event, close)"
      />
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
import { useThemeStore } from '../stores/themeStore';
import { normaliseTagEmojiForRender } from '../utils/tagStyles';

const props = withDefaults(defineProps<{
  modelValue: string | null;
  disabled?: boolean;
  placeholder?: string;
  teleport?: boolean;
}>(), {
  disabled: false,
  placeholder: 'Select emoji',
  teleport: true
});

const emit = defineEmits<{
  'update:modelValue': [value: string | null];
}>();

const themeStore = useThemeStore();
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
  color: var(--bo-text-default);
  --background: var(--bo-surface-base);
  --border-color: var(--bo-border-soft);
  --button-active-background: var(--bo-surface-energy);
  --button-hover-background: var(--bo-surface-muted);
  --category-font-color: var(--bo-text-default);
  --indicator-color: var(--bo-focus-ring);
  --input-border-color: var(--bo-border-default);
  --input-font-color: var(--bo-text-default);
  --input-placeholder-color: var(--bo-ink-subtle);
  --outline-color: var(--bo-focus-ring);
  --emoji-font-family: var(--bo-emoji-font-family);
}

.emoji-picker-dropdown-actions {
  display: flex;
  justify-content: flex-end;
  padding: 0.4rem 0.45rem;
  border-top: 1px solid var(--bo-border-soft);
  background: var(--bo-surface-base);
}
</style>
