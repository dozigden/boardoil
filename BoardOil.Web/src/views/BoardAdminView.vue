<template>
  <AdminSplitLayout
    title="Board Admin"
    :items="navItems"
    :back-to="backToBoard"
    back-label="Back to board"
  >
    <RouterView />
  </AdminSplitLayout>
  <RouterView name="dialog" />
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { RouterView, useRoute } from 'vue-router';
import AdminSplitLayout from '../components/AdminSplitLayout.vue';

const route = useRoute();

const boardId = computed(() => {
  const parsed = Number.parseInt(String(route.params.boardId ?? ''), 10);
  return Number.isFinite(parsed) ? parsed : null;
});

const navItems = computed(() => {
  if (boardId.value === null) {
    return [];
  }

  return [
    {
      label: 'Columns',
      to: { name: 'columns', params: { boardId: boardId.value } }
    },
    {
      label: 'Tags',
      to: { name: 'tags', params: { boardId: boardId.value } }
    }
  ];
});

const backToBoard = computed(() => {
  if (boardId.value === null) {
    return null;
  }

  return { name: 'board', params: { boardId: boardId.value } };
});
</script>
