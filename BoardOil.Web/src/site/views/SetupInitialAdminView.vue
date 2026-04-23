<template>
  <section class="auth-view">
    <form class="auth-card panel panel--strong" @submit.prevent="submit">
      <h2>Create Initial Admin</h2>
      <p class="auth-help">
        This works only when there are no users yet. After setup, this account is signed in immediately.
      </p>
      <label>
        Username
        <input v-model="userName" autocomplete="username" maxlength="64" required />
      </label>
      <label>
        Email
        <input v-model="email" autocomplete="email" maxlength="320" required />
      </label>
      <label>
        Password
        <input v-model="password" type="password" autocomplete="new-password" minlength="8" required />
      </label>
      <label>
        Confirm password
        <input v-model="confirmPassword" type="password" autocomplete="new-password" minlength="8" required />
      </label>
      <p v-if="displayedErrorMessage" class="error">{{ displayedErrorMessage }}</p>
      <button type="submit" class="btn" :disabled="busy">Create admin</button>
      <RouterLink class="auth-link" :to="{ name: 'login' }">Back to sign in</RouterLink>
    </form>
  </section>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { storeToRefs } from 'pinia';
import { RouterLink, useRouter } from 'vue-router';
import { useAuthStore } from '../../shared/stores/authStore';
import { PASSWORD_CONFIRMATION_ERROR, validatePasswordConfirmation } from '../../shared/utils/passwordConfirmation';

const router = useRouter();
const authStore = useAuthStore();
const { busy, errorMessage } = storeToRefs(authStore);
const userName = ref('');
const email = ref('');
const password = ref('');
const confirmPassword = ref('');
const formErrorMessage = ref<string | null>(null);
const displayedErrorMessage = computed(() => formErrorMessage.value ?? errorMessage.value);

watch([password, confirmPassword], () => {
  if (formErrorMessage.value === PASSWORD_CONFIRMATION_ERROR && validatePasswordConfirmation(password.value, confirmPassword.value) === null) {
    formErrorMessage.value = null;
  }
});

async function submit() {
  formErrorMessage.value = validateEmail(email.value);
  if (formErrorMessage.value) {
    return;
  }

  formErrorMessage.value = validatePasswordConfirmation(password.value, confirmPassword.value);
  if (formErrorMessage.value) {
    return;
  }

  const success = await authStore.registerInitialAdmin(userName.value, email.value, password.value);
  if (!success) {
    return;
  }

  await router.replace({ name: 'boards' });
}

function validateEmail(emailValue: string): string | null {
  const trimmedEmail = emailValue.trim();
  const atIndex = trimmedEmail.indexOf('@');
  if (atIndex <= 0 || atIndex !== trimmedEmail.lastIndexOf('@') || atIndex >= trimmedEmail.length - 1) {
    return "Email must contain '@' with characters before and after it.";
  }

  return null;
}
</script>
