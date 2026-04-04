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
      label: 'Details',
      to: { name: 'board-details', params: { boardId: boardId.value } }
    },
    {
      label: 'Tags',
      to: { name: 'tags', params: { boardId: boardId.value } },
      activeRouteNames: ['tags', 'tags-new', 'tags-tag']
    }
  ];

  if (board.value?.currentUserRole === 'Owner') {
    items.splice(1, 0, {
      label: 'Columns',
      to: { name: 'columns', params: { boardId: boardId.value } },
      activeRouteNames: ['columns', 'columns-column']
    });
    items.push({
      label: 'Members',
      to: { name: 'board-members', params: { boardId: boardId.value } }
    });
    items.push({
      label: 'Delete board',
      to: { name: 'board-delete', params: { boardId: boardId.value } }
    });
  }

  return items;
});

</script>
