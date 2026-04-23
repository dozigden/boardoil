<template>
  <section class="entity-rows-page entity-rows-page--compact">
    <header class="entity-rows-header users-header">
      <div class="entity-rows-header-copy">
        <h2>User Management</h2>
        <p>Create and manage local BoardOil accounts.</p>
      </div>
      <button type="button" class="btn" :disabled="busy" @click="openCreateDialog">Create user</button>
    </header>

    <p v-if="errorMessage" class="error">{{ errorMessage }}</p>
    <p v-if="successMessage" class="success">{{ successMessage }}</p>

    <section class="entity-rows-list">
      <article v-for="user in users" :key="user.id" class="entity-row">
        <div class="entity-row-main">
          <span class="badge">#{{ user.id }}</span>
          <strong class="entity-row-title">{{ user.userName }}</strong>
          <span class="user-email">{{ user.email }}</span>
          <span class="entity-row-badges badge-group">
            <span class="badge">{{ user.identityType }}</span>
            <span class="badge">{{ user.role }}</span>
            <span class="badge">{{ user.isActive ? 'Active' : 'Inactive' }}</span>
          </span>
        </div>
        <div class="entity-row-actions">
          <BoDropdown
            align="right"
            icon-only
            label="User actions"
            :icon="MoreVertical"
            :disabled="busy"
          >
            <template #default="{ close }">
              <button
                type="button"
                class="bo-dropdown-item"
                :disabled="busy"
                @click="openEditUserFromMenu(user, close)"
              >
                Edit details
              </button>
              <span class="bo-dropdown-divider" aria-hidden="true"></span>
              <button type="button" class="bo-dropdown-item" :disabled="busy" @click="openResetPasswordFromMenu(user, close)">
                Reset password
              </button>
              <span class="bo-dropdown-divider" aria-hidden="true"></span>
              <button
                type="button"
                class="bo-dropdown-item"
                :disabled="busy || isCurrentUser(user.id)"
                @click="deleteUserFromMenu(user, close)"
              >
                Delete
              </button>
            </template>
          </BoDropdown>
        </div>
      </article>
    </section>

    <UserCreateDialog :open="isCreateDialogOpen" :busy="busy" @close="closeCreateDialog" @submit="createUser" />
    <UserEditDialog
      :open="isEditDialogOpen"
      :busy="busy"
      :user="userForEdit"
      @close="closeEditDialog"
      @submit="submitUserEdit"
    />
    <PasswordResetDialog
      :open="isResetPasswordDialogOpen"
      :busy="busy"
      mode="admin"
      :target-user-name="userForPasswordReset?.userName"
      @close="closeResetPasswordDialog"
      @submit="submitResetPassword"
    />
  </section>
</template>

<script setup lang="ts">
import { MoreVertical } from 'lucide-vue-next';
import { storeToRefs } from 'pinia';
import { onMounted, ref } from 'vue';
import { createSystemApi } from '../../shared/api/systemApi';
import BoDropdown from '../../shared/components/BoDropdown.vue';
import PasswordResetDialog from '../../shared/components/PasswordResetDialog.vue';
import UserCreateDialog from '../components/UserCreateDialog.vue';
import UserEditDialog from '../components/UserEditDialog.vue';
import { useAuthStore } from '../../shared/stores/authStore';
import type { ManagedUser } from '../../shared/types/authTypes';

const systemApi = createSystemApi();
const authStore = useAuthStore();
const { user: currentUser } = storeToRefs(authStore);
const users = ref<ManagedUser[]>([]);
const busy = ref(false);
const errorMessage = ref<string | null>(null);
const successMessage = ref<string | null>(null);
const isCreateDialogOpen = ref(false);
const isEditDialogOpen = ref(false);
const isResetPasswordDialogOpen = ref(false);
const userForEdit = ref<ManagedUser | null>(null);
const userForPasswordReset = ref<ManagedUser | null>(null);

async function loadUsers() {
  busy.value = true;
  errorMessage.value = null;
  try {
    const result = await systemApi.getUsers();
    if (!result.ok) {
      errorMessage.value = result.error.message;
      return;
    }

    users.value = result.data;
  } finally {
    busy.value = false;
  }
}

