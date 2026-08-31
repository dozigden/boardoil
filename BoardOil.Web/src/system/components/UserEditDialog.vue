<template>
  <FixedChromeDialog :open="open" title="Edit User" close-label="Cancel user changes" @close="emit('close')" @submit="submit">
    <label>
      Username
      <input :value="props.user?.userName ?? ''" disabled />
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
      Role
      <select v-model="draft.role" :disabled="busy">
        <option value="Standard">Standard</option>
        <option value="Admin">Admin</option>
      </select>
    </label>

    <label class="user-edit-dialog-check">
      <input v-model="draft.isActive" :disabled="busy" type="checkbox" />
      <span>Active account</span>
    </label>

    <p v-if="draftError" class="error">{{ draftError }}</p>

    <template #actions>
      <div class="fixed-chrome-dialog-actions">
        <div class="fixed-chrome-dialog-actions-left">
          <button type="submit" class="btn" :disabled="busy" aria-label="Save user changes" title="Save user changes">
            <Check :size="16" aria-hidden="true" />
            <span>Save</span>
          </button>
          <button type="button" class="btn btn--secondary" :disabled="busy" aria-label="Cancel user changes" title="Cancel" @click="emit('close')">
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
import type { ManagedUser, UpdateManagedUserRequest } from '../../shared/types/authTypes';

const props = defineProps<{
  open: boolean;
  busy: boolean;
  user: ManagedUser | null;
}>();

const emit = defineEmits<{
  close: [];
  submit: [payload: UpdateManagedUserRequest];
}>();

const draft = ref<UpdateManagedUserRequest>(createDefaultDraft());
const draftError = ref<string | null>(null);

function createDefaultDraft(): UpdateManagedUserRequest {
  return {
    displayName: '',
    email: '',
    role: 'Standard',
    isActive: true
  };
}

function resetDraft() {
  draft.value = {
    displayName: props.user?.displayName ?? '',
    email: props.user?.email ?? '',
    role: props.user?.role === 'Admin' ? 'Admin' : 'Standard',
    isActive: props.user?.isActive ?? true
  };
  draftError.value = null;
}

function submit() {
  draft.value.displayName = draft.value.displayName.trim();
  draft.value.email = draft.value.email.trim();

  if (!draft.value.displayName) {
    draftError.value = 'Display name is required.';
    return;
  }

  const atIndex = draft.value.email.indexOf('@');
  if (atIndex <= 0 || atIndex !== draft.value.email.lastIndexOf('@') || atIndex >= draft.value.email.length - 1) {
    draftError.value = "Email must contain '@' with characters before and after it.";
    return;
  }

  emit('submit', draft.value);
}

watch(
  () => [props.open, props.user?.id] as const,
  ([isOpen]) => {
    if (isOpen) {
      resetDraft();
    }
  }
);
</script>

<style scoped>
.user-edit-dialog-check {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
}

.user-edit-dialog-check > input {
  width: auto;
  padding: 0;
  margin: 0;
}
</style>
