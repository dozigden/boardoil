<template>
  <section class="unauthorized-view">
    <h2>Session Expired or Unauthorized</h2>
    <p>Your session is no longer valid for this action. Please sign in again.</p>
    <RouterLink class="auth-link" :to="{ name: 'login' }">Go to Login</RouterLink>
  </section>
</template>
