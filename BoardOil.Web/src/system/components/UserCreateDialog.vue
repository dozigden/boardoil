<template>
  <FixedChromeDialog :open="open" title="Create User" close-label="Cancel creation" @close="closeDialog" @submit="submit">
    <FormValidation>
      <FormField v-slot="{ inputAttrs }" field="userName" label="Username">
        <input v-model="draft.userName" v-bind="inputAttrs" :disabled="busy" maxlength="64" required />
      </FormField>
      <FormField v-slot="{ inputAttrs }" field="displayName" label="Display name">
        <input v-model="draft.displayName" v-bind="inputAttrs" :disabled="busy" maxlength="64" required />
      </FormField>
      <FormField v-slot="{ inputAttrs }" field="email" label="Email">
        <input v-model="draft.email" v-bind="inputAttrs" :disabled="busy" type="email" autocomplete="email" maxlength="320" required />
      </FormField>
      <FormField v-slot="{ inputAttrs }" field="password" label="Password" :clear-fields="['confirmPassword']">
        <input v-model="draft.password" v-bind="inputAttrs" :disabled="busy" type="password" autocomplete="new-password" required />
      </FormField>
      <FormField v-slot="{ inputAttrs }" field="confirmPassword" label="Confirm password">
        <input v-model="confirmPassword" v-bind="inputAttrs" :disabled="busy" type="password" autocomplete="new-password" required />
      </FormField>
      <FormField v-slot="{ inputAttrs }" field="role" label="Role">
        <select v-model="draft.role" v-bind="inputAttrs" :disabled="busy">
          <option value="Standard">Standard</option>
          <option value="Admin">Admin</option>
        </select>
      </FormField>
    </FormValidation>

    <template #actions>
      <div class="fixed-chrome-dialog-actions">
        <div class="fixed-chrome-dialog-actions-left">
          <button type="submit" class="btn" :disabled="busy" aria-label="Create user" title="Create user">
            <Check :size="16" aria-hidden="true" />
            <span>Create user</span>
          </button>
          <button type="button" class="btn btn--secondary" :disabled="busy" aria-label="Cancel creation" title="Cancel" @click="closeDialog">
            <X :size="16" aria-hidden="true" />
            <span>Cancel</span>
          </button>
        </div>
      </div>
    </template>
  </FixedChromeDialog>
</template>

<script setup lang="ts">
import { Check, X } from '@lucide/vue';
import { ref, watch } from 'vue';
import FixedChromeDialog from '../../shared/components/FixedChromeDialog.vue';
import FormValidation from '../../shared/components/FormValidation.vue';
import FormField from '../../shared/components/FormField.vue';
import { useValidationStore } from '../../shared/stores/validationStore';
import { err } from '../../shared/types/result';
import type { CreateManagedUserRequest } from '../../shared/types/authTypes';
import { validatePasswordConfirmation } from '../../shared/utils/passwordConfirmation';

const props = defineProps<{
  open: boolean;
  busy: boolean;
}>();

const emit = defineEmits<{
  close: [];
  submit: [payload: CreateManagedUserRequest];
}>();

const draft = ref<CreateManagedUserRequest>(createDefaultDraft());
const confirmPassword = ref('');
const validation = useValidationStore();

function closeDialog() {
  if (!props.busy) {
    emit('close');
  }
}

function createDefaultDraft(): CreateManagedUserRequest {
  return {
    userName: '',
    displayName: '',
    email: '',
    password: '',
    role: 'Standard'
  };
}

function resetDraft() {
  draft.value = createDefaultDraft();
  confirmPassword.value = '';
}

function submit() {
  if (props.busy) {
    return;
  }
  draft.value.userName = draft.value.userName.trim();
  draft.value.displayName = draft.value.displayName.trim();
  draft.value.email = draft.value.email.trim();

  const errors: Record<string, string[]> = {};
  const atIndex = draft.value.email.indexOf('@');
  if (atIndex <= 0 || atIndex !== draft.value.email.lastIndexOf('@') || atIndex >= draft.value.email.length - 1) {
    errors.email = ["Email must contain '@' with characters before and after it."];
  }

  const passwordError = validatePasswordConfirmation(draft.value.password, confirmPassword.value);
  if (passwordError) {
    errors.confirmPassword = [passwordError];
  }

  if (!draft.value.displayName) {
    errors.displayName = ['Display name is required.'];
  }

  if (Object.keys(errors).length > 0) {
    validation.setValidationResult(err({ message: 'Validation failed.', validationErrors: errors }));
    return;
  }

  emit('submit', { ...draft.value });
}

watch(
  () => props.open,
  isOpen => {
    if (isOpen) {
      resetDraft();
    }
  },
  { immediate: true }
);
</script>
