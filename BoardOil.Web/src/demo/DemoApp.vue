<template>
  <DemoBoardLayout>
    <RouterView v-slot="{ Component, route }">
      <component :is="Component" :key="`${String(route.name)}:${JSON.stringify(route.params)}:${demoResetVersion}`" />
    </RouterView>
  </DemoBoardLayout>
  <RouterView name="dialog" />
  <DemoWelcomeDialog :open="welcomeOpen" @close="welcomeOpen = false" />
  <UiFeedbackToast />
  <ConfirmDialogHost />
</template>

<script setup lang="ts">
import { onUnmounted, ref } from 'vue';
import { RouterView } from 'vue-router';
import ConfirmDialogHost from '../shared/components/ConfirmDialogHost.vue';
import UiFeedbackToast from '../shared/components/UiFeedbackToast.vue';
import { useBoardStore } from '../board/stores/boardStore';
import DemoBoardLayout from './DemoBoardLayout.vue';
import { demoResetVersion } from './demoReset';
import DemoWelcomeDialog from './DemoWelcomeDialog.vue';

const boardStore = useBoardStore();
const welcomeOpen = ref(true);

onUnmounted(async () => {
  await boardStore.dispose();
});
</script>
