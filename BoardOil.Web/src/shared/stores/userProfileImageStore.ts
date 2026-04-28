import { computed, ref } from 'vue';
import { defineStore } from 'pinia';
import { buildApiUrl } from '../api/config';
import { createUsersApi } from '../api/usersApi';
import type { UserProfileImage } from '../types/authTypes';

export const useUserProfileImageStore = defineStore('userProfileImage', () => {
  const usersApi = createUsersApi();
  const userProfileImage = ref<UserProfileImage | null>(null);
  const busy = ref(false);
  const errorMessage = ref<string | null>(null);
  const loaded = ref(false);

  const userProfileImageUrl = computed(() =>
    userProfileImage.value ? buildApiUrl(`/images/${userProfileImage.value.relativePath}`) : null
  );

  async function loadOwnProfileImage(force = false) {
    if (loaded.value && !force) {
      return true;
    }

    const result = await usersApi.getMyProfileImage();
    if (!result.ok) {
      return false;
    }

    userProfileImage.value = result.data;
    loaded.value = true;
    return true;
  }

  async function uploadOwnProfileImage(file: File) {
    busy.value = true;
    errorMessage.value = null;
    try {
      const result = await usersApi.uploadMyProfileImage(file);
      if (!result.ok) {
        errorMessage.value = result.error.message;
        return false;
      }

      userProfileImage.value = result.data;
      loaded.value = true;
      return true;
    } finally {
      busy.value = false;
    }
  }

  async function deleteOwnProfileImage() {
    busy.value = true;
    errorMessage.value = null;
    try {
      const result = await usersApi.deleteMyProfileImage();
      if (!result.ok) {
        errorMessage.value = result.error.message;
        return false;
      }

      userProfileImage.value = null;
      loaded.value = true;
      return true;
    } finally {
      busy.value = false;
    }
  }

  function reset() {
    userProfileImage.value = null;
    loaded.value = false;
    errorMessage.value = null;
  }

  return {
    userProfileImage,
    userProfileImageUrl,
    busy,
    errorMessage,
    loaded,
    loadOwnProfileImage,
    uploadOwnProfileImage,
    deleteOwnProfileImage,
    reset
  };
});
