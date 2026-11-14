<template>
  <section class="account-profile">
    <template v-if="!form">
      <h2>Profile</h2>
      <LoadingIndicator v-if="loading" label="Loading profile..." />
      <template v-else-if="loadError">
        <p class="account-profile-error" role="alert">{{ loadError }}</p>
        <button type="button" class="btn" @click="loadProfile">Retry</button>
      </template>
    </template>
    <FormPage v-else :definition="form" class="account-profile-form"
      discard-message="You have unsaved profile changes. Discard them and leave?" @saved="profileSaved">
      <template #before-fields>
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
      </template>
      <template #fields="{ fields, values, busy, setValue }">
        <div class="account-profile-settings">
          <section class="panel panel-stack panel-stack--cozy">
            <FormFields
              :fields="fields.filter(field => field.name !== 'experimentalFeaturesEnabled')"
              :values="values"
              :busy="busy"
              @change="setValue"
            />
          </section>
          <section class="panel panel-stack panel-stack--cozy" aria-labelledby="experimental-features-heading">
            <h3 id="experimental-features-heading" class="account-profile-experiments-title">
              <ExperimentalFeatureIcon :size="20" aria-hidden="true" />
              Experimental features
            </h3>
            <FormFields
              :fields="fields.filter(field => field.name === 'experimentalFeaturesEnabled')"
              :values="values"
              :busy="busy"
              @change="setValue"
            />
            <section class="account-profile-experiments" aria-labelledby="current-experiments-heading">
              <h4 id="current-experiments-heading">Current experiments</h4>
              <p>New slick rendering engine.</p>
            </section>
          </section>
        </div>
      </template>
    </FormPage>

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
import { EllipsisVertical } from '@lucide/vue';
import { storeToRefs } from 'pinia';
import { computed, ref, shallowRef, watch } from 'vue';
import LoadingIndicator from '../../shared/components/LoadingIndicator.vue';
import ExperimentalFeatureIcon from '../../shared/components/ExperimentalFeatureIcon.vue';
import FormPage from '../../shared/forms/FormPage.vue';
import FormFields from '../../shared/forms/FormFields.vue';
import { createUserProfileForm } from '../forms/userProfileForm';
import ProfileImageCropDialog from '../components/ProfileImageCropDialog.vue';
import BoDropdown from '../../shared/components/BoDropdown.vue';
import UserAvatar from '../../shared/components/UserAvatar.vue';
import { useAuthStore } from '../../shared/stores/authStore';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import { useUserProfileImageStore } from '../../shared/stores/userProfileImageStore';
import { useUserProfileStore } from '../../shared/stores/userProfileStore';

const authStore = useAuthStore();
const feedback = useUiFeedbackStore();
const userProfileImageStore = useUserProfileImageStore();
const userProfileStore = useUserProfileStore();
const { user } = storeToRefs(authStore);
const { userProfileImageUrl, busy: imageBusy, errorMessage: imageErrorMessage } = storeToRefs(userProfileImageStore);
const form = shallowRef<ReturnType<typeof createUserProfileForm> | null>(null);
const loading = ref(false);
const loadError = ref<string | null>(null);
const userImageInput = ref<HTMLInputElement | null>(null);
const cropDialogOpen = ref(false);
const pendingProfileImageFile = ref<File | null>(null);

const userName = computed(() => user.value?.userName ?? 'Unknown user');
const displayName = computed(() => user.value?.displayName ?? userName.value);
const userRole = computed(() => user.value?.role ?? 'Unknown');

watch(() => user.value?.id, loadProfile, { immediate: true });

async function loadProfile() {
  const userId = user.value?.id;
  form.value = null;
  loadError.value = null;
  if (!userId) return;
  loading.value = true;
  const result = await userProfileStore.loadOwnProfile();
  if (user.value?.id !== userId) return;
  loading.value = false;
  if (!result.ok) {
    loadError.value = result.error.message;
    return;
  }
  form.value = createUserProfileForm(result.data, userProfileStore.saveOwnProfile);
}

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

function profileSaved() {
  feedback.showToast('Saved successfully.');
}

</script>

<style scoped>
.account-profile-settings { display: grid; gap: 0.9rem; }
.account-profile-experiments-title { display: flex; align-items: center; gap: 0.5rem; margin: 0; font-size: 1.1rem; }
.account-profile-experiments { margin-top: 0.5rem; }
.account-profile-experiments h4 { margin: 0 0 0.35rem; font-size: 0.9rem; font-weight: 500; color: var(--bo-ink-muted); }
.account-profile-experiments p { margin: 0; color: var(--bo-ink-muted); }
.account-profile {
  display: grid;
  gap: 1rem;
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

.account-profile-form :deep(.panel) { max-width: 32rem; }

.account-profile-error {
  margin: 0;
  color: var(--bo-colour-danger-ink);
}

</style>
