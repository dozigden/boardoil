<template>
  <TestTubeDiagonal
    class="experimental-feature-icon"
    :size="size"
    role="img"
    aria-label="Experimental feature"
  />
</template>

<script setup lang="ts">
import { TestTubeDiagonal } from '@lucide/vue';

withDefaults(defineProps<{ size?: number }>(), { size: 16 });
</script>

<style scoped>
.experimental-feature-icon { flex-shrink: 0; vertical-align: middle; }
</style>
