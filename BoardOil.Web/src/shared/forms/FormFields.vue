<template>
  <FormField
    v-for="field in fields"
    :key="field.name"
    v-slot="{ inputAttrs }"
    :field="field.name"
    :label="field.label"
    :help="field.help"
    :checkbox="field.type === 'checkbox'"
    :error-fields="field.errorFields"
    :group="isControl(field) || field.type === 'radio'"
    :boxed="field.type === 'tokenScopes' || field.type === 'tokenExpiry'"
  >
    <component
      v-if="isControl(field)"
      :is="controlFor(field)"
      v-bind="controlProps(field)"
      :model-value="values[field.name]"
      :disabled="isDisabled(field)"
      @update:model-value="(value: unknown) => updateValue(field.name, value)"
    />
    <FileFormControl
      v-else-if="field.type === 'file'"
      v-bind="inputAttrs"
      :model-value="values[field.name] as File | null"
      :disabled="isDisabled(field)"
      :accept="field.accept"
      @update:model-value="value => updateValue(field.name, value)"
    />
    <textarea
      v-else-if="field.type === 'textarea'"
      v-bind="inputAttrs"
      :value="String(values[field.name])"
      :rows="field.rows ?? 4"
      :disabled="isDisabled(field)"
      @input="event => updateInput(field.name, event)"
      @compositionend="event => updateInput(field.name, event)"
    />
    <input
      v-else-if="field.type === 'checkbox'"
      v-bind="inputAttrs"
      type="checkbox"
      :checked="Boolean(values[field.name])"
      :disabled="isDisabled(field)"
      @change="event => updateCheckbox(field.name, event)"
    />
    <SearchableSelect
      v-else-if="field.type === 'searchableSelect'"
      v-bind="inputAttrs"
      :label="field.label"
      :model-value="String(values[field.name])"
      :options="field.options"
      :disabled="isDisabled(field)"
      @update:model-value="value => updateValue(field.name, value)"
    />
    <RadioFormControl
      v-else-if="field.type === 'radio'"
      :model-value="selectionValue(field)"
      :options="optionsFor(field)"
      :disabled="isDisabled(field)"
      @update:model-value="value => updateValue(field.name, value)"
    />
    <select
      v-else-if="field.type === 'select'"
      v-bind="inputAttrs"
      :value="values[field.name] ?? ''"
      :disabled="isDisabled(field)"
      :required="field.required"
      @change="event => updateSelection(field, event)"
    >
      <option
        v-for="option in optionsFor(field)"
        :key="String(option.value)"
        :value="option.value ?? ''"
        :disabled="option.disabled"
      >{{ option.label }}</option>
    </select>
    <span
      v-else-if="field.type === 'text' || field.type === 'email' || field.type === 'password'"
      class="form-text-input"
    >
      <input
        v-bind="inputAttrs"
        :value="values[field.name]"
        :type="field.type === 'email' ? 'text' : field.type"
        :inputmode="field.type === 'email' ? 'email' : undefined"
        :disabled="isDisabled(field)"
        :required="field.required"
        :maxlength="field.maxLength"
        :autocomplete="field.autocomplete"
        :placeholder="field.placeholder"
        :spellcheck="field.spellcheck"
        @input="event => updateInput(field.name, event)"
        @compositionend="event => updateInput(field.name, event)"
      />
      <button
        v-if="field.clearable"
        type="button"
        class="btn btn--secondary"
        :disabled="isDisabled(field) || !values[field.name]"
        @click="updateValue(field.name, '')"
      >Clear</button>
    </span>
  </FormField>
</template>

<script setup lang="ts" generic="T extends object">
import FormField from '../components/FormField.vue';
import SearchableSelect from '../components/SearchableSelect.vue';
import EmojiFormControl from '../components/EmojiFormControl.vue';
import StyleFormControl from '../components/StyleFormControl.vue';
import TokenScopesFormControl from '../components/TokenScopesFormControl.vue';
import TokenExpiryFormControl from '../components/TokenExpiryFormControl.vue';
import FileFormControl from '../components/FileFormControl.vue';
import RadioFormControl from '../components/RadioFormControl.vue';
import type { FormFieldDefinition, FormOption } from './formDefinition';

const props = defineProps<{
  fields: readonly FormFieldDefinition<T>[];
  values: T;
  busy: boolean
}>();
const emit = defineEmits<{ change: [field: Extract<keyof T, string>, value: T[Extract<keyof T, string>]] }>();

function isDisabled(field: FormFieldDefinition<T>) {
  return props.busy || (field.disabled?.(props.values) ?? false);
}

function isControl(field: FormFieldDefinition<T>) {
  return ['custom', 'emoji', 'style', 'tokenScopes', 'tokenExpiry'].includes(field.type);
}

function controlProps(field: FormFieldDefinition<T>) {
  if (field.type === 'style') return { allowedStyleNames: field.allowedStyleNames, presetColours: field.presetColours };
  if (field.type === 'tokenScopes') return { allowedScopes: field.allowedScopes };
  return {};
}

function controlFor(field: FormFieldDefinition<T>) {
  switch (field.type) {
    case 'emoji': return EmojiFormControl;
    case 'style': return StyleFormControl;
    case 'tokenScopes': return TokenScopesFormControl;
    case 'tokenExpiry': return TokenExpiryFormControl;
    case 'custom': return field.component;
  }
}

function updateValue(field: Extract<keyof T, string>, value: unknown) {
  emit('change', field, value as T[Extract<keyof T, string>]);
}

function updateInput(field: Extract<keyof T, string>, event: Event) {
  if (event instanceof InputEvent && event.isComposing) return;
  const input = event.target;
  if (input instanceof HTMLInputElement || input instanceof HTMLTextAreaElement) updateValue(field, input.value);
}

function selectionValue(field: FormFieldDefinition<T>) {
  return props.values[field.name] as string | number | null;
}

function optionsFor(field: FormFieldDefinition<T>): readonly FormOption<string | number | null>[] {
  if (field.type !== 'select' && field.type !== 'radio') return [];
  return typeof field.options === 'function' ? field.options(props.values) : field.options;
}

function updateSelection(field: FormFieldDefinition<T>, event: Event) {
  const select = event.target;
  if (!(select instanceof HTMLSelectElement)) return;
  const option = optionsFor(field)[select.selectedIndex];
  if (option && !option.disabled) updateValue(field.name, option.value);
}

function updateCheckbox(field: Extract<keyof T, string>, event: Event) {
  const input = event.target;
  if (input instanceof HTMLInputElement) updateValue(field, input.checked);
}
</script>

<style scoped>
.form-text-input { display: flex; align-items: center; gap: 0.5rem; }
.form-text-input input { flex: 1 1 auto; min-width: 0; width: 100%; }
</style>
