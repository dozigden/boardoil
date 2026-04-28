<template>
  <section class="password-reset-page">
    <header class="password-reset-header">
      <h2>Reset Password</h2>
      <p>Update your account password.</p>
    </header>

    <form class="password-reset-card" @submit.prevent="submit">
      <label>
        Current password
        <input
          v-model="currentPassword"
          :disabled="busy"
          type="password"
          autocomplete="current-password"
          required
        />
      </label>

      <label>
        New password
        <input
          v-model="newPassword"
          :disabled="busy"
          type="password"
          autocomplete="new-password"
          required
        />
      </label>

      <label>
        Confirm new password
        <input
          v-model="confirmPassword"
          :disabled="busy"
          type="password"
          autocomplete="new-password"
          required
        />
      </label>

      <p v-if="draftError" class="error">{{ draftError }}</p>

      <div class="password-reset-actions">
        <button type="submit" class="btn" :disabled="busy">Reset password</button>
      </div>
    </form>
  </section>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import { useAuthStore } from '../../shared/stores/authStore';
import { PASSWORD_CONFIRMATION_ERROR, validatePasswordConfirmation } from '../../shared/utils/passwordConfirmation';
import { validatePasswordResetDraft } from '../../shared/components/passwordResetDialogModel';

const router = useRouter();
const authStore = useAuthStore();

const currentPassword = ref('');
const newPassword = ref('');
const confirmPassword = ref('');
const draftError = ref<string | null>(null);
const busy = computed(() => authStore.busy);

async function submit() {
  draftError.value = validatePasswordResetDraft('self', currentPassword.value, newPassword.value, confirmPassword.value);
  if (draftError.value) {
    return;
  }

  const success = await authStore.changeOwnPassword(currentPassword.value, newPassword.value);
  if (!success) {
    return;
  }

  await router.replace({ name: 'login', query: { passwordReset: '1' } });
}

watch([newPassword, confirmPassword], () => {
  if (draftError.value === PASSWORD_CONFIRMATION_ERROR && validatePasswordConfirmation(newPassword.value, confirmPassword.value) === null) {
    draftError.value = null;
  }
});

watch(currentPassword, () => {
  if (draftError.value === 'Current password is required.' && currentPassword.value.trim()) {
    draftError.value = null;
  }
});
</script>

<style scoped>
.password-reset-page {
  display: grid;
  gap: 1rem;
}

.password-reset-header h2 {
  margin: 0;
}

.password-reset-header p {
  margin: 0.4rem 0 0;
  color: var(--bo-ink-muted);
}

.password-reset-card {
  display: grid;
  gap: 0.75rem;
  max-width: 26rem;
  padding: 1rem;
  border: 1px solid var(--bo-border-soft);
  border-radius: 12px;
  background: var(--bo-surface-panel);
}

.password-reset-actions {
  display: flex;
  justify-content: flex-start;
}
</style>
