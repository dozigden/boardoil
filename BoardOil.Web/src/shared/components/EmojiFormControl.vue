<template>
  <EmojiPickerDropdown :model-value="modelValue" :disabled="disabled" :teleport="false" trigger-label="Emoji" @update:model-value="update" />
</template>

<script setup lang="ts">
import EmojiPickerDropdown from './EmojiPickerDropdown.vue';
defineProps<{ modelValue: string | null; disabled: boolean }>();
const emit = defineEmits<{ 'update:modelValue': [value: string | null] }>();
function update(value: string | null) { emit('update:modelValue', value); }
</script>