function openCreateDialog() {
  isCreateDialogOpen.value = true;
}

function closeCreateDialog() {
  isCreateDialogOpen.value = false;
}

function openResetPasswordDialog(user: ManagedUser) {
  userForPasswordReset.value = user;
  isResetPasswordDialogOpen.value = true;
}

function closeResetPasswordDialog() {
  isResetPasswordDialogOpen.value = false;
  userForPasswordReset.value = null;
}

async function createUser(payload: { userName: string; email: string; password: string; role: 'Admin' | 'Standard' }) {
  busy.value = true;
  errorMessage.value = null;
  successMessage.value = null;
  try {
    const result = await systemApi.createUser(payload.userName, payload.email, payload.password, payload.role);
    if (!result.ok) {
      errorMessage.value = result.error.message;
      return;
    }

    users.value = [...users.value, result.data].sort((a, b) => a.userName.localeCompare(b.userName));
    isCreateDialogOpen.value = false;
    successMessage.value = `Created user ${result.data.userName}.`;
  } finally {
    busy.value = false;
  }
}

function openEditDialog(user: ManagedUser) {
  userForEdit.value = user;
  isEditDialogOpen.value = true;
}

function closeEditDialog() {
  isEditDialogOpen.value = false;
  userForEdit.value = null;
}

async function submitUserEdit(payload: { email: string; role: 'Admin' | 'Standard'; isActive: boolean }) {
  const selectedUser = userForEdit.value;
  if (!selectedUser) {
    return;
  }

  busy.value = true;
  errorMessage.value = null;
  successMessage.value = null;
  try {
    const result = await systemApi.updateUser(selectedUser.id, payload);
    if (!result.ok) {
      errorMessage.value = result.error.message;
      return;
    }

    users.value = users.value.map(user => (user.id === selectedUser.id ? result.data : user));
    closeEditDialog();
    successMessage.value = `Updated ${result.data.userName}.`;
  } finally {
    busy.value = false;
  }
}

async function submitResetPassword(payload: { currentPassword?: string; newPassword: string }) {
  const selectedUser = userForPasswordReset.value;
  if (!selectedUser) {
    return;
  }

  busy.value = true;
  errorMessage.value = null;
  successMessage.value = null;
  try {
    const result = await systemApi.resetUserPassword(selectedUser.id, payload.newPassword);
    if (!result.ok) {
      errorMessage.value = result.error.message;
      return;
    }

    isResetPasswordDialogOpen.value = false;
    userForPasswordReset.value = null;
    successMessage.value = `Password reset for ${selectedUser.userName}.`;
  } finally {
    busy.value = false;
  }
}

async function deleteUser(user: ManagedUser) {
  if (isCurrentUser(user.id)) {
    errorMessage.value = 'You cannot delete your own account.';
    return;
  }

  const confirmed = window.confirm(`Delete user "${user.userName}"? This removes their access and cannot be undone.`);
  if (!confirmed) {
    return;
  }

  busy.value = true;
  errorMessage.value = null;
  successMessage.value = null;
  try {
    const result = await systemApi.deleteUser(user.id);
    if (!result.ok) {
      errorMessage.value = result.error.message;
      return;
    }

    users.value = users.value.filter(entry => entry.id !== user.id);
    successMessage.value = `Deleted user ${user.userName}.`;
  } finally {
    busy.value = false;
  }
}

function isCurrentUser(userId: number) {
  return currentUser.value?.id === userId;
}

async function deleteUserFromMenu(user: ManagedUser, close: () => void) {
  close();
  await deleteUser(user);
}

function openEditUserFromMenu(user: ManagedUser, close: () => void) {
  close();
  openEditDialog(user);
}

function openResetPasswordFromMenu(user: ManagedUser, close: () => void) {
  close();
  openResetPasswordDialog(user);
}

onMounted(async () => {
  await loadUsers();
});
</script>

<style scoped>
.users-header {
  align-items: flex-end;
}

.user-email {
  color: var(--bo-ink-muted);
  font-family: "Cascadia Mono", "Consolas", "Liberation Mono", monospace;
}
</style>
