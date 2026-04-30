<template>
  <section class="unauthorized-view">
    <h2>Session Expired or Unauthorized</h2>
    <p>Your session is no longer valid for this action. Please sign in again.</p>
    <RouterLink class="auth-link" :to="loginTarget">Go to Login</RouterLink>
  </section>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { useRoute } from 'vue-router';
import { getSafeRedirectTarget } from '../auth/redirectTarget';

const route = useRoute();

const loginTarget = computed(() => {
  const redirect = getSafeRedirectTarget(route.query.redirect);
  if (!redirect) {
    return { name: 'login' };
  }

  return { name: 'login', query: { redirect } };
});
</script>
