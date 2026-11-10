<template>
  <div class="btn-tab-list board-create-modes" role="tablist" aria-label="Board create mode">
    <button v-for="mode in modes" :key="mode.value" type="button" class="btn btn--tab"
      :class="{ 'is-active': modelValue === mode.value }" role="tab" :aria-selected="modelValue === mode.value"
      :disabled="disabled" @click="emit('update:modelValue', mode.value)">{{ mode.label }}</button>
  </div>
</template>

<script setup lang="ts">
import type { BoardCreateMode } from '../forms/boardCreateForm';
defineProps<{ modelValue: BoardCreateMode; disabled: boolean }>();
const emit = defineEmits<{ 'update:modelValue': [value: BoardCreateMode] }>();
const modes: { value: BoardCreateMode; label: string }[] = [
  { value: 'blank', label: 'Create Blank' },
  { value: 'clone', label: 'Clone board' },
  { value: 'package', label: 'Import package' }
];
</script>

<style scoped>
.board-create-modes { width: fit-content; max-width: 100%; align-self: flex-start; }
</style>
