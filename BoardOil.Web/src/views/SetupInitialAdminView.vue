<template>
  <section class="auth-view">
    <form class="auth-card" @submit.prevent="submit">
      <h2>Create Initial Admin</h2>
      <p class="auth-help">
        This works only when there are no users yet. After setup, this account is signed in immediately.
      </p>
      <label>
        Username
        <input v-model="userName" autocomplete="username" maxlength="64" required />
      </label>
      <label>
        Password
        <input v-model="password" type="password" autocomplete="new-password" minlength="8" required />
      </label>
      <p v-if="errorMessage" class="error">{{ errorMessage }}</p>
      <button type="submit" :disabled="busy">Create admin</button>
      <RouterLink class="auth-link" :to="{ name: 'login' }">Back to sign in</RouterLink>
    </form>
  </section>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import { storeToRefs } from 'pinia';
import { RouterLink, useRouter } from 'vue-router';
import { useAuthStore } from '../stores/authStore';

const router = useRouter();
const authStore = useAuthStore();
const { busy, errorMessage } = storeToRefs(authStore);
const userName = ref('');
const password = ref('');

async function submit() {
  const success = await authStore.registerInitialAdmin(userName.value, password.value);
  if (!success) {
    return;
  }

  await router.replace({ name: 'board' });
}
</script>
