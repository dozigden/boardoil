<template>
  <section class="auth-view">
    <AuthForm v-if="form" :definition="form" busy-label="Creating admin..." @saved="adminCreated">
      <template #intro>
        <p class="auth-help">This works only when there are no users yet. After setup, this account is signed in immediately.</p>
      </template>
      <template #footer>
        <RouterLink class="auth-link" :to="{ name: 'login' }">Back to sign in</RouterLink>
      </template>
    </AuthForm>
    <section v-else class="auth-card panel panel--strong">
      <h2>Create Initial Admin</h2>
      <LoadingIndicator v-if="loading" label="Loading setup..." />
      <template v-else-if="loadErrorMessage">
        <p class="error" role="alert">{{ loadErrorMessage }}</p>
        <button type="button" class="btn btn--secondary" @click="loadTimeZoneOptions">Retry</button>
      </template>
      <RouterLink class="auth-link" :to="{ name: 'login' }">Back to sign in</RouterLink>
    </section>
  </section>
</template>

<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, shallowRef } from 'vue';
import { RouterLink, useRouter } from 'vue-router';
import { useAuthStore } from '../../shared/stores/authStore';
import { createAuthApi } from '../../shared/api/authApi';
import LoadingIndicator from '../../shared/components/LoadingIndicator.vue';
import AuthForm from '../components/AuthForm.vue';
import { createInitialAdminForm } from '../forms/initialAdminForm';

const router = useRouter();
const authStore = useAuthStore();
const authApi = createAuthApi();
const form = shallowRef<ReturnType<typeof createInitialAdminForm> | null>(null);
const loading = ref(true);
const loadErrorMessage = ref<string | null>(null);
let disposed = false;

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
    form.value = createInitialAdminForm(result.data, ({ userName, email, password, systemTimeZoneId }) =>
      authStore.registerInitialAdmin(userName, email, password, systemTimeZoneId));
  } finally {
    if (!disposed) loading.value = false;
  }
}

async function adminCreated() {
  await router.replace({ name: 'boards' });
}
</script>
