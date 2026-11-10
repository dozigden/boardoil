<template>
  <FixedChromeDialog open :title="definition.title" :close-label="definition.closeLabel ?? definition.cancelLabel" @close="cancel" @submit="submit">
    <FormFeedback ref="feedback" :category="definition.category">
      <slot name="before-fields" :values="draft" :busy="busy" />
      <FormFields :definition="definition" :values="draft" :busy="busy" @change="setValue" />
      <slot name="after-fields" :values="draft" :busy="busy" />
    </FormFeedback>
    <template #actions>
      <slot name="actions" :busy="busy" :cancel="cancel">
        <div class="fixed-chrome-dialog-actions">
          <div v-if="definition.actions?.length" class="fixed-chrome-dialog-actions-left">
            <button
              v-for="action in definition.actions" :key="action.name"
              type="button" class="btn" :class="{ 'btn--danger': action.danger }"
              :disabled="busy" :aria-label="action.label" :title="action.label" @click="runAction(action)"
            >
              <component v-if="action.icon" :is="action.icon" :size="16" aria-hidden="true" />
              <span v-else>{{ action.label }}</span>
            </button>
          </div>
          <div class="fixed-chrome-dialog-actions-left">
            <button type="submit" class="btn" :disabled="!canSubmit" :aria-label="definition.submitLabel" :title="definition.submitLabel">
              <Check :size="16" aria-hidden="true" />
              <span>{{ definition.submitText ?? definition.submitLabel }}</span>
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
import { Check, X } from '@lucide/vue';
import { onBeforeUnmount, ref } from 'vue';
import FixedChromeDialog from '../components/FixedChromeDialog.vue';
import FormFeedback from '../components/FormFeedback.vue';
import { useValidationStore } from '../stores/validationStore';
import type { FormAction, FormDefinition } from './formDefinition';
import FormFields from './FormFields.vue';
import { useForm } from './useForm';

// Mount one instance per editing session; a new session gets fresh initial values.
const props = defineProps<{ definition: FormDefinition<T, R> }>();
const emit = defineEmits<{ close: []; saved: [value: R] }>();
const { draft, busy, canSubmit, setValue, submit: submitForm, runAction: runFormAction } = useForm(props.definition);
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
