<template>
  <section class="admin-shell">
    <header class="admin-mobile-bar">
      <div class="admin-mobile-left">
        <button
          type="button"
          class="btn btn--secondary btn--icon admin-mobile-toggle"
          :aria-controls="mobileNavId"
          :aria-expanded="mobileNavOpen"
          aria-label="Toggle section navigation"
          @click="mobileNavOpen = !mobileNavOpen"
        >
          <X v-if="mobileNavOpen" :size="18" aria-hidden="true" />
          <Menu v-else :size="18" aria-hidden="true" />
        </button>
        <h2 class="admin-mobile-page-title">{{ mobilePageTitle }}</h2>
      </div>
      <div class="admin-mobile-right">
        <span class="admin-mobile-context">{{ props.title }}</span>
      </div>
    </header>

    <aside :id="mobileNavId" class="admin-nav" :class="{ 'admin-nav--open': mobileNavOpen }" :aria-label="`${props.title} sections`">
      <h2 class="admin-nav-title">{{ props.title }}</h2>
      <nav class="admin-nav-links">
        <RouterLink
          v-for="item in props.items"
          :key="item.label"
          :to="item.to"
          :class="{ 'admin-nav-link--active': isItemActive(item) }"
          class="admin-nav-link"
          @click="closeMobileNav"
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
import { Menu, X } from 'lucide-vue-next';
import { computed, ref, watch } from 'vue';
import type { RouteLocationRaw } from 'vue-router';
import { useRoute } from 'vue-router';

type AdminNavItem = {
  label: string;
  to: RouteLocationRaw;
  activeRouteNames?: string[];
};

const props = defineProps<{
  title: string;
  items: AdminNavItem[];
}>();

const route = useRoute();
const mobileNavId = 'admin-mobile-nav';
const mobileNavOpen = ref(false);
const mobilePageTitle = computed(() => {
  const activeItem = props.items.find(item => isItemActive(item));
  return activeItem?.label ?? props.title;
});

watch(
  () => route.fullPath,
  () => {
    mobileNavOpen.value = false;
  }
);

function closeMobileNav() {
  mobileNavOpen.value = false;
}

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

.admin-mobile-bar {
  display: none;
}

.admin-mobile-left {
  display: flex;
  align-items: center;
  gap: 0.45rem;
  min-width: 0;
}

.admin-mobile-right {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 0.5rem;
  flex: 0 0 auto;
}

.admin-mobile-toggle {
  flex: 0 0 auto;
}

.admin-mobile-page-title {
  margin: 0;
  font-size: 1rem;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.admin-mobile-context {
  font-size: 0.85rem;
  font-weight: 700;
  color: var(--bo-ink-muted);
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
    gap: 0.6rem;
  }

  .admin-mobile-bar {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 0.5rem;
    background: var(--bo-surface-panel);
    border-bottom: 1px solid var(--bo-border-soft);
    padding: 0.45rem 0.75rem;
  }

  .admin-nav {
    display: none;
    border-top: none;
    border-left: none;
    border-right: none;
    border-radius: 0 0 10px 10px;
    padding: 0.5rem 0.75rem;
    overflow: visible;
  }

  .admin-nav--open {
    display: grid;
  }

  .admin-content {
    overflow: visible;
    padding-right: 0;
    padding-inline: 0.75rem;
  }
}
</style>
