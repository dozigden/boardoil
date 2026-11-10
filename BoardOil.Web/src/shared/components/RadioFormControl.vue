<template>
  <div class="radio-form-options">
    <label v-for="option in options" :key="String(option.value)" class="radio-form-option" :class="{ 'radio-form-option--disabled': disabled || option.disabled }">
      <input :name="groupName" type="radio" :checked="modelValue === option.value" :disabled="disabled || option.disabled" @change="choose(option)">
      <span>
        <strong>{{ option.label }}</strong>
        <small v-if="option.description">{{ option.description }}</small>
      </span>
    </label>
  </div>
</template>

<script setup lang="ts" generic="V extends string | number | null">
import { useId } from 'vue';
import type { FormOption } from '../forms/formDefinition';
const props = defineProps<{ modelValue: V; options: readonly FormOption<V>[]; disabled: boolean }>();
const emit = defineEmits<{ 'update:modelValue': [value: V] }>();
const groupName = `form-radio-${useId()}`;
function choose(option: FormOption<V>) {
  if (!props.disabled && !option.disabled) emit('update:modelValue', option.value);
}
</script>

<style scoped>
.radio-form-options { display: grid; gap: 0.5rem; }
.radio-form-option { display: grid; grid-template-columns: auto 1fr; gap: 0.55rem; align-items: start; padding: 0.65rem; border: 1px solid var(--bo-border-soft); border-radius: 8px; cursor: pointer; }
.radio-form-option input { margin-top: 0.2rem; }
.radio-form-option span { display: grid; gap: 0.15rem; }
.radio-form-option small { color: var(--bo-ink-muted); }
.radio-form-option--disabled { cursor: not-allowed; opacity: 0.65; }
</style>
