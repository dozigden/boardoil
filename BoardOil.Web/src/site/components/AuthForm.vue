<template>
  <form class="auth-card panel panel--strong" @submit.prevent="submit">
    <h2>{{ definition.title }}</h2>
    <slot name="intro" />
    <FormFeedback ref="feedback" general-errors="toast">
      <FormFields :fields="fields" :values="draft" :busy="locked" @change="setValue" />
    </FormFeedback>
    <button type="submit" class="btn" :disabled="!canSubmit">{{ busy ? busyLabel : submitLabel }}</button>
    <slot name="footer" :busy="busy" />
  </form>
</template>

<script setup lang="ts" generic="T extends object, R">
import { computed, onBeforeUnmount, ref } from 'vue';
import FormFeedback from '../../shared/components/FormFeedback.vue';
import FormFields from '../../shared/forms/FormFields.vue';
import type { FormDefinition } from '../../shared/forms/formDefinition';
import { useForm } from '../../shared/forms/useForm';
import { useValidationStore } from '../../shared/stores/validationStore';

const props = defineProps<{ definition: FormDefinition<T, R>; busyLabel: string }>();
const definition = props.definition;
const emit = defineEmits<{ saved: [value: R] }>();
const feedback = ref<InstanceType<typeof FormFeedback> | null>(null);
const validation = useValidationStore();
let active = true;
const {
  draft,
  fields,
  busy,
  locked,
  canSubmit,
  setValue,
  submit: submitForm
} = useForm({
  ...definition,
  isCurrent: () => active && (definition.isCurrent?.() ?? true)
});
const submitLabel = computed(() => typeof definition.submitLabel === 'function'
  ? definition.submitLabel(draft.value) : definition.submitLabel);
validation.clearValidation();
onBeforeUnmount(() => {
  active = false;
  validation.clearValidation();
});

async function submit() {
  const result = await submitForm();
  if (!result) return;
  if (result.ok) emit('saved', result.data);
  else await feedback.value?.showFeedback();
}
</script>
