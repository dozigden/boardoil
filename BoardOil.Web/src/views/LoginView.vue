<template>
  <section class="auth-view">
    <form class="auth-card panel panel--strong" @submit.prevent="submit">
      <h2>Sign in</h2>
      <p v-if="requiresInitialAdminSetup" class="auth-help">
        First time setup? <RouterLink :to="{ name: 'setup-initial-admin' }">Create the initial admin</RouterLink>.
      </p>
      <p v-if="passwordResetSuccessMessage" class="success">{{ passwordResetSuccessMessage }}</p>
      <label>
        Username
        <input v-model="userName" autocomplete="username" maxlength="64" required />
      </label>
      <label>
        Password
        <input v-model="password" type="password" autocomplete="current-password" required />
      </label>
      <p v-if="errorMessage" class="error">{{ errorMessage }}</p>
      <button type="submit" class="btn" :disabled="busy">Login</button>
    </form>
  </section>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import { storeToRefs } from 'pinia';
import { RouterLink, useRoute, useRouter } from 'vue-router';
import { useAuthStore } from '../stores/authStore';

const router = useRouter();
const route = useRoute();
const authStore = useAuthStore();
const { busy, errorMessage, requiresInitialAdminSetup } = storeToRefs(authStore);
const userName = ref('');
const password = ref('');
const passwordResetSuccessMessage = route.query.passwordReset === '1' ? 'Password reset successful. Please sign in again.' : null;

async function submit() {
  const success = await authStore.login(userName.value, password.value);
  if (!success) {
    return;
  }

  await router.replace({ name: 'boards' });
}
</script>
