<template>
  <AdminSplitLayout
    :title="userName"
    :title-avatar-url="userProfileImageUrl"
    :items="navItems"
  >
    <RouterView />
  </AdminSplitLayout>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed } from 'vue';
import { RouterView } from 'vue-router';
import AdminSplitLayout from '../../system/components/AdminSplitLayout.vue';
import { useAuthStore } from '../../shared/stores/authStore';
import { useUserProfileImageStore } from '../../shared/stores/userProfileImageStore';

const authStore = useAuthStore();
const userProfileImageStore = useUserProfileImageStore();
const { user } = storeToRefs(authStore);
const { userProfileImageUrl } = storeToRefs(userProfileImageStore);
const userName = computed(() => user.value?.userName ?? 'Profile');

const navItems = [
  {
    label: 'Profile',
    to: { name: 'user-admin-profile' }
  },
  {
    label: 'Access Tokens',
    to: { name: 'user-admin-access-tokens' }
  }
];
</script>
