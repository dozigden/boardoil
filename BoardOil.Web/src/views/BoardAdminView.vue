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
import { storeToRefs } from 'pinia';
import { computed } from 'vue';
import { RouterView, useRoute } from 'vue-router';
import AdminSplitLayout from '../components/AdminSplitLayout.vue';
import { useBoardStore } from '../stores/boardStore';

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

  const items = [
    {
      label: 'Tags',
      to: { name: 'tags', params: { boardId: boardId.value } }
    }
  ];

  if (board.value?.currentUserRole === 'Owner') {
    items.unshift({
      label: 'Columns',
      to: { name: 'columns', params: { boardId: boardId.value } }
    });
    items.push({
      label: 'Members',
      to: { name: 'board-members', params: { boardId: boardId.value } }
    });
  }

  return items;
});

const backToBoard = computed(() => {
  if (boardId.value === null) {
    return null;
  }

  return { name: 'board', params: { boardId: boardId.value } };
});
</script>
