<template>
  <section class="board-workspace-view">
    <BoardView />
    <RouterView name="dialog" />
  </section>
</template>

<script setup lang="ts">
import { RouterView } from 'vue-router';
import BoardView from './BoardView.vue';
</script>

<style scoped>
.board-workspace-view {
  flex: 1;
  min-height: 0;
  min-width: 0;
  display: flex;
  flex-direction: column;
}
</style>
