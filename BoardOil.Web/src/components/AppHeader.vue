<template>
  <header class="app-header">
    <h1>BoardOil</h1>
    <p>Live board for small trusted teams.</p>
    <nav class="view-switch">
      <button :class="{ active: currentView === 'board' }" type="button" @click="emit('change-view', 'board')">
        Board
      </button>
      <button :class="{ active: currentView === 'columns' }" type="button" @click="emit('change-view', 'columns')">
        Manage Columns
      </button>
    </nav>
  </header>
</template>

<script setup lang="ts">
import type { ViewMode } from '../composables/useViewHash';

defineProps<{ currentView: ViewMode }>();

const emit = defineEmits<{
  'change-view': [view: ViewMode];
}>();
</script>
