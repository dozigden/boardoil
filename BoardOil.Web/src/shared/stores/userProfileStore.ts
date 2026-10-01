import { defineStore } from 'pinia';
import { ref } from 'vue';
import { createUsersApi } from '../api/usersApi';
import { useAuthStore } from './authStore';
import type { OwnUserProfile, UserProfileEditModel } from '../types/authTypes';

export const useUserProfileStore = defineStore('userProfile', () => {
  const usersApi = createUsersApi();
  const ownProfile = ref<OwnUserProfile | null>(null);
  const busy = ref(false);
  const errorMessage = ref<string | null>(null);
  const authStore = useAuthStore();

  function SET_PROFILE(profile: OwnUserProfile) {
    ownProfile.value = profile;
  }

  function CLEAR_PROFILE() {
    ownProfile.value = null;
    busy.value = false;
    errorMessage.value = null;
  }

  async function loadOwnProfile() {
    const result = await usersApi.getMyProfile();
    if (!result.ok) {
      return null;
    }

    SET_PROFILE(result.data);
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

      SET_PROFILE(result.data);
      authStore.setOwnProfile(result.data.displayName, result.data.userName, result.data.role);
      return result.data;
    } finally {
      busy.value = false;
    }
  }

  function reset() {
    CLEAR_PROFILE();
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
