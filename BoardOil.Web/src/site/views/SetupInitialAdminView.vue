<template>
  <section class="auth-view">
    <form class="auth-card panel panel--strong" @submit.prevent="submit">
      <h2>Create Initial Admin</h2>
      <LoadingIndicator v-if="loading" label="Loading setup..." />
      <template v-else-if="loadErrorMessage">
        <p class="error" role="alert">{{ loadErrorMessage }}</p>
        <button type="button" class="btn btn--secondary" @click="loadTimeZoneOptions">Retry</button>
      </template>
      <template v-else>
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
        <div class="initial-admin-timezone">
          <label :for="timeZoneInputId">System timezone</label>
          <SearchableSelect :id="timeZoneInputId" v-model="systemTimeZoneId" label="System timezone"
            :options="timeZoneOptions" :disabled="busy" />
        </div>
        <p v-if="displayedErrorMessage" class="error">{{ displayedErrorMessage }}</p>
        <button type="submit" class="btn" :disabled="busy">{{ busy ? 'Creating admin...' : 'Create admin' }}</button>
      </template>
      <RouterLink class="auth-link" :to="{ name: 'login' }">Back to sign in</RouterLink>
    </form>
  </section>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, useId, watch } from 'vue';
import { storeToRefs } from 'pinia';
import { RouterLink, useRouter } from 'vue-router';
import { useAuthStore } from '../../shared/stores/authStore';
import { createAuthApi } from '../../shared/api/authApi';
import LoadingIndicator from '../../shared/components/LoadingIndicator.vue';
import SearchableSelect from '../../shared/components/SearchableSelect.vue';
import { PASSWORD_CONFIRMATION_ERROR, validatePasswordConfirmation } from '../../shared/utils/passwordConfirmation';

const router = useRouter();
const authStore = useAuthStore();
const authApi = createAuthApi();
const { busy, errorMessage } = storeToRefs(authStore);
const userName = ref('');
const email = ref('');
const password = ref('');
const confirmPassword = ref('');
const timeZoneInputId = `initial-admin-timezone-${useId()}`;
const systemTimeZoneId = ref('');
const timeZoneOptions = ref<{ value: string; label: string }[]>([]);
const loading = ref(true);
const loadErrorMessage = ref<string | null>(null);
let disposed = false;
const formErrorMessage = ref<string | null>(null);
const displayedErrorMessage = computed(() => formErrorMessage.value ?? errorMessage.value);

onMounted(loadTimeZoneOptions);
onBeforeUnmount(() => { disposed = true; });

async function loadTimeZoneOptions() {
  loading.value = true;
  loadErrorMessage.value = null;
  try {
    const result = await authApi.getTimeZoneOptions();
    if (disposed) return;
    if (!result.ok) {
      loadErrorMessage.value = result.error.message;
      return;
    }
    timeZoneOptions.value = result.data.options.map(option => ({ value: option.id, label: option.displayName }));
    systemTimeZoneId.value = result.data.defaultId;
  } finally {
    if (!disposed) loading.value = false;
  }
}

watch([password, confirmPassword], () => {
  if (formErrorMessage.value === PASSWORD_CONFIRMATION_ERROR && validatePasswordConfirmation(password.value, confirmPassword.value) === null) {
    formErrorMessage.value = null;
  }
});

async function submit() {
  if (loading.value || loadErrorMessage.value || busy.value) return;

  formErrorMessage.value = validateEmail(email.value);
  if (formErrorMessage.value) {
    return;
  }

  formErrorMessage.value = validatePasswordConfirmation(password.value, confirmPassword.value);
  if (formErrorMessage.value) {
    return;
  }

  const success = await authStore.registerInitialAdmin(userName.value, email.value, password.value, systemTimeZoneId.value);
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

<style scoped>
.initial-admin-timezone {
  display: grid;
  gap: 0.3rem;
}
</style>
