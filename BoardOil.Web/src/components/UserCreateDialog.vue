<template>
  <ModalDialog :open="open" title="Create User" close-label="Cancel creation" @close="emit('close')" @submit="submit">
    <label>
      Username
      <input v-model="userName" :disabled="busy" maxlength="64" required />
    </label>

    <label>
      Password
      <input v-model="password" :disabled="busy" type="password" required />
    </label>

    <label>
      Role
      <select v-model="role" :disabled="busy">
        <option value="Standard">Standard</option>
        <option value="Admin">Admin</option>
      </select>
    </label>

    <template #actions>
      <div class="editor-actions card-modal-actions">
        <div class="card-modal-actions-left">
          <button type="submit" class="card-modal-save" :disabled="busy" aria-label="Create user" title="Create user">
            <Check :size="16" aria-hidden="true" />
            <span>Create user</span>
          </button>
          <button type="button" class="ghost card-modal-cancel" :disabled="busy" aria-label="Cancel creation" title="Cancel" @click="emit('close')">
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
import ModalDialog from './ModalDialog.vue';

const props = defineProps<{
  open: boolean;
  busy: boolean;
}>();

const emit = defineEmits<{
  close: [];
  submit: [payload: { userName: string; password: string; role: 'Admin' | 'Standard' }];
}>();

const userName = ref('');
const password = ref('');
const role = ref<'Admin' | 'Standard'>('Standard');

function resetDraft() {
  userName.value = '';
  password.value = '';
  role.value = 'Standard';
}

function submit() {
  emit('submit', {
    userName: userName.value,
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
</script>
