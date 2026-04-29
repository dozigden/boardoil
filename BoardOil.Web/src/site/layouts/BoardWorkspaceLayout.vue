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
  padding-inline: 1.5rem;
}

@media (max-width: 720px) {
  .app-layout--board {
    padding-inline: 0.75rem;
  }
}
</style>
