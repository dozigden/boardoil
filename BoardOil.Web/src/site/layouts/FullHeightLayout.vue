<template>
  <section class="app-layout app-layout--full-height">
    <slot />
  </section>
</template>

<style scoped>
.app-layout--full-height {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  padding-inline: 1.5rem;
}

@media (max-width: 720px) {
  .app-layout--full-height {
    padding-inline: 0.75rem;
  }
}
</style>
