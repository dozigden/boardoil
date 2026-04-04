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
        <p v-if="isAuthenticated && userName" class="user-meta">
          {{ userName }}
        </p>
        <details v-if="isAuthenticated" ref="userMenu" class="header-menu">
          <summary class="btn btn--secondary btn--icon menu-trigger" aria-label="Open user menu" title="User menu">
            <CircleUserRound :size="18" aria-hidden="true" />
          </summary>
          <nav class="menu-panel" aria-label="User menu">
            <button v-if="isAuthenticated" type="button" class="btn btn--menu-item" @click="openAboutDialog">About</button>
            <RouterLink v-if="isAuthenticated" to="/licences" class="menu-item" @click="closeMenus">Licences</RouterLink>
            <button v-if="isAuthenticated" type="button" class="btn btn--menu-item" @click="handleLogout">Logout</button>
          </nav>
        </details>
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
      </div>
    </div>
  </header>
  <AboutDialog :open="aboutDialogOpen" @close="closeAboutDialog" />
</template>

<script setup lang="ts">
import { CircleUserRound, Settings, SlidersHorizontal } from 'lucide-vue-next';
import { storeToRefs } from 'pinia';
import { computed, ref } from 'vue';
import { useRouter } from 'vue-router';
import AboutDialog from './AboutDialog.vue';
import BoardOilDrop from './BoardOilDrop.vue';
import BoardOilLogo from './BoardOilLogo.vue';
import HeaderBoardPicker from './HeaderBoardPicker.vue';
import { getBrandTarget } from './appHeaderNavigation';
import { useAuthStore } from '../stores/authStore';
import { useBoardCatalogueStore } from '../stores/boardCatalogueStore';
import { useBoardStore } from '../stores/boardStore';

const userMenu = ref<HTMLDetailsElement | null>(null);
const aboutDialogOpen = ref(false);
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
  currentBoardId.value !== null && board.value
    ? {
        name: 'board-details',
        params: { boardId: currentBoardId.value }
      }
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

async function openAboutDialog() {
  closeMenus();
  aboutDialogOpen.value = true;
}

function closeAboutDialog() {
  aboutDialogOpen.value = false;
}
</script>

<style scoped>
.app-header {
  margin: 0 0 1rem;
  padding: 1rem 1.5rem;
  background: var(--bo-surface-panel-strong);
  border-bottom: 1px solid var(--bo-border-brand);
}

.app-header h1 {
  margin: 0;
  font-size: 2rem;
}

.header-top {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
}

.header-primary {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.75rem;
  flex: 1 1 auto;
  min-width: 0;
}

.header-meta {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  flex-wrap: wrap;
  gap: 0.5rem;
  min-height: 2rem;
  margin-left: auto;
  flex: 0 0 auto;
}

.brand-title {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  line-height: 1;
  flex: 0 0 auto;
  min-width: 0;
}

.brand-link {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  line-height: 1;
  color: var(--bo-link);
  text-decoration: none;
  min-width: 0;
}

.brand-wordmark {
  display: flex;
  align-items: baseline;
  gap: 0;
  line-height: 1;
}

.brand-wordmark > span {
  display: block;
  line-height: 1;
}

.brand-wordmark-drop {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 0.86em;
  height: 0.86em;
  align-self: baseline;
  flex: 0 0 auto;
  margin-top: -0.12em;
  margin-right: -0.12em;
}

.brand-logo {
  --boardoil-logo-size: 60px;
  display: block;
  align-self: center;
  flex: 0 0 auto;
}

.header-board-admin-link {
  margin-left: 0.25rem;
}

.app-header p {
  margin: 0.25rem 0 0;
  color: var(--bo-ink-default);
}

.user-meta {
  display: flex;
  align-items: center;
  height: 2rem;
  margin: 0;
  font-size: 0.85rem;
  line-height: 1;
  color: var(--bo-ink-muted);
  white-space: nowrap;
}

.header-menu {
  display: flex;
  align-items: center;
  height: 2rem;
  position: relative;
}

.menu-trigger {
  list-style: none;
  user-select: none;
  text-decoration: none;
}

.menu-trigger::-webkit-details-marker {
  display: none;
}

.menu-panel {
  position: absolute;
  right: 0;
  top: calc(100% + 0.35rem);
  min-width: 11rem;
  background: var(--bo-surface-base);
  border: 1px solid var(--bo-border-default);
  border-radius: 8px;
  padding: 0.35rem;
  box-shadow: var(--bo-shadow-pop);
  z-index: 10;
}

.menu-item {
  display: block;
  text-decoration: none;
  color: var(--bo-ink-default);
  border-radius: 6px;
  padding: 0.45rem 0.55rem;
}

.menu-item:hover,
.menu-item:focus-visible {
  background: var(--bo-surface-energy);
  color: var(--bo-colour-energy);
}

@media (max-width: 720px) {
  .app-header {
    margin-bottom: 0;
    padding: 0.6rem 0.75rem;
  }

  .header-top {
    align-items: center;
    flex-wrap: nowrap;
    gap: 0.35rem;
  }

  .header-primary {
    align-items: center;
    flex-wrap: nowrap;
    gap: 0.35rem;
    min-width: 0;
  }

  .brand-title {
    flex: 0 0 auto;
  }

  .brand-wordmark {
    display: none;
  }

  .brand-logo {
    --boardoil-logo-size: 36px;
  }

  .header-meta {
    width: auto;
    justify-content: flex-end;
    flex-wrap: nowrap;
    gap: 0.35rem;
    margin-left: 0.25rem;
    min-height: 0;
  }

  .user-meta {
    display: none;
  }

  .header-menu {
    margin-left: 0;
  }

  .header-board-admin-link {
    margin-left: 0;
  }
}
</style>
