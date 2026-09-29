<template>
  <div class="loading-indicator" role="status">
    <span class="loading-indicator-spinner" aria-hidden="true" />
    <span>{{ label }}</span>
  </div>
</template>

<script setup lang="ts">
defineProps<{ label: string }>();
</script>

<style scoped>
.loading-indicator {
  display: grid;
  justify-items: center;
  gap: 0.75rem;
  padding: 1.5rem;
  color: var(--bo-ink-muted);
}

.loading-indicator-spinner {
  width: 2rem;
  height: 2rem;
  border-radius: 999px;
  border: 3px solid var(--bo-border-soft);
  border-top-color: var(--bo-link);
  animation: loading-indicator-spin 0.9s linear infinite;
}

@keyframes loading-indicator-spin {
  to {
    transform: rotate(360deg);
  }
}
</style>
