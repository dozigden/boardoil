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
        <button
          v-if="activeSystemInfoMessage"
          type="button"
          class="system-info-chip system-info-trigger"
          :class="systemInfoStyleClasses"
          :style="systemInfoStyle"
          @click="openSystemInfoDialog"
        >
          <span v-if="activeSystemInfoMessage.emoji" class="system-info-chip-emoji">{{ activeSystemInfoMessage.emoji }}</span>
          <strong>{{ activeSystemInfoMessage.title }}</strong>
        </button>
      </div>
      <div class="header-meta">
        <BoDropdown
          v-if="isAuthenticated"
          class="header-menu header-menu--user"
          align="right"
          icon-only
          label="User menu"
          :icon="CircleUserRound"
        >
          <template #icon>
            <UserAvatar
              :image-url="userProfileImageUrl"
              :display-name="userName"
              size="lg"
            />
          </template>
          <template #default="{ close }">
            <RouterLink :to="{ name: 'user-admin-profile' }" class="bo-dropdown-item" @click="close">User settings</RouterLink>
            <span class="bo-dropdown-divider" aria-hidden="true"></span>
            <button type="button" class="bo-dropdown-item" @click="handleLogout(close)">Logout</button>
          </template>
        </BoDropdown>
        <BoDropdown
          v-if="isAuthenticated"
          class="header-menu"
          align="right"
          icon-only
          label="System admin"
          :icon="Settings"
        >
          <template #default="{ close }">
            <RouterLink
              v-if="boardAdminTarget"
              :to="boardAdminTarget"
              class="bo-dropdown-item"
              @click="close"
            >
              Board Configuration
            </RouterLink>
            <RouterLink
              v-if="isAdmin"
              :to="{ name: 'system-admin-boards' }"
              class="bo-dropdown-item"
              @click="close"
            >
              System Settings
            </RouterLink>
            <span class="bo-dropdown-divider" aria-hidden="true"></span>
            <RouterLink to="/licences" class="bo-dropdown-item" @click="close">Licences</RouterLink>
            <button type="button" class="bo-dropdown-item" @click="openAboutDialog(close)">About</button>
          </template>
        </BoDropdown>
      </div>
    </div>
  </header>
  <AboutDialog :open="aboutDialogOpen" @close="closeAboutDialog" />
  <ModalDialog
    :open="systemInfoDialogOpen"
    :title="systemInfoDialogTitle"
    close-label="Close system information"
    @close="closeSystemInfoDialog"
  >
    <section v-if="activeSystemInfoMessage" class="system-info-dialog">
      <MdViewer
        :model-value="activeSystemInfoMessage.description"
        aria-label="System information"
      />
    </section>
  </ModalDialog>
</template>

