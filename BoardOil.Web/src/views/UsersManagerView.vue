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
          <span class="entity-row-badges badge-group">
            <span class="badge">{{ user.role }}</span>
            <span class="badge">{{ user.isActive ? 'Active' : 'Inactive' }}</span>
          </span>
        </div>
        <div class="entity-row-actions">
          <select :value="user.role" :disabled="busy" @change="onRoleChange(user.id, ($event.target as HTMLSelectElement).value)">
            <option value="Standard">Standard</option>
            <option value="Admin">Admin</option>
          </select>
          <button type="button" class="btn btn--secondary" :disabled="busy" @click="toggleStatus(user.id, user.isActive)">
            {{ user.isActive ? 'Deactivate' : 'Activate' }}
          </button>
        </div>
      </article>
    </section>

    <UserCreateDialog :open="isCreateDialogOpen" :busy="busy" @close="closeCreateDialog" @submit="createUser" />
  </section>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { createAuthApi } from '../api/authApi';
import UserCreateDialog from '../components/UserCreateDialog.vue';
import type { ManagedUser } from '../types/authTypes';

const authApi = createAuthApi();
const users = ref<ManagedUser[]>([]);
const busy = ref(false);
const errorMessage = ref<string | null>(null);
const successMessage = ref<string | null>(null);
const isCreateDialogOpen = ref(false);

async function loadUsers() {
  busy.value = true;
  errorMessage.value = null;
  try {
    const result = await authApi.getUsers();
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
    const result = await authApi.createUser(payload.userName, payload.password, payload.role);
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

async function onRoleChange(userId: number, nextRole: string) {
  if (nextRole !== 'Admin' && nextRole !== 'Standard') {
    return;
  }

  busy.value = true;
  errorMessage.value = null;
  successMessage.value = null;
  try {
    const result = await authApi.updateUserRole(userId, nextRole);
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
    const result = await authApi.updateUserStatus(userId, !currentState);
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

onMounted(async () => {
  await loadUsers();
});
</script>

<style scoped>
.users-header {
  align-items: flex-end;
}
</style>
