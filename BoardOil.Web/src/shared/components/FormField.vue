<template>
  <div class="form-field" @input="clearErrors" @change="clearErrors">
    <label :for="inputId">
      {{ label }}
      <slot :input-attrs="inputAttrs" />
    </label>
    <FormFieldErrors :id="errorId" :field="field" :category="category" />
  </div>
</template>

<script setup lang="ts">
import { computed, inject, onBeforeUnmount, useId } from 'vue';
import { useValidationStore } from '../stores/validationStore';
import FormFieldErrors from './FormFieldErrors.vue';
import { formValidationContext } from './formValidationContext';

const props = defineProps<{ field: string; label: string; clearFields?: readonly string[] }>();
const form = inject(formValidationContext);
if (!form) {
  throw new Error('FormField must be inside FormValidation.');
}
const { category, registerField } = form;
const validation = useValidationStore();
const inputId = `form-field-${useId()}`;
const errorId = `${inputId}-errors`;
const inputAttrs = computed(() => {
  const hasErrors = validation.getFieldErrors(props.field, category.value).length > 0;
  return {
    id: inputId,
    'aria-invalid': hasErrors || undefined,
    'aria-describedby': hasErrors ? errorId : undefined
  };
});

onBeforeUnmount(registerField(props.field));

function clearErrors() {
  validation.clearFieldErrors(props.field, category.value);
  for (const field of props.clearFields ?? []) {
    validation.clearFieldErrors(field, category.value);
  }
}
</script>

<style scoped>
.form-field {
  display: grid;
  gap: 0.5rem;
  min-width: 0;
}
</style>
