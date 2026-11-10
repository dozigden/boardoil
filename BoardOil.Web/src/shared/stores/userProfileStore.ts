import { defineStore } from 'pinia';
import { ref } from 'vue';
import { createUsersApi } from '../api/usersApi';
import { useAuthStore } from './authStore';
import type { OwnUserProfile, UserProfileEditModel } from '../types/authTypes';

export const useUserProfileStore = defineStore('userProfile', () => {
  const usersApi = createUsersApi();
  const ownProfile = ref<OwnUserProfile | null>(null);
  const busy = ref(false);
  const authStore = useAuthStore();

  function SET_PROFILE(profile: OwnUserProfile) {
    ownProfile.value = profile;
  }

  function CLEAR_PROFILE() {
    ownProfile.value = null;
    busy.value = false;
  }

  async function loadOwnProfile() {
    const result = await usersApi.getMyProfile();
    if (result.ok) SET_PROFILE(result.data);
    return result;
  }

  async function saveOwnProfile(model: UserProfileEditModel) {
    busy.value = true;
    try {
      const result = await usersApi.updateMyProfile(model);
      if (result.ok) {
        SET_PROFILE(result.data);
        authStore.setOwnProfile(result.data.displayName, result.data.userName, result.data.role);
      }
      return result;
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
    loadOwnProfile,
    saveOwnProfile,
    reset
  };
});
