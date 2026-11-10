<template>
  <FormField
    v-for="field in definition.fields"
    :key="field.name"
    v-slot="{ inputAttrs }"
    :field="field.name"
    :label="field.label"
    :error-fields="field.errorFields"
    :group="field.type === 'custom' || field.type === 'emoji' || field.type === 'style'"
  >
    <component
      v-if="field.type === 'custom' || field.type === 'emoji' || field.type === 'style'"
      :is="controlFor(field)"
      :model-value="values[field.name]"
      :disabled="busy"
      @update:model-value="(value: unknown) => updateValue(field.name, value)"
    />
    <select
      v-else-if="field.type === 'select'"
      v-bind="inputAttrs"
      :value="values[field.name]"
      :disabled="busy"
      :required="field.required"
      @change="event => updateInput(field.name, event)"
    >
      <option v-for="option in field.options" :key="String(option.value)" :value="option.value">{{ option.label }}</option>
    </select>
    <input
      v-else
      v-bind="inputAttrs"
      :value="values[field.name]"
      :type="field.type"
      :disabled="busy"
      :required="field.required"
      :maxlength="field.maxLength"
      :autocomplete="field.autocomplete"
      :placeholder="field.placeholder"
      @input="event => updateInput(field.name, event)"
      @compositionend="event => updateInput(field.name, event)"
    />
  </FormField>
</template>

<script setup lang="ts" generic="T extends object, R">
import FormField from '../components/FormField.vue';
import EmojiFormControl from '../components/EmojiFormControl.vue';
import StyleFormControl from '../components/StyleFormControl.vue';
import type { FormDefinition, FormFieldDefinition } from './formDefinition';

defineProps<{ definition: FormDefinition<T, R>; values: T; busy: boolean }>();
const emit = defineEmits<{ change: [field: Extract<keyof T, string>, value: T[Extract<keyof T, string>]] }>();

function controlFor(field: FormFieldDefinition<T>) {
  switch (field.type) {
    case 'emoji': return EmojiFormControl;
    case 'style': return StyleFormControl;
    case 'custom': return field.component;
  }
}

function updateValue(field: Extract<keyof T, string>, value: unknown) {
  // Dynamic Vue components cross a runtime boundary; definitions bind each control to its draft field.
  emit('change', field, value as T[Extract<keyof T, string>]);
}

function updateInput(field: Extract<keyof T, string>, event: Event) {
  if (event instanceof InputEvent && event.isComposing) return;
  const input = event.target;
  if (input instanceof HTMLInputElement || input instanceof HTMLSelectElement) updateValue(field, input.value);
}
</script>
