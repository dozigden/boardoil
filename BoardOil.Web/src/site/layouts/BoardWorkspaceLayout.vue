<template>
  <section class="app-layout app-layout--board">
    <slot />
  </section>
</template>

<style scoped>
.app-layout--board {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
}
</style>
