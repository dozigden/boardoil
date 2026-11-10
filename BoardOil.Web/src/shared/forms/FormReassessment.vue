<template>
  <div v-if="error" class="form-reassessment-error" role="alert">
    <span>{{ error }}</span>
    <button type="button" class="btn btn--secondary" @click="emit('retry')">Retry</button>
  </div>
</template>

<script setup lang="ts">
defineProps<{ error: string }>();
const emit = defineEmits<{ retry: [] }>();
</script>

<style scoped>
.form-reassessment-error { display: flex; align-items: center; gap: 0.5rem; color: var(--bo-colour-danger-ink); }
</style>
