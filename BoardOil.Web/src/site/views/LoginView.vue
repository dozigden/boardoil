<template>
  <section class="auth-view">
    <AuthForm :definition="form" busy-label="Signing in..." @saved="signedIn">
      <template #intro>
        <p v-if="requiresInitialAdminSetup" class="auth-help">
          First time setup? <RouterLink :to="{ name: 'setup-initial-admin' }">Create the initial admin</RouterLink>.
        </p>
      </template>
    </AuthForm>
  </section>
</template>

<script setup lang="ts">
import { onMounted } from 'vue';
import AuthForm from '../components/AuthForm.vue';
import { createLoginForm } from '../forms/loginForm';
import { storeToRefs } from 'pinia';
import { RouterLink, useRoute, useRouter } from 'vue-router';
import { useAuthStore } from '../../shared/stores/authStore';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import { getSafeRedirectTarget } from '../auth/redirectTarget';

const router = useRouter();
const route = useRoute();
const authStore = useAuthStore();
const feedback = useUiFeedbackStore();
const { requiresInitialAdminSetup } = storeToRefs(authStore);
const form = createLoginForm(({ userName, password }) => authStore.login(userName, password));

onMounted(() => {
  if (!authStore.errorMessage) return;
  feedback.showToast(authStore.errorMessage, 'error');
  authStore.errorMessage = null;
});

if (route.query.passwordReset === '1') {
  feedback.showToast('Password reset successfully.');
  const nextQuery = { ...route.query };
  delete nextQuery.passwordReset;
  void router.replace({ query: nextQuery });
}

async function signedIn() {
  const target = getSafeRedirectTarget(route.query.redirect);
  if (target) {
    await router.replace(target);
    return;
  }

  await router.replace({ name: 'boards' });
}
</script>
