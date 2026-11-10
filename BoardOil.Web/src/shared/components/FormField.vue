<template>
  <fieldset v-if="group" class="form-field" :class="{ 'form-field--boxed': boxed }" v-bind="inputAttrs" tabindex="-1">
    <legend>{{ label }}</legend>
    <slot :input-attrs="inputAttrs" />
    <FormFieldErrors :id="errorId" :field="field" :fields="errorFields" :category="category" />
    <p v-if="help" :id="helpId" class="form-field-help">{{ help }}</p>
  </fieldset>
  <div v-else class="form-field">
    <label v-if="checkbox" :for="inputId" class="form-field-checkbox">
      <slot :input-attrs="inputAttrs" />
      {{ label }}
    </label>
    <template v-else>
      <label :for="inputId">{{ label }}</label>
      <slot :input-attrs="inputAttrs" />
    </template>
    <FormFieldErrors :id="errorId" :field="field" :fields="errorFields" :category="category" />
    <p v-if="help" :id="helpId" class="form-field-help">{{ help }}</p>
  </div>
</template>

<script setup lang="ts">
import { computed, inject, onBeforeUnmount, useId } from 'vue';
import { useValidationStore } from '../stores/validationStore';
import FormFieldErrors from './FormFieldErrors.vue';
import { formValidationContext } from './formValidationContext';

const props = defineProps<{ field: string; label: string; errorFields?: readonly string[]; group?: boolean; boxed?: boolean; checkbox?: boolean; help?: string }>();
const form = inject(formValidationContext);
if (!form) throw new Error('FormField must be inside FormFeedback.');
const { category, registerField } = form;
const validation = useValidationStore();
const inputId = `form-field-${useId()}`;
const helpId = `${inputId}-help`;
const errorId = `${inputId}-errors`;
const inputAttrs = computed(() => {
  const hasErrors = [props.field, ...(props.errorFields ?? [])].some(field => validation.getFieldErrors(field, category.value).length > 0);
  const descriptions = [hasErrors ? errorId : null, props.help ? helpId : null].filter(Boolean);
  return { id: inputId, 'aria-invalid': hasErrors || undefined, 'aria-describedby': descriptions.join(' ') || undefined };
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
.form-field-help { margin: 0; color: var(--bo-ink-muted); }
.form-field-checkbox { display: flex; align-items: center; gap: 0.5rem; }
.form-field-checkbox input { width: auto; margin: 0; }
</style>
