<template>
  <button type="button" class="bo-dropdown-item" :class="{ 'bo-dropdown-item--danger': danger }"
    :role="selected === undefined ? undefined : 'menuitemradio'" :aria-checked="selected">
    <component v-if="icon" :is="icon" class="bo-dropdown-item-icon" :size="16" aria-hidden="true" />
    <span class="bo-dropdown-item-main bo-dropdown-item-content"><slot /></span>
    <span v-if="selected !== undefined" class="bo-dropdown-item-meta bo-dropdown-item-check" aria-hidden="true">
      <Check v-if="selected" :size="16" />
    </span>
  </button>
</template>

<script setup lang="ts">
import { Check } from '@lucide/vue';
import type { Component } from 'vue';

withDefaults(defineProps<{ selected?: boolean; icon?: Component; danger?: boolean }>(), { selected: undefined, danger: false });
</script>

<style scoped>
.bo-dropdown-item {
  display: flex;
  align-items: center;
  gap: 0.4rem;
}

.bo-dropdown-item-icon {
  flex: 0 0 auto;
}

.bo-dropdown-item--danger:not(:disabled) {
  --bo-dropdown-item-ink: var(--bo-colour-danger-strong);
  --bo-dropdown-item-hover-ink: var(--bo-colour-danger-strong);
}

.bo-dropdown-item-content {
  display: flex;
  align-items: center;
  flex: 1;
  min-width: 0;
}

.bo-dropdown-item-check {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 1rem;
  height: 1rem;
}
</style>
