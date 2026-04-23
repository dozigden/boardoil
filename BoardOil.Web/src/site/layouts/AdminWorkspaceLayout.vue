<template>
  <section class="app-layout app-layout--admin">
    <slot />
  </section>
</template>

<style scoped>
.app-layout--admin {
  flex: 1;
  min-height: 0;
  padding: 0 1rem 1rem 1rem;
}

.app-layout--admin :deep(.admin-content > *) {
  margin-top: 0;
  max-width: none;
}

@media (max-width: 720px) {
  .app-layout--admin {
    padding: 0 0 0.75rem 0;
  }
}
</style>
