<template>
  <FixedChromeDialog :open="open" title="Create User" close-label="Cancel creation" @close="emit('close')" @submit="submit">
    <label>
      Username
      <input v-model="draft.userName" :disabled="busy" maxlength="64" required />
    </label>

    <label>
      Display name
      <input v-model="draft.displayName" :disabled="busy" maxlength="64" required />
    </label>

    <label>
      Email
      <input v-model="draft.email" :disabled="busy" type="email" autocomplete="email" maxlength="320" required />
    </label>

    <label>
      Password
      <input v-model="draft.password" :disabled="busy" type="password" autocomplete="new-password" required />
    </label>

    <label>
      Confirm password
      <input v-model="confirmPassword" :disabled="busy" type="password" autocomplete="new-password" required />
    </label>

    <label>
      Role
      <select v-model="draft.role" :disabled="busy">
        <option value="Standard">Standard</option>
        <option value="Admin">Admin</option>
      </select>
    </label>

    <p v-if="draftError" class="error">{{ draftError }}</p>

    <template #actions>
      <div class="fixed-chrome-dialog-actions">
        <div class="fixed-chrome-dialog-actions-left">
          <button type="submit" class="btn" :disabled="busy" aria-label="Create user" title="Create user">
            <Check :size="16" aria-hidden="true" />
            <span>Create user</span>
          </button>
          <button type="button" class="btn btn--secondary" :disabled="busy" aria-label="Cancel creation" title="Cancel" @click="emit('close')">
            <X :size="16" aria-hidden="true" />
            <span>Cancel</span>
          </button>
        </div>
      </div>
    </template>
  </FixedChromeDialog>
</template>

<script setup lang="ts">
import { Check, X } from 'lucide-vue-next';
import { ref, watch } from 'vue';
import FixedChromeDialog from '../../shared/components/FixedChromeDialog.vue';
import type { CreateManagedUserRequest } from '../../shared/types/authTypes';
import { PASSWORD_CONFIRMATION_ERROR, validatePasswordConfirmation } from '../../shared/utils/passwordConfirmation';

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
const draftError = ref<string | null>(null);

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
  draftError.value = null;
}

function submit() {
  draft.value.userName = draft.value.userName.trim();
  draft.value.displayName = draft.value.displayName.trim();
  draft.value.email = draft.value.email.trim();

  const atIndex = draft.value.email.indexOf('@');
  if (atIndex <= 0 || atIndex !== draft.value.email.lastIndexOf('@') || atIndex >= draft.value.email.length - 1) {
    draftError.value = "Email must contain '@' with characters before and after it.";
    return;
  }

  draftError.value = validatePasswordConfirmation(draft.value.password, confirmPassword.value);
  if (draftError.value) {
    return;
  }

  if (!draft.value.displayName) {
    draftError.value = 'Display name is required.';
    return;
  }

  emit('submit', draft.value);
}

watch(
  () => props.open,
  isOpen => {
    if (isOpen) {
      resetDraft();
    }
  }
);

watch(
  () => [draft.value.password, confirmPassword.value] as const,
  ([password]) => {
    if (draftError.value === PASSWORD_CONFIRMATION_ERROR && validatePasswordConfirmation(password, confirmPassword.value) === null) {
      draftError.value = null;
    }
  }
);
</script>
