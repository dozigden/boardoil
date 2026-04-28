<template>
  <section class="account-profile">
    <header class="account-profile-header">
      <h2>Profile</h2>
      <p>Manage your account details and profile image.</p>
    </header>

    <section class="account-profile-card">
      <div class="account-profile-avatar-wrap">
        <div class="account-profile-avatar-shell">
          <img
            v-if="userProfileImageUrl"
            :src="userProfileImageUrl"
            alt="User profile image"
            class="account-profile-avatar"
          />
          <div v-else class="account-profile-avatar account-profile-avatar--empty" aria-hidden="true">
            {{ userInitials }}
          </div>
          <BoDropdown
            class="account-profile-avatar-menu"
            align="left"
            icon-only
            label="Profile image options"
            :icon="EllipsisVertical"
            :icon-size="14"
          >
            <template #default="{ close }">
              <button type="button" class="bo-dropdown-item" @click="openImagePicker(close)">Upload image</button>
              <button
                type="button"
                class="bo-dropdown-item"
                :disabled="!userProfileImageUrl"
                @click="removeImage(close)"
              >
                Remove image
              </button>
            </template>
          </BoDropdown>
        </div>
      </div>

      <div class="account-profile-details">
        <p><strong>User:</strong> {{ userName }}</p>
        <p><strong>Role:</strong> {{ userRole }}</p>
      </div>

    </section>

    <input
      ref="userImageInput"
      type="file"
      accept="image/png,image/jpeg,image/webp"
      class="account-profile-file-input"
      @change="onUserImageSelected"
    />
  </section>
</template>

<script setup lang="ts">
import { EllipsisVertical } from 'lucide-vue-next';
import { storeToRefs } from 'pinia';
import { computed, ref } from 'vue';
import BoDropdown from '../../shared/components/BoDropdown.vue';
import { useAuthStore } from '../../shared/stores/authStore';

const authStore = useAuthStore();
const { user, userProfileImageUrl } = storeToRefs(authStore);
const userImageInput = ref<HTMLInputElement | null>(null);

const userName = computed(() => user.value?.userName ?? 'Unknown user');
const userRole = computed(() => user.value?.role ?? 'Unknown');
const userInitials = computed(() => userName.value.slice(0, 2).toUpperCase());

function openImagePicker(close?: () => void) {
  close?.();
  userImageInput.value?.click();
}

async function onUserImageSelected(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  if (!file) {
    return;
  }

  await authStore.uploadOwnProfileImage(file);
  input.value = '';
}

async function removeImage(close?: () => void) {
  close?.();
  if (!userProfileImageUrl.value) {
    return;
  }

  await authStore.deleteOwnProfileImage();
}
</script>

<style scoped>
.account-profile {
  display: grid;
  gap: 1rem;
}

.account-profile-header h2 {
  margin: 0;
}

.account-profile-header p {
  margin: 0.4rem 0 0;
  color: var(--bo-ink-muted);
}

.account-profile-card {
  display: grid;
  gap: 1rem;
  padding: 1rem;
  border: 1px solid var(--bo-border-soft);
  border-radius: 12px;
  background: var(--bo-surface-panel);
  overflow: visible;
}

.account-profile-avatar-wrap {
  display: flex;
  align-items: center;
}

.account-profile-avatar-shell {
  position: relative;
  display: inline-flex;
  width: 6.75rem;
  height: 6.75rem;
  overflow: visible;
}

.account-profile-avatar {
  width: 100%;
  height: 100%;
  aspect-ratio: 1 / 1;
  flex: 0 0 auto;
  border-radius: 999px;
  object-fit: cover;
  border: 1px solid var(--bo-border-default);
}

.account-profile-avatar--empty {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  background: var(--bo-surface-brand);
  color: var(--bo-link);
  font-weight: 700;
}

.account-profile-details p {
  margin: 0;
}

.account-profile-avatar-menu {
  position: absolute;
  right: 0.1rem;
  bottom: 0.1rem;
}

.account-profile-avatar-menu :deep(.bo-dropdown-trigger) {
  width: 1.75rem;
  height: 1.75rem;
  aspect-ratio: 1 / 1;
  min-width: 1.75rem;
  min-height: 1.75rem;
  max-width: 1.75rem;
  max-height: 1.75rem;
  padding: 0;
  border-radius: 50%;
  box-sizing: border-box;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  line-height: 1;
}

.account-profile-avatar-menu :deep(.bo-dropdown-panel) {
  top: calc(100% + 0.35rem);
  bottom: auto;
  left: 0;
  right: auto;
  z-index: 50;
}

.account-profile-file-input {
  display: none;
}
</style>
