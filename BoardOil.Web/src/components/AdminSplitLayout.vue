<template>
  <section class="admin-shell">
    <aside class="admin-nav" :aria-label="`${title} sections`">
      <div class="admin-nav-heading">
        <RouterLink
          v-if="backTo"
          :to="backTo"
          class="admin-nav-back"
          :aria-label="backLabel"
          :title="backLabel"
        >
          ←
        </RouterLink>
        <h2 class="admin-nav-title">{{ title }}</h2>
      </div>
      <nav class="admin-nav-links">
        <RouterLink
          v-for="item in items"
          :key="item.label"
          :to="item.to"
          :class="{ 'admin-nav-link--active': isItemActive(item) }"
          class="admin-nav-link"
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
import { useRoute } from 'vue-router';

type AdminNavItem = {
  label: string;
  to: RouteLocationRaw;
  activeRouteNames?: string[];
};

withDefaults(defineProps<{
  title: string;
  items: AdminNavItem[];
  backTo?: RouteLocationRaw | null;
  backLabel?: string;
}>(), {
  backTo: null,
  backLabel: 'Back'
});

const route = useRoute();

function isItemActive(item: AdminNavItem) {
  const currentRouteName = typeof route.name === 'string' ? route.name : null;
  if (currentRouteName === null) {
    return false;
  }

  if (item.activeRouteNames?.includes(currentRouteName)) {
    return true;
  }

  const targetRouteName = tryGetTargetRouteName(item.to);
  return targetRouteName === currentRouteName;
}

function tryGetTargetRouteName(target: RouteLocationRaw) {
  if (typeof target !== 'object' || target === null || !('name' in target)) {
    return null;
  }

  const name = target.name;
  return typeof name === 'string' ? name : null;
}
</script>

<style scoped>
.admin-shell {
  display: grid;
  grid-template-columns: minmax(220px, 260px) minmax(0, 1fr);
  gap: 1rem;
  min-height: 0;
  height: 100%;
}

.admin-nav {
  background: var(--bo-surface-panel);
  border: 1px solid var(--bo-border-soft);
  border-radius: 14px;
  padding: 0.75rem;
  display: grid;
  align-content: start;
  gap: 0.75rem;
  overflow-y: auto;
}

.admin-nav-title {
  margin: 0;
  font-size: 1.05rem;
}

.admin-nav-heading {
  display: flex;
  align-items: center;
  gap: 0.45rem;
}

.admin-nav-back {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 1.9rem;
  height: 1.9rem;
  border: 1px solid var(--bo-border-default);
  border-radius: 8px;
  color: var(--bo-ink-default);
  text-decoration: none;
  background: var(--bo-surface-card);
  font-weight: 700;
  line-height: 1;
}

.admin-nav-back:hover,
.admin-nav-back:focus-visible {
  background: var(--bo-surface-energy);
  border-color: var(--bo-colour-energy);
  color: var(--bo-colour-energy);
}

.admin-nav-links {
  display: grid;
  gap: 0.35rem;
}

.admin-nav-link {
  display: block;
  padding: 0.5rem 0.6rem;
  border: 1px solid transparent;
  border-radius: 8px;
  color: var(--bo-ink-default);
  text-decoration: none;
}

.admin-nav-link:hover,
.admin-nav-link:focus-visible {
  background: var(--bo-surface-energy);
  color: var(--bo-colour-energy);
}

.admin-nav-link--active {
  background: var(--bo-surface-brand);
  color: var(--bo-link);
  border-color: var(--bo-border-brand);
  font-weight: 700;
}

.admin-content {
  min-height: 0;
  overflow-y: auto;
  padding-right: 0.25rem;
  scrollbar-width: thin;
  scrollbar-color: var(--bo-border-default) transparent;
}

@media (max-width: 720px) {
  .admin-shell {
    grid-template-columns: 1fr;
    height: auto;
  }

  .admin-nav {
    overflow: visible;
  }

  .admin-content {
    overflow: visible;
    padding-right: 0;
  }
}
</style>
