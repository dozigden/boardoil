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
  padding-inline-start: 2rem;
  padding-inline-end: 0;
}

@media (max-width: 720px) {
  .app-layout--full-height {
    padding-inline-start: 0.75rem;
    padding-inline-end: 0;
  }
}
</style>
