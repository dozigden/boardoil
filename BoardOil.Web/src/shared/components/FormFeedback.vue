<template>
  <div ref="root" class="form-feedback">
    <FormErrorSummary v-if="generalErrors === 'inline'" ref="summary" :rendered-fields="renderedFields" :category="category" />
    <slot />
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, provide, ref } from 'vue';
import { useUiFeedbackStore } from '../stores/uiFeedbackStore';
import { useValidationStore } from '../stores/validationStore';
import FormErrorSummary from './FormErrorSummary.vue';
import { formValidationContext } from './formValidationContext';

const props = withDefaults(defineProps<{ category?: string; generalErrors?: 'inline' | 'toast' }>(), { category: '', generalErrors: 'inline' });
const validation = useValidationStore();
const feedback = useUiFeedbackStore();
const root = ref<HTMLElement | null>(null);
const summary = ref<InstanceType<typeof FormErrorSummary> | null>(null);
const renderedFields = ref<string[]>([]);

provide(formValidationContext, {
  category: computed(() => props.category),
  registerField(field) {
    renderedFields.value = [...renderedFields.value, field];
    return () => {
      renderedFields.value = renderedFields.value.filter(candidate => candidate !== field);
    };
  }
});

async function showFeedback() {
  await nextTick();
  if (!root.value?.isConnected || validation.getValidationMessages([], props.category).length === 0) return;
  if (props.generalErrors === 'toast') {
    const messages = validation.getValidationMessages(renderedFields.value, props.category);
    if (messages.length) feedback.showToast(messages.join(' '), 'error');
  }
  const input = root.value.querySelector<HTMLElement>('[aria-invalid="true"]');
  if (input) {
    input.focus();
  } else {
    summary.value?.focus();
  }
}

defineExpose({ showFeedback });
</script>

<style scoped>
.form-feedback {
  display: grid;
  gap: 0.5rem;
  min-width: 0;
}
</style>
