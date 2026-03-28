<template>
  <header class="app-header">
    <div class="header-top">
      <h1 class="brand-title">
        <RouterLink to="/" class="brand-link" aria-label="Board Oil">
          <BoardOilLogo class="brand-logo" />
          <span class="brand-wordmark">
            <span>Board </span>
            <BoardOilDrop class="brand-wordmark-drop" />
            <span>il</span>
          </span>
        </RouterLink>
      </h1>
      <div class="header-meta">
        <p v-if="isAuthenticated && userName" class="user-meta">Signed in as {{ userName }}</p>
        <details v-if="isAuthenticated" ref="menu" class="header-menu">
          <summary class="menu-trigger" aria-label="Open menu">
            <Settings :size="18" aria-hidden="true" />
          </summary>
          <nav class="menu-panel" aria-label="Site menu">
            <RouterLink v-if="isAuthenticated" to="/tags" class="menu-item" @click="closeMenu">Manage Tags</RouterLink>
            <RouterLink
              v-if="isAdmin && currentBoardId !== null"
              :to="{ name: 'columns', params: { boardId: currentBoardId } }"
              class="menu-item"
              @click="closeMenu"
            >
              Manage Columns
            </RouterLink>
            <RouterLink v-if="isAdmin" to="/users" class="menu-item" @click="closeMenu">Manage Users</RouterLink>
            <RouterLink v-if="isAdmin" to="/machine-access" class="menu-item" @click="closeMenu">Machine Access</RouterLink>
            <RouterLink v-if="isAdmin" to="/configuration" class="menu-item" @click="closeMenu">Configuration</RouterLink>
            <RouterLink v-if="isAuthenticated" to="/licences" class="menu-item" @click="closeMenu">Licences</RouterLink>
            <button v-if="isAuthenticated" type="button" class="menu-item menu-button" @click="handleLogout">Logout</button>
          </nav>
        </details>
      </div>
    </div>
  </header>
</template>

<script setup lang="ts">
import { Settings } from 'lucide-vue-next';
import { storeToRefs } from 'pinia';
import { computed, ref } from 'vue';
import { useRouter } from 'vue-router';
import BoardOilDrop from './BoardOilDrop.vue';
import BoardOilLogo from './BoardOilLogo.vue';
import { useAuthStore } from '../stores/authStore';
import { useBoardStore } from '../stores/boardStore';

const menu = ref<HTMLDetailsElement | null>(null);
const router = useRouter();
const authStore = useAuthStore();
const boardStore = useBoardStore();
const { user, isAuthenticated, isAdmin } = storeToRefs(authStore);
const { currentBoardId } = storeToRefs(boardStore);
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
