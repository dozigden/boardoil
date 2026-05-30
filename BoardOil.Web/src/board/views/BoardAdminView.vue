<template>
  <AdminSplitLayout title="Board Admin" :items="navItems">
    <RouterView />
  </AdminSplitLayout>
  <RouterView v-slot="{ Component, route: dialogRoute }" name="dialog">
    <component :is="Component" :key="getDialogViewKey(dialogRoute)" />
  </RouterView>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed } from 'vue';
import { RouterView, type RouteLocationNormalizedLoaded } from 'vue-router';
import AdminSplitLayout from '../../system/components/AdminSplitLayout.vue';
import { useBoardStore } from '../stores/boardStore';
import { buildBoardAdminNavItems } from './boardAdminNav';

const boardStore = useBoardStore();
const { board, currentBoardId } = storeToRefs(boardStore);
const boardId = computed(() => currentBoardId.value!);

const navItems = computed(() => {
  return buildBoardAdminNavItems(boardId.value, board.value?.currentUserRole);
});

function getDialogViewKey(dialogRoute: RouteLocationNormalizedLoaded) {
  const routeName = typeof dialogRoute.name === 'string' ? dialogRoute.name : 'dialog';
  return `${routeName}:${JSON.stringify(dialogRoute.params ?? {})}`;
}
</script>
