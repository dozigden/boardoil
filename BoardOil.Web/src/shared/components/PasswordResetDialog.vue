<template>
  <ModalDialog :open="open" :title="dialogTitle" close-label="Cancel password reset" @close="emit('close')" @submit="submit">
    <p class="reset-password-hint">
      {{ dialogHint }}
    </p>

    <label v-if="mode === 'self'">
      Current password
      <input
        v-model="currentPassword"
        :disabled="busy"
        type="password"
        autocomplete="current-password"
        required
      />
    </label>

    <label>
      New password
      <input
        v-model="newPassword"
        :disabled="busy"
        type="password"
        autocomplete="new-password"
        required
      />
    </label>

    <label>
      Confirm new password
      <input
        v-model="confirmPassword"
        :disabled="busy"
        type="password"
        autocomplete="new-password"
        required
      />
    </label>

    <p v-if="draftError" class="error">{{ draftError }}</p>

    <template #actions>
      <div class="editor-actions card-modal-actions">
        <div class="card-modal-actions-left">
          <button type="submit" class="btn" :disabled="busy" aria-label="Reset password" title="Reset password">
            <Check :size="16" aria-hidden="true" />
            <span>Reset password</span>
          </button>
          <button type="button" class="btn btn--secondary" :disabled="busy" aria-label="Cancel password reset" title="Cancel" @click="emit('close')">
            <X :size="16" aria-hidden="true" />
            <span>Cancel</span>
          </button>
        </div>
      </div>
    </template>
  </ModalDialog>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { Check, X } from 'lucide-vue-next';
import ModalDialog from './ModalDialog.vue';
import { PASSWORD_CONFIRMATION_ERROR, validatePasswordConfirmation } from '../utils/passwordConfirmation';
import { validatePasswordResetDraft } from './passwordResetDialogModel';

const props = defineProps<{
  open: boolean;
  busy: boolean;
  mode: 'self' | 'admin';
  targetUserName?: string;
}>();

const emit = defineEmits<{
  close: [];
  submit: [payload: { currentPassword?: string; newPassword: string }];
}>();

const currentPassword = ref('');
const newPassword = ref('');
const confirmPassword = ref('');
const draftError = ref<string | null>(null);

const dialogTitle = computed(() => (props.mode === 'self' ? 'Reset Your Password' : 'Reset User Password'));
const dialogHint = computed(() => {
  if (props.mode === 'self') {
    return 'Enter your current password and set a new one.';
  }

  return props.targetUserName
    ? `Set a new password for ${props.targetUserName}.`
    : 'Set a new password for this user.';
});

function resetDraft() {
  currentPassword.value = '';
  newPassword.value = '';
  confirmPassword.value = '';
  draftError.value = null;
}

function submit() {
  draftError.value = validatePasswordResetDraft(props.mode, currentPassword.value, newPassword.value, confirmPassword.value);
  if (draftError.value) {
    return;
  }

  emit('submit', {
    currentPassword: props.mode === 'self' ? currentPassword.value : undefined,
    newPassword: newPassword.value
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

watch([newPassword, confirmPassword], () => {
  if (draftError.value === PASSWORD_CONFIRMATION_ERROR && validatePasswordConfirmation(newPassword.value, confirmPassword.value) === null) {
    draftError.value = null;
  }
});

watch(currentPassword, () => {
  if (draftError.value === 'Current password is required.' && currentPassword.value.trim()) {
    draftError.value = null;
  }
});
</script>

<style scoped>
.reset-password-hint {
  margin: 0 0 0.75rem;
  color: var(--bo-ink-muted);
}
</style>
