<template>
  <span class="boardoil-drop" v-html="boardOilDropMarkup" />
</template>

<script setup lang="ts">
import boardOilDropSvg from '../../assets/svg/boardoil-drop.svg?raw';

const boardOilDropMarkup = boardOilDropSvg.replace('<svg', '<svg aria-hidden="true" focusable="false"');
</script>

<style scoped>
.boardoil-drop {
  display: block;
  width: 1em;
  height: 1em;
}

.boardoil-drop :deep(svg) {
  display: block;
  width: 100%;
  height: 100%;
}

.boardoil-drop :deep(path) {
  fill: var(--bo-colour-brand-mid, #7d63ab);
  stroke: currentColor;
  stroke-width: 8;
  stroke-linecap: round;
  stroke-linejoin: round;
}
</style>
