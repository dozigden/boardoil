<template>
  <FixedChromeDialog open :title="definition.title" :close-label="definition.closeLabel ?? definition.cancelLabel" @close="cancel" @submit="submit">
    <FormFeedback ref="feedback" class="form-dialog-fields" :category="definition.category">
      <slot name="before-fields" :values="draft" :busy="locked" />
      <FormReassessment v-if="definition.reassess" :error="reassessmentError" @retry="reassess" />
      <FormFields :fields="fields" :values="draft" :busy="locked" @change="setValue" />
      <slot name="after-fields" :values="draft" :busy="locked" />
    </FormFeedback>
    <template #actions>
      <slot name="actions" :busy="locked" :cancel="cancel">
        <div class="fixed-chrome-dialog-actions">
          <div v-if="definition.actions?.length" class="fixed-chrome-dialog-actions-left">
            <button
              v-for="action in definition.actions" :key="action.name"
              type="button" class="btn" :class="{ 'btn--danger': action.danger }"
              :disabled="locked" :aria-label="action.label" :title="action.label" @click="runAction(action)"
            >
              <component v-if="action.icon" :is="action.icon" :size="16" aria-hidden="true" />
              <span v-else>{{ action.label }}</span>
            </button>
          </div>
          <div class="fixed-chrome-dialog-actions-left">
            <button type="submit" class="btn" :class="{ 'btn--reassessing': reassessing }" :disabled="!canSubmit" :aria-label="submitLabel" :title="submitLabel">
              <FormSubmitIcon :loading="reassessing" />
              <span>{{ definition.submitText ?? submitLabel }}</span>
            </button>
            <button type="button" class="btn btn--secondary" :disabled="busy" :aria-label="definition.cancelLabel ?? 'Cancel'" title="Cancel" @click="cancel">
              <X :size="16" aria-hidden="true" />
              <span>Cancel</span>
            </button>
          </div>
        </div>
      </slot>
    </template>
  </FixedChromeDialog>
</template>

<script setup lang="ts" generic="T extends object, R">
import { X } from '@lucide/vue';
import { computed, onBeforeUnmount, ref } from 'vue';
import FixedChromeDialog from '../components/FixedChromeDialog.vue';
import FormFeedback from '../components/FormFeedback.vue';
import { useValidationStore } from '../stores/validationStore';
import type { FormAction, FormDefinition } from './formDefinition';
import FormFields from './FormFields.vue';
import FormReassessment from './FormReassessment.vue';
import FormSubmitIcon from './FormSubmitIcon.vue';
import { useForm } from './useForm';

// Mount one instance per editing session; a new session gets fresh initial values.
const props = defineProps<{ definition: FormDefinition<T, R> }>();
const definition = props.definition;
const emit = defineEmits<{ close: []; saved: [value: R] }>();
const { draft, fields, busy, locked, reassessing, reassessmentError, reassess, canSubmit, setValue, submit: submitForm, runAction: runFormAction } = useForm(definition);
const submitLabel = computed(() => typeof definition.submitLabel === 'function'
  ? definition.submitLabel(draft.value) : definition.submitLabel);
const feedback = ref<InstanceType<typeof FormFeedback> | null>(null);
const validation = useValidationStore();
validation.clearValidation();
onBeforeUnmount(() => validation.clearValidation());

async function submit() {
  const result = await submitForm();
  if (!result) return;
  if (result.ok) emit('saved', result.data);
  else await feedback.value?.showFeedback();
}

async function runAction(action: FormAction<T>) {
  const result = await runFormAction(action);
  if (!result) return;
  if (result.ok) emit('close');
  else await feedback.value?.showFeedback();
}

function cancel() {
  if (!busy.value) emit('close');
}
</script>

<style scoped>
.form-dialog-fields { align-content: start; }
</style>
