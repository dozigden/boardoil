<template>
  <header class="app-header">
    <div class="header-top">
      <div class="header-primary">
        <h1 class="brand-title">
          <RouterLink :to="brandTarget" class="brand-link" aria-label="Board Oil">
            <BoardOilLogo class="brand-logo" />
            <span class="brand-wordmark">
              <span>Board </span>
              <BoardOilDrop class="brand-wordmark-drop" />
              <span>il</span>
            </span>
          </RouterLink>
        </h1>
        <HeaderBoardPicker
          :is-authenticated="isAuthenticated"
          :board="board"
          :boards="boards"
          :current-board-id="currentBoardId"
        />
        <RouterLink
          v-if="boardAdminTarget"
          :to="boardAdminTarget"
          class="btn btn--secondary btn--icon menu-trigger header-board-admin-link"
          aria-label="Open board admin"
          title="Board admin"
          @click="closeMenus"
        >
          <SlidersHorizontal :size="18" aria-hidden="true" />
        </RouterLink>
      </div>
      <div class="header-meta">
        <p v-if="isAuthenticated && userName" class="user-meta">Signed in as {{ userName }}</p>
        <RouterLink
          v-if="isAdmin"
          :to="{ name: 'system-admin-boards' }"
          class="btn btn--secondary btn--icon menu-trigger"
          aria-label="Open system admin"
          title="System admin"
          @click="closeMenus"
        >
            <Settings :size="18" aria-hidden="true" />
        </RouterLink>
        <details v-if="isAuthenticated" ref="userMenu" class="header-menu">
          <summary class="btn btn--secondary btn--icon menu-trigger" aria-label="Open user menu" title="User menu">
            <CircleUserRound :size="18" aria-hidden="true" />
          </summary>
          <nav class="menu-panel" aria-label="User menu">
            <RouterLink v-if="isAuthenticated" to="/licences" class="menu-item" @click="closeMenus">Licences</RouterLink>
            <button v-if="isAuthenticated" type="button" class="btn btn--menu-item" @click="handleLogout">Logout</button>
          </nav>
        </details>
      </div>
    </div>
  </header>
</template>

<script setup lang="ts">
import { CircleUserRound, Settings, SlidersHorizontal } from 'lucide-vue-next';
import { storeToRefs } from 'pinia';
import { computed, ref } from 'vue';
import { useRouter } from 'vue-router';
import BoardOilDrop from './BoardOilDrop.vue';
import BoardOilLogo from './BoardOilLogo.vue';
import HeaderBoardPicker from './HeaderBoardPicker.vue';
import { getBrandTarget } from './appHeaderNavigation';
import { useAuthStore } from '../stores/authStore';
import { useBoardCatalogueStore } from '../stores/boardCatalogueStore';
import { useBoardStore } from '../stores/boardStore';

const userMenu = ref<HTMLDetailsElement | null>(null);
const router = useRouter();
const authStore = useAuthStore();
const boardCatalogueStore = useBoardCatalogueStore();
const boardStore = useBoardStore();
const { user, isAuthenticated, isAdmin } = storeToRefs(authStore);
const { boards } = storeToRefs(boardCatalogueStore);
const { board, currentBoardId } = storeToRefs(boardStore);
const userName = computed(() => user.value?.userName ?? '');
const brandTarget = computed(() => getBrandTarget(boards.value));
const boardAdminTarget = computed(() =>
  isAdmin.value && currentBoardId.value !== null
    ? { name: 'columns', params: { boardId: currentBoardId.value } }
    : null
);

function closeMenu() {
  if (userMenu.value) {
    userMenu.value.open = false;
  }
}

function closeMenus() {
  closeMenu();
}

async function handleLogout() {
  await authStore.logout();
  closeMenus();
  await router.replace({ name: 'login' });
}
</script>
