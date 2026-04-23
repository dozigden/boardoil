<template>
  <ModalDialog :open="open" title="Edit Client Account" close-label="Cancel client changes" @close="emit('close')" @submit="submit">
    <label>
      Username
      <input :value="draftUserName" disabled />
    </label>

    <label>
      Email
      <input v-model="draftEmail" :disabled="busy" type="email" autocomplete="email" maxlength="320" required />
    </label>

    <label>
      Role
      <select v-model="draftRole" :disabled="busy">
        <option value="Standard">Standard</option>
        <option value="Admin">Admin</option>
      </select>
    </label>

    <label class="client-edit-dialog-check">
      <input v-model="draftIsActive" :disabled="busy" type="checkbox" />
      <span>Active account</span>
    </label>

    <p v-if="draftError" class="error">{{ draftError }}</p>

    <template #actions>
      <div class="editor-actions card-modal-actions">
        <div class="card-modal-actions-left">
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
  </ModalDialog>
</template>

<script setup lang="ts">
import { Check, X } from 'lucide-vue-next';
import { ref, watch } from 'vue';
import ModalDialog from '../../shared/components/ModalDialog.vue';
import type { ClientAccount } from '../../shared/types/authTypes';

const props = defineProps<{
  open: boolean;
  busy: boolean;
  client: ClientAccount | null;
}>();

const emit = defineEmits<{
  close: [];
  submit: [payload: { email: string; role: 'Admin' | 'Standard'; isActive: boolean }];
}>();

const draftUserName = ref('');
const draftEmail = ref('');
const draftRole = ref<'Admin' | 'Standard'>('Standard');
const draftIsActive = ref(true);
const draftError = ref<string | null>(null);

function resetDraft() {
  draftUserName.value = props.client?.userName ?? '';
  draftEmail.value = props.client?.email ?? '';
  draftRole.value = props.client?.role === 'Admin' ? 'Admin' : 'Standard';
  draftIsActive.value = props.client?.isActive ?? true;
  draftError.value = null;
}

function submit() {
  const trimmedEmail = draftEmail.value.trim();
  const atIndex = trimmedEmail.indexOf('@');
  if (atIndex <= 0 || atIndex !== trimmedEmail.lastIndexOf('@') || atIndex >= trimmedEmail.length - 1) {
    draftError.value = "Email must contain '@' with characters before and after it.";
    return;
  }

  emit('submit', {
    email: trimmedEmail,
    role: draftRole.value,
    isActive: draftIsActive.value
  });
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
