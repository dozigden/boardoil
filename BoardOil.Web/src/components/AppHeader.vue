<template>
  <header class="app-header">
    <div class="header-top">
      <h1 class="brand-title">
        <RouterLink to="/" class="brand-link">
          <SoapDispenserDroplet class="brand-icon" :size="26" aria-hidden="true" />
          <span>BoardOil</span>
        </RouterLink>
      </h1>
      <details ref="menu" class="header-menu">
        <summary class="menu-trigger" aria-label="Open menu">
          <Settings :size="18" aria-hidden="true" />
        </summary>
        <nav class="menu-panel" aria-label="Site menu">
          <RouterLink to="/columns" class="menu-item" @click="closeMenu">Manage Columns</RouterLink>
        </nav>
      </details>
    </div>
    <p>Make your stories full bodied and shiny!!</p>
  </header>
</template>

<script setup lang="ts">
import { Settings, SoapDispenserDroplet } from 'lucide-vue-next';
import { ref } from 'vue';

const menu = ref<HTMLDetailsElement | null>(null);

function closeMenu() {
  if (menu.value) {
    menu.value.open = false;
  }
}
</script>
