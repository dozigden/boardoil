<template>
  <section class="admin-shell">
    <aside class="admin-nav" :aria-label="`${title} sections`">
      <h2 class="admin-nav-title">{{ title }}</h2>
      <nav class="admin-nav-links">
        <RouterLink
          v-for="item in items"
          :key="item.label"
          :to="item.to"
          class="admin-nav-link"
          active-class="admin-nav-link--active"
        >
          {{ item.label }}
        </RouterLink>
      </nav>
    </aside>
    <section class="admin-content">
      <slot />
    </section>
  </section>
</template>

<script setup lang="ts">
import type { RouteLocationRaw } from 'vue-router';

type AdminNavItem = {
  label: string;
  to: RouteLocationRaw;
};

defineProps<{
  title: string;
  items: AdminNavItem[];
}>();
</script>
