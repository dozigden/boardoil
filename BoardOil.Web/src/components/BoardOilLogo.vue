<template>
  <span class="boardoil-logo boardoil-defaults" v-html="boardOilLogoMarkup" />
</template>

<script setup lang="ts">
import boardOilLogoSvg from '../assets/svg/boardoil-logo.svg?raw';

const boardOilLogoMarkup = boardOilLogoSvg.replace('<svg', '<svg aria-hidden="true" focusable="false"');
</script>

<style scoped>
.boardoil-logo {
  display: block;
  width: var(--boardoil-logo-size, 1em);
  height: var(--boardoil-logo-size, 1em);
}

.boardoil-logo :deep(svg) {
  display: block;
  width: 100%;
  height: 100%;
}

.boardoil-logo :deep(.card),
.boardoil-logo :deep(.drop) {
  stroke: var(--bo-drop);
  stroke-width: 5px;
}

.boardoil-defaults {
  --bo-drop: var(--bo-colour-brand, #35165a);
  --bo-c2r1: var(--bo-colour-secondary, #69c1ce);
  --bo-c3r1: var(--bo-colour-warning, #e8c07d);
  --bo-c1r2: var(--bo-colour-warning, #e8c07d);
  --bo-c2r2: var(--bo-colour-danger, #cd474e);
  --bo-c3r2: var(--bo-colour-info, #9bbef8);
  --bo-c1r3: var(--bo-colour-energy, #f17437);
  --bo-c2r3: var(--bo-colour-success, #32cda0);
  --bo-c3r3: var(--bo-colour-success, #32cda0);
}

.boardoil-logo :deep(.drop) {
  fill: var(--bo-colour-brand-mid, #7d63ab);
}

.boardoil-logo :deep(.c2r1) {
  fill: var(--bo-c2r1);
}

.boardoil-logo :deep(.c3r1) {
  fill: var(--bo-c3r1);
}

.boardoil-logo :deep(.c1r2) {
  fill: var(--bo-c1r2);
}

.boardoil-logo :deep(.c2r2) {
  fill: var(--bo-c2r2);
}

.boardoil-logo :deep(.c3r2) {
  fill: var(--bo-c3r2);
}

.boardoil-logo :deep(.c1r3) {
  fill: var(--bo-c1r3);
}

.boardoil-logo :deep(.c2r3) {
  display: none;
}

.boardoil-logo :deep(.c3r3) {
  fill: var(--bo-c3r3);
}
</style>
