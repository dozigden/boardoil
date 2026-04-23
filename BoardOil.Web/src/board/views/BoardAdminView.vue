<template>
  <AdminSplitLayout title="Board Admin" :items="navItems">
    <RouterView />
  </AdminSplitLayout>
  <RouterView name="dialog" />
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed } from 'vue';
import { RouterView, useRoute } from 'vue-router';
import AdminSplitLayout from '../../system/components/AdminSplitLayout.vue';
import { useBoardStore } from '../stores/boardStore';
import { buildBoardAdminNavItems } from './boardAdminNav';

const route = useRoute();
const boardStore = useBoardStore();
const { board } = storeToRefs(boardStore);

const boardId = computed(() => {
  const parsed = Number.parseInt(String(route.params.boardId ?? ''), 10);
  return Number.isFinite(parsed) ? parsed : null;
});

const navItems = computed(() => {
  if (boardId.value === null) {
    return [];
  }

  return buildBoardAdminNavItems(boardId.value, board.value?.currentUserRole);
});

</script>
