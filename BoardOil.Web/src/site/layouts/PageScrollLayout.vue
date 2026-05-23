<template>
  <section class="app-layout app-layout--page">
    <slot />
  </section>
</template>

<style scoped>
.app-layout--page {
  padding-inline: 1rem;
}

@media (min-width: 1024px) {
  .app-layout--page {
    padding-inline: 1.5rem;
  }
}
</style>
