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

const authStore = useAuthStore();
const { user, userProfileImageUrl } = storeToRefs(authStore);
const userName = computed(() => user.value?.userName ?? 'Profile');

const navItems = [
  {
    label: 'Profile',
    to: { name: 'user-admin-profile' }
  }
];
</script>
