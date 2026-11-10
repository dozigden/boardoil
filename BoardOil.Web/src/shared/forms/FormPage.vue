<template>
  <form class="form-page" @submit.prevent="submit">
    <header class="form-page-header">
      <h2>{{ definition.title }}</h2>
      <button type="submit" class="btn" :disabled="!canSubmit || !dirty">{{ busy ? 'Saving...' : definition.submitLabel }}</button>
    </header>
    <slot name="before-fields" />
    <section class="panel panel-stack panel-stack--cozy">
      <slot name="fields-header" />
      <FormFeedback ref="feedback" :category="definition.category" general-errors="toast">
        <FormFields :definition="definition" :values="draft" :busy="busy" @change="setValue" />
      </FormFeedback>
    </section>
  </form>
</template>

<script setup lang="ts" generic="T extends object, R">
import { onBeforeUnmount, onMounted, ref } from 'vue';
import { onBeforeRouteLeave } from 'vue-router';
import FormFeedback from '../components/FormFeedback.vue';
import { useConfirm } from '../composables/useConfirm';
import { useValidationStore } from '../stores/validationStore';
import type { FormDefinition } from './formDefinition';
import FormFields from './FormFields.vue';
import { useForm } from './useForm';

const props = defineProps<{ definition: FormDefinition<T, R>; discardMessage: string }>();
const emit = defineEmits<{ saved: [value: R] }>();
let active = true;
const { draft, busy, dirty, canSubmit, setValue, submit: submitForm } = useForm({
  ...props.definition,
  isCurrent: () => active && (props.definition.isCurrent?.() ?? true)
});
const feedback = ref<InstanceType<typeof FormFeedback> | null>(null);
const validation = useValidationStore();
const { confirm } = useConfirm();
validation.clearValidation();

async function submit() {
  if (!dirty.value) return;
  const result = await submitForm();
  if (!result) return;
  if (result.ok) emit('saved', result.data);
  else await feedback.value?.showFeedback();
}

function onBeforeUnload(event: BeforeUnloadEvent) {
  if (!dirty.value) return;
  event.preventDefault();
  event.returnValue = '';
}

onBeforeRouteLeave(async () => {
  if (busy.value) return false;
  if (!dirty.value) return true;
  return await confirm({
    title: 'Discard unsaved changes', message: props.discardMessage, confirmLabel: 'Discard', danger: true
  });
});
onMounted(() => window.addEventListener('beforeunload', onBeforeUnload));
onBeforeUnmount(() => {
  active = false;
  window.removeEventListener('beforeunload', onBeforeUnload);
  validation.clearValidation();
});
</script>

<style scoped>
.form-page { display: grid; gap: 0.9rem; }
.form-page-header { display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem; }
.form-page-header h2 { margin: 0; }
@media (max-width: 620px) {
  .form-page-header { align-items: stretch; flex-direction: column; }
}
</style>
