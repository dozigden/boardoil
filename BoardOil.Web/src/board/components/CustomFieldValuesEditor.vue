<template>
  <section v-if="definitions.length" ref="root" class="card-field-values" aria-label="Custom Fields">
    <div v-for="field in definitions" :key="field.code" class="card-field-control">
      <label>
        <span><span v-if="field.emoji" class="bo-emoji" aria-hidden="true">{{ field.emoji }} </span>{{ field.name }}<span v-if="!field.nullable" class="required-marker" aria-label="required"> *</span></span>
        <input
          :value="values[field.code] ?? ''"
          :type="field.dataType === 'date' ? 'date' : 'text'"
          :inputmode="field.dataType === 'integer' ? 'numeric' : 'text'"
          :maxlength="field.dataType === 'text' ? 250 : undefined"
          :disabled="disabled"
          :aria-label="field.name"
          :aria-required="!field.nullable"
          :aria-invalid="Boolean(errors[`customFieldValues.${field.code}`])"
          :aria-describedby="errors[`customFieldValues.${field.code}`] ? `${id}-${field.code}` : undefined"
          autocomplete="off"
          @input="(event: Event) => updateValue(field.code, event)"
        />
      </label>
      <small v-if="errors[`customFieldValues.${field.code}`]" :id="`${id}-${field.code}`" class="card-field-error">
        <CircleAlert :size="12" aria-hidden="true" /><span>{{ errors[`customFieldValues.${field.code}`].join(' ') }}</span>
      </small>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref, useId } from 'vue';
import { CircleAlert } from '@lucide/vue';
import type { CustomFieldDefinition, CustomFieldValues } from '../../shared/types/customFieldTypes';

defineProps<{ definitions: CustomFieldDefinition[]; values: CustomFieldValues; errors: Record<string, string[]>; disabled: boolean }>();
const emit = defineEmits<{ change: [code: string, value: string] }>();
const id = useId();
const root = ref<HTMLElement | null>(null);
function focusError() {
  const input = root.value?.querySelector<HTMLInputElement>('[aria-invalid="true"]');
  input?.focus();
  input?.closest('.card-field-control')?.scrollIntoView({ block: 'nearest' });
}
defineExpose({ focusError });
function updateValue(code: string, event: Event) {
  emit('change', code, (event.target as HTMLInputElement).value);
}
</script>

<style scoped>
.card-field-values {
  display: grid;
  gap: 0.65rem;
  padding-top: 0.75rem;
  border-top: 1px solid var(--bo-border-soft);
}
.card-field-control { display: grid; gap: 0.2rem; min-width: 0; }
.card-field-control label { display: grid; gap: 0.25rem; font-size: 0.85rem; }
.card-field-control input { min-width: 0; width: 100%; }
.card-field-control input[aria-invalid="true"] { border-color: var(--bo-colour-danger); }
.card-field-control .bo-emoji { margin-right: 0.25rem; }
.required-marker { color: var(--bo-ink-subtle); }
.card-field-error { display: flex; align-items: flex-start; gap: 0.25rem; color: var(--bo-colour-danger-ink); font-size: 0.75rem; line-height: 1.25; }
.card-field-error svg { flex-shrink: 0; margin-top: 0.1em; }
</style>
