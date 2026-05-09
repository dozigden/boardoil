<template>
  <section class="entity-rows-page entity-rows-page--compact users-page">
    <header class="entity-rows-header users-header">
      <div class="entity-rows-header-copy">
        <h2>User Management</h2>
      </div>
      <button type="button" class="btn" :disabled="busy" @click="openCreateDialog">Create user</button>
    </header>

    <p v-if="errorMessage" class="error">{{ errorMessage }}</p>
    <p v-if="successMessage" class="success">{{ successMessage }}</p>

    <section class="users-grid-wrap">
      <BoGrid
        class="users-grid"
        :columns="gridFields"
        :items="users"
        :is-loading="busy"
        empty-text="No users found."
        sticky-header="100%"
        :total-count="users.length"
        :offset="0"
        :limit="users.length > 0 ? users.length : 1"
        :show-pagination-controls="false"
      >
        <template #cell(id)="{ row }">
          <button
            type="button"
            class="users-id-link users-cell-text"
            :disabled="busy"
            @click="openEditDialog(asManagedUser(row))"
          >
            #{{ row.id }}
          </button>
        </template>
        <template #cell(displayName)="{ row }">
          <span class="users-row-leading">
            <UserAvatar
              :image-relative-path="String(row.profileImageRelativePath ?? '') || null"
              :display-name="String(row.displayName ?? '')"
              size="lg"
              class="user-row-avatar"
            />
            <span class="users-cell-text">{{ row.displayName }}</span>
          </span>
        </template>
        <template #cell(userName)="{ row }">
          <span class="users-cell-text">{{ row.userName }}</span>
        </template>
        <template #cell(email)="{ row }">
          <span class="users-cell-text">{{ row.email }}</span>
        </template>
        <template #cell(role)="{ row }">
          <span class="users-cell-text">{{ row.role }}</span>
        </template>
        <template #cell(isActive)="{ row }">
          <span class="users-cell-text">{{ row.isActive ? 'Active' : 'Inactive' }}</span>
        </template>
        <template #cell(actions)="{ row }">
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
                @click="openEditUserFromMenu(asManagedUser(row), close)"
              >
                Edit details
              </button>
              <span class="bo-dropdown-divider" aria-hidden="true"></span>
              <button
                type="button"
                class="bo-dropdown-item"
                :disabled="busy"
                @click="openResetPasswordFromMenu(asManagedUser(row), close)"
              >
                Reset password
              </button>
              <span class="bo-dropdown-divider" aria-hidden="true"></span>
              <button
                type="button"
                class="bo-dropdown-item"
                :disabled="busy || isCurrentUser(Number(row.id))"
                @click="deleteUserFromMenu(asManagedUser(row), close)"
              >
                Delete
              </button>
            </template>
          </BoDropdown>
        </template>
      </BoGrid>
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
import BoDropdown from '../../shared/components/BoDropdown.vue';
import BoGrid from '../../shared/components/BoGrid.vue';
import PasswordResetDialog from '../../shared/components/PasswordResetDialog.vue';
import UserAvatar from '../../shared/components/UserAvatar.vue';
import { useConfirm } from '../../shared/composables/useConfirm';
import UserCreateDialog from '../components/UserCreateDialog.vue';
import UserEditDialog from '../components/UserEditDialog.vue';
import { useAuthStore } from '../../shared/stores/authStore';
import type { ManagedUser } from '../../shared/types/authTypes';
import { useSystemUsersManagerStore } from '../stores/systemUsersManagerStore';

const authStore = useAuthStore();
const usersManagerStore = useSystemUsersManagerStore();
const { confirm } = useConfirm();
const { user: currentUser } = storeToRefs(authStore);
const { users, busy, errorMessage, successMessage } = storeToRefs(usersManagerStore);
const isCreateDialogOpen = ref(false);
const isEditDialogOpen = ref(false);
const isResetPasswordDialogOpen = ref(false);
const userForEdit = ref<ManagedUser | null>(null);
const userForPasswordReset = ref<ManagedUser | null>(null);
const gridFields: Array<{
  key: string;
  label: string;
  rowKeyColumn?: boolean;
  width?: string;
  align?: 'end';
}> = [
  { key: 'id', label: 'Id', rowKeyColumn: true, width: '5.5rem' },
  { key: 'displayName', label: 'Display Name', width: '17rem' },
  { key: 'userName', label: 'User Name', width: '10rem' },
  { key: 'email', label: 'Email' },
  { key: 'role', label: 'Role', width: '8rem' },
  { key: 'isActive', label: 'Status', width: '8rem' },
  { key: 'actions', label: '', width: '4.5rem', align: 'end' }
];

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

async function createUser(payload: { userName: string; displayName: string; email: string; password: string; role: 'Admin' | 'Standard' }) {
  const created = await usersManagerStore.createUser(payload);
  if (!created) {
    return;
  }

  isCreateDialogOpen.value = false;
}

function openEditDialog(user: ManagedUser) {
  userForEdit.value = user;
  isEditDialogOpen.value = true;
}

function closeEditDialog() {
  isEditDialogOpen.value = false;
  userForEdit.value = null;
}

async function submitUserEdit(payload: { displayName: string; email: string; role: 'Admin' | 'Standard'; isActive: boolean }) {
  const selectedUser = userForEdit.value;
  if (!selectedUser) {
    return;
  }

  const updated = await usersManagerStore.updateUser(selectedUser.id, payload);
  if (!updated) {
    return;
  }

  closeEditDialog();
}

async function submitResetPassword(payload: { currentPassword?: string; newPassword: string }) {
  const selectedUser = userForPasswordReset.value;
  if (!selectedUser) {
    return;
  }

  const reset = await usersManagerStore.resetUserPassword(selectedUser.id, selectedUser.displayName, payload.newPassword);
  if (!reset) {
    return;
  }

  isResetPasswordDialogOpen.value = false;
  userForPasswordReset.value = null;
}

async function deleteUser(user: ManagedUser) {
  if (isCurrentUser(user.id)) {
    errorMessage.value = 'You cannot delete your own account.';
    return;
  }

  const confirmed = await confirm({
    title: 'Delete user',
    message: `Delete user "${user.displayName}"? This removes their access and cannot be undone.`,
    confirmLabel: 'Delete',
    danger: true
  });
  if (!confirmed) {
    return;
  }

  await usersManagerStore.deleteUser(user.id, user.displayName);
}

function isCurrentUser(userId: number) {
  return currentUser.value?.id === userId;
}

function asManagedUser(row: Record<string, unknown>): ManagedUser {
  return row as ManagedUser;
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
  usersManagerStore.clearMessages();
  await usersManagerStore.loadUsers();
});
</script>

<style scoped>
.users-header {
  align-items: flex-end;
}

.users-page {
  height: 100%;
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 0;
  overflow: hidden;
}

.users-grid-wrap {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  overflow: hidden;
}

.users-grid {
  height: 100%;
  min-height: 0;
}

.users-row-leading {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
}

.user-row-avatar {
  flex-shrink: 0;
}

.users-cell-text {
  display: block;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.users-id-link {
  background: transparent;
  border: none;
  padding: 0;
  color: var(--bo-link);
  cursor: pointer;
  text-decoration: underline;
  text-underline-offset: 0.12em;
}

.users-id-link:disabled {
  cursor: default;
  color: var(--bo-ink-muted);
  text-decoration: none;
}

</style>
