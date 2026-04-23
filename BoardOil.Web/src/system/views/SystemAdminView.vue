<template>
  <AdminSplitLayout title="System" :items="navItems">
    <RouterView />
  </AdminSplitLayout>
</template>

<script setup lang="ts">
import { RouterView } from 'vue-router';
import AdminSplitLayout from '../components/AdminSplitLayout.vue';

const navItems = [
  {
    label: 'Boards',
    to: { name: 'system-admin-boards' },
    activeRouteNames: ['system-admin-boards', 'system-admin-board-members']
  },
  {
    label: 'Users',
    to: { name: 'users' }
  },
  {
    label: 'Client Accounts',
    to: { name: 'client-accounts' }
  },
  {
    label: 'Configuration',
    to: { name: 'configuration' }
  }
];
</script>