<script setup lang="ts">
import { CircleUserRound, Settings } from 'lucide-vue-next';
import { storeToRefs } from 'pinia';
import { computed, onMounted, ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import AboutDialog from './AboutDialog.vue';
import BoardOilDrop from './BoardOilDrop.vue';
import BoardOilLogo from './BoardOilLogo.vue';
import BoDropdown from '../../shared/components/BoDropdown.vue';
import UserAvatar from '../../shared/components/UserAvatar.vue';
import HeaderBoardPicker from './HeaderBoardPicker.vue';
import MdViewer from '../../shared/components/MdViewer.vue';
import ModalDialog from '../../shared/components/ModalDialog.vue';
import { getBrandTarget } from './appHeaderNavigation';
import { useAuthStore } from '../../shared/stores/authStore';
import { useUserProfileImageStore } from '../../shared/stores/userProfileImageStore';
import { useBoardCatalogueStore } from '../../shared/stores/boardCatalogueStore';
import { useBoardStore } from '../../board/stores/boardStore';
import { useSystemInfoMessageStore } from '../../shared/stores/systemInfoMessageStore';
import { getSemanticStyleClasses, getSurfaceStyle } from '../../shared/utils/styleRenderer';
import type { StylePresentation } from '../../shared/utils/styleTypes';
const aboutDialogOpen = ref(false);
const systemInfoDialogOpen = ref(false);
const router = useRouter();
const authStore = useAuthStore();
const userProfileImageStore = useUserProfileImageStore();
const boardCatalogueStore = useBoardCatalogueStore();
const boardStore = useBoardStore();
const systemInfoMessageStore = useSystemInfoMessageStore();
const { user, isAuthenticated, isAdmin } = storeToRefs(authStore);
const { userProfileImageUrl } = storeToRefs(userProfileImageStore);
const { boards } = storeToRefs(boardCatalogueStore);
const { board, currentBoardId } = storeToRefs(boardStore);
const { message: systemInfoMessage } = storeToRefs(systemInfoMessageStore);
const userName = computed(() => user.value?.displayName ?? user.value?.userName ?? '');
const brandTarget = computed(() => getBrandTarget(boards.value));
const activeSystemInfoMessage = computed(() => {
  if (!systemInfoMessage.value?.enabled) {
    return null;
  }

  return systemInfoMessage.value;
});
const systemInfoStylePresentation = computed<StylePresentation | null>(() => {
  if (!activeSystemInfoMessage.value) {
    return null;
  }

  return {
    styleName: activeSystemInfoMessage.value.styleName,
    stylePropertiesJson: activeSystemInfoMessage.value.stylePropertiesJson
  };
});
const systemInfoStyle = computed(() => getSurfaceStyle(systemInfoStylePresentation.value, {
  fallbackBackground: 'var(--bo-surface-chip)',
  fallbackColor: 'var(--bo-ink-default)',
  fallbackBorderColor: 'var(--bo-border-soft)'
}));
const systemInfoStyleClasses = computed(() => getSemanticStyleClasses(systemInfoStylePresentation.value, 'card'));
const systemInfoDialogTitle = computed(() => {
  const title = activeSystemInfoMessage.value?.title ?? '';
  const emoji = activeSystemInfoMessage.value?.emoji?.trim();
  if (emoji && emoji.length > 0) {
    return `${emoji} ${title}`;
  }

  return title;
});
const boardAdminTarget = computed(() =>
  currentBoardId.value !== null && board.value
    ? {
        name: 'board-details',
        params: { boardId: currentBoardId.value }
      }
    : null
);

async function handleLogout(close?: () => void) {
  close?.();
  await authStore.logout();
  await router.replace({ name: 'login' });
}

async function openAboutDialog(close?: () => void) {
  close?.();
  aboutDialogOpen.value = true;
}

function closeAboutDialog() {
  aboutDialogOpen.value = false;
}

function openSystemInfoDialog() {
  if (!activeSystemInfoMessage.value) {
    return;
  }

  systemInfoDialogOpen.value = true;
}

function closeSystemInfoDialog() {
  systemInfoDialogOpen.value = false;
}

onMounted(async () => {
  if (!isAuthenticated.value) {
    return;
  }

  await systemInfoMessageStore.load();
});

watch(isAuthenticated, async authenticated => {
  if (!authenticated) {
    systemInfoMessageStore.clear();
    systemInfoDialogOpen.value = false;
    return;
  }

  await systemInfoMessageStore.load();
});

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

.system-info-chip {
  margin: 0;
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  border: 1px solid transparent;
  border-radius: 0.35rem;
  padding: 0.5rem 0.8rem;
  font-size: 0.85rem;
}

.system-info-chip-emoji {
  line-height: 1;
}

.system-info-trigger {
  margin: 0;
  cursor: pointer;
}

.system-info-dialog {
  display: grid;
  gap: 0.6rem;
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
  --boardoil-logo-size: 2rem;
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

.header-menu--user :deep(.bo-dropdown-trigger) {
  width: 2rem;
  height: 2rem;
  padding: 0;
  border-radius: 999px;
  overflow: hidden;
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

@media (max-width: 767px) {
  .app-header {
    padding: 0.6rem 0.75rem;
  }

  .header-top {
    align-items: center;
    flex-wrap: wrap;
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
    --boardoil-logo-size: 1.75rem;
  }

  .header-meta {
    width: auto;
    justify-content: flex-end;
    flex-wrap: nowrap;
    gap: 0.35rem;
    margin-left: 0.25rem;
    min-height: 0;
  }

  .header-menu {
    margin-left: 0;
  }

  .header-board-admin-link {
    margin-left: 0;
  }

  .system-info-chip {
    font-size: 0.8rem;
    padding: 0.4rem 0.65rem;
  }
}
</style>
