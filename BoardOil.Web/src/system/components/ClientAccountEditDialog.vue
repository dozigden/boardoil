<template>
  <FixedChromeDialog :open="open" title="Edit Client Account" close-label="Cancel client changes" @close="emit('close')" @submit="submit">
    <label>
      Username
      <input :value="props.client?.userName ?? ''" disabled />
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

    <label class="client-edit-dialog-check">
      <input v-model="draft.isActive" :disabled="busy" type="checkbox" />
      <span>Active account</span>
    </label>

    <p v-if="draftError" class="error">{{ draftError }}</p>

    <template #actions>
      <div class="fixed-chrome-dialog-actions">
        <div class="fixed-chrome-dialog-actions-left">
          <button type="submit" class="btn" :disabled="busy" aria-label="Save client account changes" title="Save client account changes">
            <Check :size="16" aria-hidden="true" />
            <span>Save</span>
          </button>
          <button type="button" class="btn btn--secondary" :disabled="busy" aria-label="Cancel client changes" title="Cancel" @click="emit('close')">
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
import type { ClientAccount, UpdateClientAccountRequest } from '../../shared/types/authTypes';

const props = defineProps<{
  open: boolean;
  busy: boolean;
  client: ClientAccount | null;
}>();

const emit = defineEmits<{
  close: [];
  submit: [payload: UpdateClientAccountRequest];
}>();

const draft = ref<UpdateClientAccountRequest>(createDefaultDraft());
const draftError = ref<string | null>(null);

function createDefaultDraft(): UpdateClientAccountRequest {
  return {
    displayName: '',
    email: '',
    role: 'Standard',
    isActive: true
  };
}

function resetDraft() {
  draft.value = {
    displayName: props.client?.displayName ?? '',
    email: props.client?.email ?? '',
    role: props.client?.role === 'Admin' ? 'Admin' : 'Standard',
    isActive: props.client?.isActive ?? true
  };
  draftError.value = null;
}

function submit() {
  const editModel = draft.value;
  editModel.displayName = editModel.displayName.trim();
  editModel.email = editModel.email.trim();

  if (!editModel.displayName) {
    draftError.value = 'Display name is required.';
    return;
  }

  const atIndex = editModel.email.indexOf('@');
  if (atIndex <= 0 || atIndex !== editModel.email.lastIndexOf('@') || atIndex >= editModel.email.length - 1) {
    draftError.value = "Email must contain '@' with characters before and after it.";
    return;
  }

  emit('submit', editModel);
}

watch(
  () => [props.open, props.client?.id] as const,
  ([isOpen]) => {
    if (isOpen) {
      resetDraft();
    }
  }
);
</script>

<style scoped>
.client-edit-dialog-check {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
}

.client-edit-dialog-check > input {
  width: auto;
  padding: 0;
  margin: 0;
}
</style>
