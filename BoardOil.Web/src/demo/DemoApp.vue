<template>
  <DemoBoardLayout>
    <RouterView v-slot="{ Component, route }">
      <component :is="Component" :key="`${String(route.name)}:${JSON.stringify(route.params)}`" />
    </RouterView>
  </DemoBoardLayout>
  <RouterView name="dialog" />
  <UiFeedbackToast />
  <ConfirmDialogHost />
</template>

<script setup lang="ts">
import { onUnmounted } from 'vue';
import { RouterView } from 'vue-router';
import ConfirmDialogHost from '../shared/components/ConfirmDialogHost.vue';
import UiFeedbackToast from '../shared/components/UiFeedbackToast.vue';
import { useBoardStore } from '../board/stores/boardStore';
import DemoBoardLayout from './DemoBoardLayout.vue';

const boardStore = useBoardStore();

onUnmounted(async () => {
  await boardStore.dispose();
});
</script>
