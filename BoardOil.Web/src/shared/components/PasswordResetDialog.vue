<template>
  <FormDialog :definition="form" @close="close" @saved="close">
    <template #before-fields>
      <p class="reset-password-hint">Set a new password for {{ targetUserName }}.</p>
    </template>
  </FormDialog>
</template>

<script setup lang="ts">
import FormDialog from '../forms/FormDialog.vue';
import type { FormDefinition } from '../forms/formDefinition';
import { createPasswordResetForm, type PasswordResetRequest } from '../forms/passwordResetForm';

const props = defineProps<{
  targetUserName: string;
  save: FormDefinition<PasswordResetRequest, void>['submit'];
}>();
const emit = defineEmits<{ close: [] }>();
const form = createPasswordResetForm('admin', props.save);

function close() {
  emit('close');
}
</script>

<style scoped>
.reset-password-hint { margin: 0 0 0.75rem; color: var(--bo-ink-muted); }
</style>
