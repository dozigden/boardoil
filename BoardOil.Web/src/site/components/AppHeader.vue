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
      </div>
      <div class="header-meta">
        <p v-if="isAuthenticated && userName" class="user-meta">
          {{ userName }}
        </p>
        <BoDropdown
          v-if="isAuthenticated"
          class="header-menu header-menu--user"
          align="right"
          icon-only
          label="User menu"
          :icon="CircleUserRound"
        >
          <template #icon>
            <img
              v-if="userProfileImageUrl"
              :src="userProfileImageUrl"
              alt="User profile image"
              class="user-avatar"
            />
            <CircleUserRound v-else :size="18" aria-hidden="true" />
          </template>
          <template #default="{ close }">
            <button type="button" class="bo-dropdown-item" @click="openUserImagePicker(close)">User image</button>
            <span class="bo-dropdown-divider" aria-hidden="true"></span>
            <button type="button" class="bo-dropdown-item" @click="openPasswordResetDialog(close)">Reset password</button>
            <span class="bo-dropdown-divider" aria-hidden="true"></span>
            <RouterLink :to="{ name: 'access-tokens' }" class="bo-dropdown-item" @click="close">Access tokens</RouterLink>
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
  <input
    ref="userImageInput"
    type="file"
    accept="image/png,image/jpeg,image/webp"
    class="user-image-input"
    @change="onUserImageSelected"
  />
  <AboutDialog :open="aboutDialogOpen" @close="closeAboutDialog" />
  <PasswordResetDialog
    :open="passwordResetDialogOpen"
    :busy="busy"
    mode="self"
    @close="closePasswordResetDialog"
    @submit="submitPasswordReset"
  />
  <ModalDialog
    :open="passwordResetSuccessDialogOpen"
    title="Password Reset Complete"
    close-label="Continue to login"
    @close="acknowledgePasswordReset"
    @submit="acknowledgePasswordReset"
  >
    <p class="password-reset-success-copy">
      Password reset successful. You are now signed out.
    </p>

    <template #actions>
      <div class="editor-actions card-modal-actions">
        <div class="card-modal-actions-left">
          <button type="submit" class="btn" aria-label="Continue to login" title="Continue to login">
            <span>OK</span>
          </button>
        </div>
      </div>
    </template>
  </ModalDialog>
</template>

<script setup lang="ts">
import { CircleUserRound, Settings } from 'lucide-vue-next';
import { storeToRefs } from 'pinia';
import { computed, ref } from 'vue';
import { useRouter } from 'vue-router';
import AboutDialog from './AboutDialog.vue';
import BoardOilDrop from './BoardOilDrop.vue';
import BoardOilLogo from './BoardOilLogo.vue';
import BoDropdown from '../../shared/components/BoDropdown.vue';
import HeaderBoardPicker from './HeaderBoardPicker.vue';
import ModalDialog from '../../shared/components/ModalDialog.vue';
import PasswordResetDialog from '../../shared/components/PasswordResetDialog.vue';
import { getBrandTarget } from './appHeaderNavigation';
import { useAuthStore } from '../../shared/stores/authStore';
import { useBoardCatalogueStore } from '../../shared/stores/boardCatalogueStore';
import { useBoardStore } from '../../board/stores/boardStore';
const aboutDialogOpen = ref(false);
const passwordResetDialogOpen = ref(false);
const passwordResetSuccessDialogOpen = ref(false);
const router = useRouter();
const authStore = useAuthStore();
const boardCatalogueStore = useBoardCatalogueStore();
const boardStore = useBoardStore();
const { user, isAuthenticated, isAdmin, busy, userProfileImageUrl } = storeToRefs(authStore);
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
const userImageInput = ref<HTMLInputElement | null>(null);

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

async function openPasswordResetDialog(close?: () => void) {
  close?.();
  passwordResetDialogOpen.value = true;
}

function closePasswordResetDialog() {
  passwordResetDialogOpen.value = false;
}

function openUserImagePicker(close?: () => void) {
  close?.();
  userImageInput.value?.click();
}

async function onUserImageSelected(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  if (!file) {
    return;
  }

  await authStore.uploadOwnProfileImage(file);
  input.value = '';
}

async function submitPasswordReset(payload: { currentPassword?: string; newPassword: string }) {
  if (!payload.currentPassword) {
    return;
  }

  const success = await authStore.changeOwnPassword(payload.currentPassword, payload.newPassword);
  if (!success) {
    return;
  }

  passwordResetDialogOpen.value = false;
  passwordResetSuccessDialogOpen.value = true;
}

async function acknowledgePasswordReset() {
  passwordResetSuccessDialogOpen.value = false;
  await router.replace({ name: 'login', query: { passwordReset: '1' } });
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

.user-avatar {
  width: 100%;
  height: 100%;
  border-radius: inherit;
  object-fit: cover;
  border: 0;
  background: var(--bo-surface-base);
}

.user-image-input {
  display: none;
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

.password-reset-success-copy {
  margin: 0 0 0.75rem;
  color: var(--bo-ink-muted);
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
