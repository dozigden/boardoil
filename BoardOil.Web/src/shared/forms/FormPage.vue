<template>
  <form class="form-page" @submit.prevent="submit">
    <header class="form-page-header">
      <h2>{{ definition.title }}</h2>
      <button type="submit" class="btn" :class="{ 'btn--reassessing': reassessing }" :disabled="!canSubmit || !dirty">
        <FormSubmitIcon v-if="definition.reassess" :loading="reassessing" />
        <span>{{ busy ? 'Saving...' : submitLabel }}</span>
      </button>
    </header>
    <slot name="before-fields" :values="draft" :busy="locked" />
    <section class="panel panel-stack panel-stack--cozy">
      <slot name="fields-header" :values="draft" :busy="locked" />
      <FormFeedback ref="feedback" :category="definition.category" general-errors="toast">
        <FormReassessment v-if="definition.reassess" :error="reassessmentError" @retry="reassess" />
        <FormFields :fields="fields" :values="draft" :busy="locked" @change="setValue" />
        <slot name="after-fields" :values="draft" :busy="locked" />
      </FormFeedback>
    </section>
  </form>
</template>

<script setup lang="ts" generic="T extends object, R">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import { onBeforeRouteLeave } from 'vue-router';
import FormFeedback from '../components/FormFeedback.vue';
import { useConfirm } from '../composables/useConfirm';
import { useAuthStore } from '../stores/authStore';
import { useValidationStore } from '../stores/validationStore';
import type { FormDefinition } from './formDefinition';
import FormFields from './FormFields.vue';
import FormReassessment from './FormReassessment.vue';
import FormSubmitIcon from './FormSubmitIcon.vue';
import { useForm } from './useForm';

const props = defineProps<{ definition: FormDefinition<T, R>; discardMessage: string }>();
const emit = defineEmits<{ saved: [value: R] }>();
let active = true;
const { draft, fields, busy, locked, reassessing, reassessmentError, reassess, dirty, canSubmit, setValue, submit: submitForm } = useForm({
  ...props.definition,
  isCurrent: () => active && (props.definition.isCurrent?.() ?? true)
});
const submitLabel = computed(() => typeof props.definition.submitLabel === 'function'
  ? props.definition.submitLabel(draft.value) : props.definition.submitLabel);
const feedback = ref<InstanceType<typeof FormFeedback> | null>(null);
const validation = useValidationStore();
const auth = useAuthStore();
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

onBeforeRouteLeave(async to => {
  if (!auth.isAuthenticated && (to.name === 'unauthorized' || to.name === 'login')) return true;
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
