<template>
  <FormPage :definition="form" discard-message="Discard your password changes and leave?" @saved="passwordChanged" />
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router';
import FormPage from '../../shared/forms/FormPage.vue';
import { createPasswordResetForm } from '../../shared/forms/passwordResetForm';
import { useAuthStore } from '../../shared/stores/authStore';

const router = useRouter();
const authStore = useAuthStore();
const form = createPasswordResetForm('self', values => authStore.changeOwnPassword(values.currentPassword!, values.newPassword));

async function passwordChanged() {
  await router.replace({ name: 'login', query: { passwordReset: '1' } });
}
</script>
