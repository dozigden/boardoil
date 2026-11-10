<template>
  <div ref="root" class="form-validation">
    <FormErrorSummary ref="summary" :rendered-fields="renderedFields" :category="category" />
    <slot />
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, provide, ref } from 'vue';
import { useValidationStore } from '../stores/validationStore';
import FormErrorSummary from './FormErrorSummary.vue';
import { formValidationContext } from './formValidationContext';

const props = withDefaults(defineProps<{ category?: string }>(), { category: '' });
const validation = useValidationStore();
const root = ref<HTMLElement | null>(null);
const summary = ref<InstanceType<typeof FormErrorSummary> | null>(null);
const renderedFields = ref<string[]>([]);
let form: HTMLFormElement | null = null;

provide(formValidationContext, {
  category: computed(() => props.category),
  registerField(field) {
    renderedFields.value = [...renderedFields.value, field];
    return () => {
      renderedFields.value = renderedFields.value.filter(candidate => candidate !== field);
    };
  }
});

validation.clearValidation();

// React to submitted results, not field corrections, so typing never moves focus.
const unsubscribe = validation.$onAction(({ name, after }) => {
  if (name === 'setValidationResult') {
    after(() => { void focusFeedback(); });
  }
});

async function focusFeedback() {
  await nextTick();
  if (!root.value?.isConnected || validation.getValidationMessages([], props.category).length === 0) {
    return;
  }
  const input = root.value.querySelector<HTMLElement>('[aria-invalid="true"]');
  if (input) {
    input.focus();
  } else {
    summary.value?.focus();
  }
}

function clearValidation() {
  validation.clearValidation();
}

onMounted(() => {
  form = root.value?.closest('form') ?? null;
  form?.addEventListener('submit', clearValidation, true);
});

onBeforeUnmount(() => {
  form?.removeEventListener('submit', clearValidation, true);
  unsubscribe();
  validation.clearValidation();
});
</script>

<style scoped>
.form-validation {
  display: grid;
  gap: 0.5rem;
  min-width: 0;
}
</style>
