<template>
  <input ref="input" class="file-form-control" type="file" :accept="accept" :disabled="disabled" @change="choose">
</template>

<script setup lang="ts">
import { ref, watch } from 'vue';
const props = defineProps<{ modelValue: File | null; disabled: boolean; accept?: string }>();
const emit = defineEmits<{ 'update:modelValue': [value: File | null] }>();
const input = ref<HTMLInputElement | null>(null);

watch([() => props.modelValue, input], ([file, element]) => {
  if (!element) return;
  if (!file) {
    element.value = '';
  } else if (element.files?.[0] !== file) {
    const selection = new DataTransfer();
    selection.items.add(file);
    element.files = selection.files;
  }
}, { immediate: true, flush: 'post' });

function choose() {
  emit('update:modelValue', input.value?.files?.[0] ?? null);
}
</script>

<style scoped>
.file-form-control { min-width: 0; width: 100%; }
</style>
