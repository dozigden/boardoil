<template>
  <ModalDialog :open="open" title="Add Member" close-label="Cancel add member" @close="emit('close')" @submit="submit">
    <label>
      User
      <select v-model="selectedUserIdText" :disabled="busy || users.length === 0" required>
        <option value="" disabled>
          {{ users.length === 0 ? 'No users available' : 'Select a user' }}
        </option>
        <option v-for="user in users" :key="user.id" :value="String(user.id)">
          {{ user.userName }} (#{{ user.id }}){{ user.isActive ? '' : ' - Inactive' }}
        </option>
      </select>
    </label>

    <label>
      Role
      <select v-model="role" :disabled="busy">
        <option value="Contributor">Contributor</option>
        <option value="Owner">Owner</option>
      </select>
    </label>

    <template #actions>
      <div class="editor-actions card-modal-actions">
        <div class="card-modal-actions-left">
          <button type="submit" class="btn" :disabled="busy || !isValidUserId" aria-label="Add member" title="Add member">
            <Check :size="16" aria-hidden="true" />
            <span>Add member</span>
          </button>
          <button type="button" class="btn btn--secondary" :disabled="busy" aria-label="Cancel add member" title="Cancel" @click="emit('close')">
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
import { computed, ref, watch } from 'vue';
import type { UserDirectoryEntry } from '../../shared/types/authTypes';
import type { BoardMemberRole } from '../../shared/types/boardTypes';
import ModalDialog from '../../shared/components/ModalDialog.vue';

const props = defineProps<{
  open: boolean;
  busy: boolean;
  users: UserDirectoryEntry[];
}>();

const emit = defineEmits<{
  close: [];
  submit: [payload: { userId: number; role: BoardMemberRole }];
}>();

const selectedUserIdText = ref('');
const role = ref<BoardMemberRole>('Contributor');

const parsedUserId = computed(() => Number.parseInt(selectedUserIdText.value, 10));
const isValidUserId = computed(() => Number.isFinite(parsedUserId.value) && parsedUserId.value > 0);

function submit() {
  if (!isValidUserId.value) {
    return;
  }

  emit('submit', {
    userId: parsedUserId.value,
    role: role.value
  });
}

watch(
  () => [props.open, props.users] as const,
  isOpen => {
    if (!isOpen[0]) {
      return;
    }

    selectedUserIdText.value = isOpen[1].length > 0 ? String(isOpen[1][0].id) : '';
    role.value = 'Contributor';
  },
  { deep: true }
);
</script>
