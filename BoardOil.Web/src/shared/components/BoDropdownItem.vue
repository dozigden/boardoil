<template>
  <button type="button" class="bo-dropdown-item" :role="selected === undefined ? 'menuitem' : 'menuitemradio'" :aria-checked="selected">
    <span class="bo-dropdown-item-main bo-dropdown-item-content"><slot /></span>
    <span v-if="selected !== undefined" class="bo-dropdown-item-meta bo-dropdown-item-check" aria-hidden="true">
      <Check v-if="selected" :size="16" />
    </span>
  </button>
</template>

<script setup lang="ts">
import { Check } from '@lucide/vue';

withDefaults(defineProps<{ selected?: boolean }>(), { selected: undefined });
</script>

<style scoped>
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
