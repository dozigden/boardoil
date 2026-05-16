<template>
  <section class="account-profile">
    <header class="account-profile-header">
      <h2>Profile</h2>
    </header>

    <section class="account-profile-card">
      <div class="account-profile-avatar-wrap">
        <div class="account-profile-avatar-shell">
          <UserAvatar
            :image-url="userProfileImageUrl"
            :display-name="displayName"
            size="xl"
            class="account-profile-avatar"
          />
          <BoDropdown
            class="account-profile-avatar-menu"
            align="left"
            icon-only
            label="Profile image options"
            :icon="EllipsisVertical"
            :icon-size="14"
          >
            <template #default="{ close }">
              <button type="button" class="bo-dropdown-item" :disabled="imageBusy" @click="openImagePicker(close)">
                Upload image
              </button>
              <button
                type="button"
                class="bo-dropdown-item"
                :disabled="!userProfileImageUrl || imageBusy"
                @click="removeImage(close)"
              >
                Remove image
              </button>
            </template>
          </BoDropdown>
        </div>
      </div>

      <div class="account-profile-details">
        <p><strong>Name:</strong> {{ displayName }}</p>
        <p><strong>User:</strong> @{{ userName }}</p>
        <p><strong>Role:</strong> {{ userRole }}</p>
        <p v-if="imageErrorMessage" class="account-profile-error" role="alert">{{ imageErrorMessage }}</p>
      </div>
    </section>

    <form class="account-profile-form" @submit.prevent="saveProfile">
      <label class="account-profile-field">
        <span>Display name</span>
        <input v-model="editDisplayName" type="text" maxlength="64" autocomplete="name" />
      </label>
      <label class="account-profile-field">
        <span>Email</span>
        <input v-model="editEmail" type="email" maxlength="320" autocomplete="email" />
      </label>
      <div class="account-profile-actions">
        <button type="submit" class="btn" :disabled="saveBusy">Save profile</button>
      </div>
      <p v-if="saveErrorMessage" class="account-profile-error" role="alert">{{ saveErrorMessage }}</p>
      <p v-else-if="saveSuccessMessage" class="account-profile-success">{{ saveSuccessMessage }}</p>
    </form>

    <input
      ref="userImageInput"
      type="file"
      accept="image/png,image/jpeg,image/webp"
      class="account-profile-file-input"
      @change="onUserImageSelected"
    />

    <ProfileImageCropDialog
      :open="cropDialogOpen"
      :source-file="pendingProfileImageFile"
      :busy="imageBusy"
      :error-message="imageErrorMessage"
      @close="closeCropDialog"
      @confirm="uploadCroppedImage"
    />
  </section>
</template>

<script setup lang="ts">
import { EllipsisVertical } from 'lucide-vue-next';
import { storeToRefs } from 'pinia';
import { computed, ref, watch } from 'vue';
import ProfileImageCropDialog from '../components/ProfileImageCropDialog.vue';
import BoDropdown from '../../shared/components/BoDropdown.vue';
import UserAvatar from '../../shared/components/UserAvatar.vue';
import { createUsersApi } from '../../shared/api/usersApi';
import { useAuthStore } from '../../shared/stores/authStore';
import { useUserProfileImageStore } from '../../shared/stores/userProfileImageStore';

const authStore = useAuthStore();
const userProfileImageStore = useUserProfileImageStore();
const usersApi = createUsersApi();
const { user } = storeToRefs(authStore);
const { userProfileImageUrl, busy: imageBusy, errorMessage: imageErrorMessage } = storeToRefs(userProfileImageStore);
const userImageInput = ref<HTMLInputElement | null>(null);
const cropDialogOpen = ref(false);
const pendingProfileImageFile = ref<File | null>(null);
const editDisplayName = ref('');
const editEmail = ref('');
const saveBusy = ref(false);
const saveErrorMessage = ref<string | null>(null);
const saveSuccessMessage = ref<string | null>(null);

const userName = computed(() => user.value?.userName ?? 'Unknown user');
const displayName = computed(() => user.value?.displayName ?? userName.value);
const userRole = computed(() => user.value?.role ?? 'Unknown');

watch(
  () => user.value,
  async (nextUser) => {
    if (!nextUser) {
      editDisplayName.value = '';
      editEmail.value = '';
      return;
    }

    editDisplayName.value = nextUser.displayName;
    const profileResult = await usersApi.getMyProfile();
    if (profileResult.ok) {
      editEmail.value = profileResult.data.email;
    }
  },
  { immediate: true }
);

function openImagePicker(close?: () => void) {
  if (imageBusy.value) {
    return;
  }

  close?.();
  userImageInput.value?.click();
}

async function onUserImageSelected(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  if (!file) {
    return;
  }

  pendingProfileImageFile.value = file;
  cropDialogOpen.value = true;
  input.value = '';
}

async function removeImage(close?: () => void) {
  if (imageBusy.value) {
    return;
  }

  close?.();
  if (!userProfileImageUrl.value) {
    return;
  }

  await userProfileImageStore.deleteOwnProfileImage();
}

function closeCropDialog() {
  if (imageBusy.value) {
    return;
  }

  cropDialogOpen.value = false;
  pendingProfileImageFile.value = null;
}

async function uploadCroppedImage(file: File) {
  const success = await userProfileImageStore.uploadOwnProfileImage(file);
  if (!success) {
    return;
  }

  closeCropDialog();
}

async function saveProfile() {
  saveBusy.value = true;
  saveErrorMessage.value = null;
  saveSuccessMessage.value = null;
  try {
    const result = await usersApi.updateMyProfile(editDisplayName.value, editEmail.value);
    if (!result.ok) {
      saveErrorMessage.value = result.error.message;
      return;
    }

    authStore.setOwnProfile(result.data.displayName, result.data.userName, result.data.role);
    editDisplayName.value = result.data.displayName;
    editEmail.value = result.data.email;
    saveSuccessMessage.value = 'Profile updated.';
  } finally {
    saveBusy.value = false;
  }
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
  aspect-ratio: 1 / 1;
  border: 1px solid var(--bo-border-default);
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

.account-profile-form {
  display: grid;
  gap: 0.75rem;
  max-width: 32rem;
}

.account-profile-field {
  display: grid;
  gap: 0.35rem;
}

.account-profile-field span {
  font-size: 0.9rem;
  color: var(--bo-ink-muted);
}

.account-profile-field input {
  border: 1px solid var(--bo-border-default);
  border-radius: 8px;
  padding: 0.5rem 0.65rem;
  font: inherit;
}

.account-profile-actions {
  display: flex;
  justify-content: flex-start;
}

.account-profile-error {
  margin: 0;
  color: var(--bo-colour-danger-ink);
}

.account-profile-success {
  margin: 0;
  color: var(--bo-colour-success-ink);
}
</style>
