<template>
  <ModalDialog :open="open" title="Create User" close-label="Cancel creation" @close="emit('close')" @submit="submit">
    <label>
      Username
      <input v-model="userName" :disabled="busy" maxlength="64" required />
    </label>

    <label>
      Email
      <input v-model="email" :disabled="busy" type="email" autocomplete="email" maxlength="320" required />
    </label>

    <label>
      Password
      <input v-model="password" :disabled="busy" type="password" autocomplete="new-password" required />
    </label>

    <label>
      Confirm password
      <input v-model="confirmPassword" :disabled="busy" type="password" autocomplete="new-password" required />
    </label>

    <label>
      Role
      <select v-model="role" :disabled="busy">
        <option value="Standard">Standard</option>
        <option value="Admin">Admin</option>
      </select>
    </label>

    <p v-if="draftError" class="error">{{ draftError }}</p>

    <template #actions>
      <div class="editor-actions card-modal-actions">
        <div class="card-modal-actions-left">
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
  </ModalDialog>
</template>

<script setup lang="ts">
import { Check, X } from 'lucide-vue-next';
import { ref, watch } from 'vue';
import ModalDialog from '../../shared/components/ModalDialog.vue';
import { PASSWORD_CONFIRMATION_ERROR, validatePasswordConfirmation } from '../../shared/utils/passwordConfirmation';

const props = defineProps<{
  open: boolean;
  busy: boolean;
}>();

const emit = defineEmits<{
  close: [];
  submit: [payload: { userName: string; email: string; password: string; role: 'Admin' | 'Standard' }];
}>();

const userName = ref('');
const email = ref('');
const password = ref('');
const confirmPassword = ref('');
const role = ref<'Admin' | 'Standard'>('Standard');
const draftError = ref<string | null>(null);

function resetDraft() {
  userName.value = '';
  email.value = '';
  password.value = '';
  confirmPassword.value = '';
  role.value = 'Standard';
  draftError.value = null;
}

function submit() {
  const trimmedEmail = email.value.trim();
  const atIndex = trimmedEmail.indexOf('@');
  if (atIndex <= 0 || atIndex !== trimmedEmail.lastIndexOf('@') || atIndex >= trimmedEmail.length - 1) {
    draftError.value = "Email must contain '@' with characters before and after it.";
    return;
  }

  draftError.value = validatePasswordConfirmation(password.value, confirmPassword.value);
  if (draftError.value) {
    return;
  }

  emit('submit', {
    userName: userName.value,
    email: trimmedEmail,
    password: password.value,
    role: role.value
  });
}

watch(
  () => props.open,
  isOpen => {
    if (isOpen) {
      resetDraft();
    }
  }
);

watch([password, confirmPassword], () => {
  if (draftError.value === PASSWORD_CONFIRMATION_ERROR && validatePasswordConfirmation(password.value, confirmPassword.value) === null) {
    draftError.value = null;
  }
});
</script>
