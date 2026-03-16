<template>
  <section class="auth-view">
    <form class="auth-card" @submit.prevent="submit">
      <h2>Sign in</h2>
      <label>
        Username
        <input v-model="userName" autocomplete="username" maxlength="64" required />
      </label>
      <label>
        Password
        <input v-model="password" type="password" autocomplete="current-password" required />
      </label>
      <p v-if="errorMessage" class="error">{{ errorMessage }}</p>
      <button type="submit" :disabled="busy">Login</button>
    </form>
  </section>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import { storeToRefs } from 'pinia';
import { useRouter } from 'vue-router';
import { useAuthStore } from '../stores/authStore';

const router = useRouter();
const authStore = useAuthStore();
const { busy, errorMessage } = storeToRefs(authStore);
const userName = ref('');
const password = ref('');

async function submit() {
  const success = await authStore.login(userName.value, password.value);
  if (!success) {
    return;
  }

  await router.replace({ name: 'board' });
}
</script>
