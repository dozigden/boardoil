<template>
  <fieldset v-if="group" class="form-field" :class="{ 'form-field--boxed': boxed }" v-bind="inputAttrs" tabindex="-1">
    <legend>{{ label }}</legend>
    <slot :input-attrs="inputAttrs" />
    <FormFieldErrors :id="errorId" :field="field" :fields="errorFields" :category="category" />
  </fieldset>
  <div v-else class="form-field">
    <label :for="inputId">
      {{ label }}
      <slot :input-attrs="inputAttrs" />
    </label>
    <FormFieldErrors :id="errorId" :field="field" :fields="errorFields" :category="category" />
  </div>
</template>

<script setup lang="ts">
import { computed, inject, onBeforeUnmount, useId } from 'vue';
import { useValidationStore } from '../stores/validationStore';
import FormFieldErrors from './FormFieldErrors.vue';
import { formValidationContext } from './formValidationContext';

const props = defineProps<{ field: string; label: string; errorFields?: readonly string[]; group?: boolean; boxed?: boolean }>();
const form = inject(formValidationContext);
if (!form) throw new Error('FormField must be inside FormFeedback.');
const { category, registerField } = form;
const validation = useValidationStore();
const inputId = `form-field-${useId()}`;
const errorId = `${inputId}-errors`;
const inputAttrs = computed(() => {
  const hasErrors = [props.field, ...(props.errorFields ?? [])].some(field => validation.getFieldErrors(field, category.value).length > 0);
  return { id: inputId, 'aria-invalid': hasErrors || undefined, 'aria-describedby': hasErrors ? errorId : undefined };
});

const unregister = [props.field, ...(props.errorFields ?? [])].map(registerField);
onBeforeUnmount(() => unregister.forEach(remove => remove()));
</script>

<style scoped>
.form-field {
  display: grid;
  gap: 0.5rem;
  min-width: 0;
  border: 0;
  padding: 0;
  margin: 0;
}
.form-field > label,
.form-field > legend {
  padding: 0;
}
.form-field--boxed {
  padding: 0.55rem 0.65rem;
  border: 1px solid var(--bo-border-soft);
  border-radius: 10px;
  background: var(--bo-surface-base);
}
.form-field--boxed > legend {
  font-weight: 700;
  color: var(--bo-link);
  padding: 0 0.25rem;
}
.form-field > legend {
  margin-bottom: 0.2rem;
  font-size: 0.85rem;
}
</style>
