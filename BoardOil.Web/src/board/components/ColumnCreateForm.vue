<template>
  <form ref="root" class="entity-row create-column-inline" @submit.prevent="submit" @keydown.esc.prevent="cancel">
    <FormFeedback ref="feedback" class="column-create-fields" general-errors="toast">
      <FormFields :fields="fields" :values="draft" :busy="locked" @change="setValue" />
    </FormFeedback>
    <div class="entity-row-actions column-create-actions">
      <button type="submit" class="btn" :disabled="!canSubmit" aria-label="Save new column">Save</button>
      <button type="button" class="btn btn--secondary" :disabled="locked" aria-label="Cancel new column" @click="cancel">Cancel</button>
    </div>
  </form>
</template>

<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue';
import FormFeedback from '../../shared/components/FormFeedback.vue';
import FormFields from '../../shared/forms/FormFields.vue';
import { useForm } from '../../shared/forms/useForm';
import { useValidationStore } from '../../shared/stores/validationStore';
import { createColumnForm } from '../forms/columnForm';
import { useBoardStore } from '../stores/boardStore';

const emit = defineEmits<{ close: [] }>();
const store = useBoardStore();
const boardId = store.currentBoardId!;
const validation = useValidationStore();
const root = ref<HTMLFormElement | null>(null);
const feedback = ref<InstanceType<typeof FormFeedback> | null>(null);
const { draft, fields, locked, canSubmit, setValue, submit: submitForm } = useForm(createColumnForm(null, {
  save: store.createColumn,
  canEdit: () => store.isCurrentUserOwner,
  isCurrent: () => store.currentBoardId === boardId
}));
validation.clearValidation();
onBeforeUnmount(() => validation.clearValidation());
onMounted(focus);

function focus() {
  root.value?.querySelector('input')?.focus();
}

async function submit() {
  const result = await submitForm();
  if (result?.ok) emit('close');
  else if (result) await feedback.value?.showFeedback();
}

function cancel() {
  if (!locked.value) emit('close');
}

defineExpose({ focus });
</script>

<style scoped>
.create-column-inline { border-style: dashed; gap: 0.5rem; }
.column-create-fields { flex: 1; }
.column-create-actions { justify-content: flex-end; }
</style>
