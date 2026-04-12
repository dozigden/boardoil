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
        <button
          type="button"
          class="entity-row-main entity-row-main-button"
          :disabled="busy"
          :aria-label="`User actions for ${user.userName}`"
          @click="openUserActions(user.id)"
        >
          <span class="badge">#{{ user.id }}</span>
          <strong class="entity-row-title">{{ user.userName }}</strong>
          <span class="entity-row-badges badge-group">
            <span class="badge">{{ user.identityType }}</span>
            <span class="badge">{{ user.role }}</span>
            <span class="badge">{{ user.isActive ? 'Active' : 'Inactive' }}</span>
          </span>
        </button>
        <div class="entity-row-actions">
          <BoDropdown
            align="right"
            icon-only
            label="User actions"
            :icon="MoreVertical"
            :disabled="busy"
            :open="openUserMenuId === user.id"
            @update:open="setUserMenuOpen(user.id, $event)"
          >
            <template #default="{ close }">
              <button
                type="button"
                class="bo-dropdown-item"
                :disabled="busy || user.role === 'Admin'"
                @click="setRoleFromMenu(user.id, 'Admin', close)"
              >
                Set role: Admin
              </button>
              <button
                type="button"
                class="bo-dropdown-item"
                :disabled="busy || user.role === 'Standard'"
                @click="setRoleFromMenu(user.id, 'Standard', close)"
              >
                Set role: Standard
              </button>
              <span class="bo-dropdown-divider" aria-hidden="true"></span>
              <button type="button" class="bo-dropdown-item" :disabled="busy" @click="toggleStatusFromMenu(user.id, user.isActive, close)">
                {{ user.isActive ? 'Deactivate' : 'Activate' }}
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
  </section>
</template>

<script setup lang="ts">
import { MoreVertical } from 'lucide-vue-next';
import { storeToRefs } from 'pinia';
import { onMounted, ref } from 'vue';
import { createSystemApi } from '../api/systemApi';
import BoDropdown from '../components/BoDropdown.vue';
import UserCreateDialog from '../components/UserCreateDialog.vue';
import { useAuthStore } from '../stores/authStore';
import type { ManagedUser } from '../types/authTypes';

const systemApi = createSystemApi();
const authStore = useAuthStore();
const { user: currentUser } = storeToRefs(authStore);
const users = ref<ManagedUser[]>([]);
const busy = ref(false);
const errorMessage = ref<string | null>(null);
const successMessage = ref<string | null>(null);
const isCreateDialogOpen = ref(false);
const openUserMenuId = ref<number | null>(null);

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

async function createUser(payload: { userName: string; password: string; role: 'Admin' | 'Standard' }) {
  busy.value = true;
  errorMessage.value = null;
  successMessage.value = null;
  try {
    const result = await systemApi.createUser(payload.userName, payload.password, payload.role);
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

async function onRoleChange(userId: number, nextRole: 'Admin' | 'Standard') {
  busy.value = true;
  errorMessage.value = null;
  successMessage.value = null;
  try {
    const result = await systemApi.updateUserRole(userId, nextRole);
    if (!result.ok) {
      errorMessage.value = result.error.message;
      return;
    }

    users.value = users.value.map(user => (user.id === userId ? result.data : user));
    successMessage.value = `Updated role for ${result.data.userName}.`;
  } finally {
    busy.value = false;
  }
}

async function toggleStatus(userId: number, currentState: boolean) {
  busy.value = true;
  errorMessage.value = null;
  successMessage.value = null;
  try {
    const result = await systemApi.updateUserStatus(userId, !currentState);
    if (!result.ok) {
      errorMessage.value = result.error.message;
      return;
    }

    users.value = users.value.map(user => (user.id === userId ? result.data : user));
    successMessage.value = `${result.data.userName} is now ${result.data.isActive ? 'active' : 'inactive'}.`;
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

function openUserActions(userId: number) {
  if (busy.value) {
    return;
  }

  openUserMenuId.value = openUserMenuId.value === userId ? null : userId;
}

function setUserMenuOpen(userId: number, isOpen: boolean) {
  if (isOpen) {
    openUserMenuId.value = userId;
    return;
  }

  if (openUserMenuId.value === userId) {
    openUserMenuId.value = null;
  }
}

function isCurrentUser(userId: number) {
  return currentUser.value?.id === userId;
}

async function setRoleFromMenu(userId: number, nextRole: 'Admin' | 'Standard', close: () => void) {
  close();
  await onRoleChange(userId, nextRole);
}

async function toggleStatusFromMenu(userId: number, currentState: boolean, close: () => void) {
  close();
  await toggleStatus(userId, currentState);
}

async function deleteUserFromMenu(user: ManagedUser, close: () => void) {
  close();
  await deleteUser(user);
}

onMounted(async () => {
  await loadUsers();
});
</script>

<style scoped>
.users-header {
  align-items: flex-end;
}
</style>
