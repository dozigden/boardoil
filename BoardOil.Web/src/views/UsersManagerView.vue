<template>
  <section class="users-manager">
    <header class="users-header">
      <h2>User Management</h2>
      <p>Create and manage local BoardOil accounts.</p>
    </header>

    <form class="users-create" @submit.prevent="createUser">
      <h3>Create User</h3>
      <label>
        Username
        <input v-model="newUserName" maxlength="64" required />
      </label>
      <label>
        Password
        <input v-model="newPassword" type="password" required />
      </label>
      <label>
        Role
        <select v-model="newRole">
          <option value="Standard">Standard</option>
          <option value="Admin">Admin</option>
        </select>
      </label>
      <button type="submit" :disabled="busy">Create user</button>
    </form>

    <p v-if="errorMessage" class="error">{{ errorMessage }}</p>
    <p v-if="successMessage" class="success">{{ successMessage }}</p>

    <section class="users-list">
      <article v-for="user in users" :key="user.id" class="users-item">
        <div class="users-item-meta">
          <strong>{{ user.userName }}</strong>
          <span class="users-badges">
            <span class="card-id">{{ user.role }}</span>
            <span class="card-id">{{ user.isActive ? 'Active' : 'Inactive' }}</span>
          </span>
        </div>
        <div class="users-item-actions">
          <select :value="user.role" :disabled="busy" @change="onRoleChange(user.id, ($event.target as HTMLSelectElement).value)">
            <option value="Standard">Standard</option>
            <option value="Admin">Admin</option>
          </select>
          <button type="button" class="ghost" :disabled="busy" @click="toggleStatus(user.id, user.isActive)">
            {{ user.isActive ? 'Deactivate' : 'Activate' }}
          </button>
        </div>
      </article>
    </section>
  </section>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { createAuthApi } from '../api/authApi';
import type { ManagedUser } from '../types/authTypes';

const authApi = createAuthApi();
const users = ref<ManagedUser[]>([]);
const busy = ref(false);
const errorMessage = ref<string | null>(null);
const successMessage = ref<string | null>(null);
const newUserName = ref('');
const newPassword = ref('');
const newRole = ref<'Admin' | 'Standard'>('Standard');

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

async function createUser() {
  busy.value = true;
  errorMessage.value = null;
  successMessage.value = null;
  try {
    const result = await authApi.createUser(newUserName.value, newPassword.value, newRole.value);
    if (!result.ok) {
      errorMessage.value = result.error.message;
      return;
    }

    users.value = [...users.value, result.data].sort((a, b) => a.userName.localeCompare(b.userName));
    newUserName.value = '';
    newPassword.value = '';
    newRole.value = 'Standard';
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
