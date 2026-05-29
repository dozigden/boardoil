import { defineStore } from 'pinia';
import { ref } from 'vue';
import { createUsersApi } from '../api/usersApi';
import type { OwnUserProfile, UserProfileEditModel } from '../types/authTypes';

export const useUserProfileStore = defineStore('userProfile', () => {
  const usersApi = createUsersApi();
  const ownProfile = ref<OwnUserProfile | null>(null);
  const busy = ref(false);
  const errorMessage = ref<string | null>(null);

  async function loadOwnProfile() {
    const result = await usersApi.getMyProfile();
    if (!result.ok) {
      return null;
    }

    ownProfile.value = result.data;
    return result.data;
  }

  async function saveOwnProfile(model: UserProfileEditModel) {
    busy.value = true;
    errorMessage.value = null;
    try {
      const result = await usersApi.updateMyProfile(model);
      if (!result.ok) {
        errorMessage.value = result.error.message;
        return null;
      }

      ownProfile.value = result.data;
      return result.data;
    } finally {
      busy.value = false;
    }
  }

  function reset() {
    ownProfile.value = null;
    busy.value = false;
    errorMessage.value = null;
  }

  return {
    ownProfile,
    busy,
    errorMessage,
    loadOwnProfile,
    saveOwnProfile,
    reset
  };
});
