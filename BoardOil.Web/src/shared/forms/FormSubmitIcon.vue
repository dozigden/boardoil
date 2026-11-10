<template>
  <span class="form-submit-icon">
    <LoaderCircle v-if="showSpinner" class="form-submit-spinner" :size="16" aria-hidden="true" />
    <Check v-else :size="16" aria-hidden="true" />
    <span class="form-submit-status" role="status">{{ showSpinner ? 'Loading choices...' : '' }}</span>
  </span>
</template>

<script setup lang="ts">
import { Check, LoaderCircle } from '@lucide/vue';
import { ref, watch } from 'vue';

const props = defineProps<{ loading: boolean }>();
const showSpinner = ref(false);
watch(() => props.loading, (loading, _, onCleanup) => {
  showSpinner.value = false;
  if (!loading) return;
  const timer = setTimeout(() => { showSpinner.value = true; }, 300);
  onCleanup(() => clearTimeout(timer));
}, { immediate: true });
</script>

<style scoped>
.form-submit-icon { display: inline-flex; flex: 0 0 16px; width: 16px; height: 16px; }
.form-submit-spinner { animation: form-submit-spin 0.9s linear infinite; }
.form-submit-status { position: absolute; width: 1px; height: 1px; padding: 0; overflow: hidden; clip-path: inset(50%); white-space: nowrap; }
@keyframes form-submit-spin { to { transform: rotate(360deg); } }
@media (prefers-reduced-motion: reduce) { .form-submit-spinner { animation: none; } }
</style>
