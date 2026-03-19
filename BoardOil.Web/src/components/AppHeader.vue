<template>
  <header class="app-header">
    <div class="header-top">
      <h1 class="brand-title">
        <RouterLink to="/" class="brand-link">
          <SoapDispenserDroplet class="brand-icon" :size="26" aria-hidden="true" />
          <span>BoardOil</span>
        </RouterLink>
      </h1>
      <details v-if="isAuthenticated" ref="menu" class="header-menu">
        <summary class="menu-trigger" aria-label="Open menu">
          <Settings :size="18" aria-hidden="true" />
        </summary>
        <nav class="menu-panel" aria-label="Site menu">
          <RouterLink v-if="isAdmin" to="/columns" class="menu-item" @click="closeMenu">Manage Columns</RouterLink>
          <RouterLink v-if="isAdmin" to="/users" class="menu-item" @click="closeMenu">Manage Users</RouterLink>
          <RouterLink v-if="isAdmin" to="/configuration" class="menu-item" @click="closeMenu">Configuration</RouterLink>
          <button v-if="isAuthenticated" type="button" class="menu-item menu-button" @click="handleLogout">Logout</button>
        </nav>
      </details>
    </div>
    <p v-if="isAuthenticated && userName" class="user-meta">Signed in as {{ userName }}</p>
    <p>Make your stories full bodied and shiny!!</p>
  </header>
</template>

<script setup lang="ts">
import { Settings, SoapDispenserDroplet } from 'lucide-vue-next';
import { storeToRefs } from 'pinia';
import { computed, ref } from 'vue';
import { useRouter } from 'vue-router';
import { useAuthStore } from '../stores/authStore';

const menu = ref<HTMLDetailsElement | null>(null);
const router = useRouter();
const authStore = useAuthStore();
const { user, isAuthenticated, isAdmin } = storeToRefs(authStore);
const userName = computed(() => user.value?.userName ?? '');

function closeMenu() {
  if (menu.value) {
    menu.value.open = false;
  }
}

async function handleLogout() {
  await authStore.logout();
  closeMenu();
  await router.replace({ name: 'login' });
}
</script>
