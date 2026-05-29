<template>
  <section class="entity-rows-page">
    <header class="entity-rows-header">
      <h2>Members</h2>
      <button
        v-if="isCurrentUserOwner"
        type="button"
        class="btn"
        :disabled="busy"
        @click="openAddMemberDialog"
      >
        Add member
      </button>
    </header>

    <p v-if="!isCurrentUserOwner" class="entity-rows-empty">Owner permission required to manage members.</p>

    <template v-else>
      <section class="entity-rows-list">
        <article v-for="member in members" :key="member.userId" class="entity-row">
          <button
            type="button"
            class="entity-row-main entity-row-main-button"
            :disabled="busy"
            :aria-label="`Edit member ${member.displayName}`"
            @click="focusMemberRoleControl(member.userId)"
          >
            <span class="member-row-leading">
              <UserAvatar
                :image-relative-path="member.profileImageRelativePath ?? null"
                :display-name="member.displayName"
                size="lg"
                class="member-avatar"
              />
              <span class="member-row-meta">
                <span class="entity-row-title">{{ member.displayName }}</span>
                <span class="member-username">@{{ member.userName }}</span>
                <span class="badge">#{{ member.userId }}</span>
              </span>
            </span>
          </button>
          <div class="entity-row-actions">
            <select
              :id="`board-member-role-${member.userId}`"
              :value="member.role"
              :disabled="busy"
              @change="onRoleChange(member.userId)"
            >
              <option value="Contributor">Contributor</option>
              <option value="Owner">Owner</option>
            </select>
            <button
              type="button"
              class="btn btn--danger"
              :disabled="busy"
              @click="removeMember(member)"
            >
              Remove
            </button>
          </div>
        </article>
      </section>

      <AddBoardMemberDialog
        :open="isAddMemberDialogOpen"
        :busy="busy || usersBusy"
        :users="users"
        @close="closeAddMemberDialog"
        @submit="addMember"
      />
    </template>
  </section>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { onUnmounted, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { createUsersApi } from '../../shared/api/usersApi';
import UserAvatar from '../../shared/components/UserAvatar.vue';
import AddBoardMemberDialog from '../../system/components/AddBoardMemberDialog.vue';
import { useConfirm } from '../../shared/composables/useConfirm';
import { useBoardMembersStore } from '../stores/boardMembersStore';
import { useBoardStore } from '../stores/boardStore';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import type { UserDirectoryEntry } from '../../shared/types/authTypes';
import type {
  BoardMember,
  BoardMemberEditModel,
  BoardMemberRole,
} from '../../shared/types/boardTypes';

const route = useRoute();
const router = useRouter();
const boardStore = useBoardStore();
const boardMembersStore = useBoardMembersStore();
const { confirm } = useConfirm();
const usersApi = createUsersApi();
const feedback = useUiFeedbackStore();
const { isCurrentUserOwner } = storeToRefs(boardStore);
const { members, busy } = storeToRefs(boardMembersStore);
const users = ref<UserDirectoryEntry[]>([]);
const usersBusy = ref(false);
const isAddMemberDialogOpen = ref(false);

onUnmounted(() => {
  boardMembersStore.dispose();
});

watch(
  () => route.params.boardId,
  async () => {
    const boardId = resolveBoardId();
    if (boardId === null) {
      await router.replace({ name: 'boards' });
      return;
    }

    const loaded = await boardStore.initialize(boardId);
    if (!loaded && resolveBoardId() === boardId) {
      await router.replace({ name: 'boards' });
      return;
    }

    await boardMembersStore.loadMembers(boardId);
    await loadUsers();
  },
  { immediate: true }
);

async function openAddMemberDialog() {
  await loadUsers();
  isAddMemberDialogOpen.value = true;
}

function closeAddMemberDialog() {
  isAddMemberDialogOpen.value = false;
}

async function addMember(model: BoardMemberEditModel) {
  const added = await boardMembersStore.addMember(model);
  if (!added) {
    return;
  }

  isAddMemberDialogOpen.value = false;
}

async function updateRole(userId: number, role: BoardMemberRole) {
  const model: BoardMemberEditModel = {
    userId,
    role
  };
  await boardMembersStore.updateMemberRole(model);
}

function onRoleChange(userId: number) {
  const roleControl = document.getElementById(`board-member-role-${userId}`);
  if (!(roleControl instanceof HTMLSelectElement)) {
    return;
  }

  void updateRole(userId, roleControl.value as BoardMemberRole);
}

async function removeMember(member: BoardMember) {
  const shouldRemove = await confirm({
    title: 'Remove board member',
    message: `Remove ${member.displayName} from this board?`,
    confirmLabel: 'Remove',
    danger: true
  });
  if (!shouldRemove) {
    return;
  }

  await boardMembersStore.removeMember(member.userId);
}

function focusMemberRoleControl(userId: number) {
  if (busy.value) {
    return;
  }

  const roleControl = document.getElementById(`board-member-role-${userId}`);
  if (roleControl instanceof HTMLSelectElement) {
    roleControl.focus();
  }
}

function resolveBoardId() {
  const parsed = Number.parseInt(String(route.params.boardId ?? ''), 10);
  return Number.isFinite(parsed) ? parsed : null;
}

async function loadUsers() {
  usersBusy.value = true;
  try {
    const result = await usersApi.getAllUsers();
    if (!result.ok) {
      feedback.setError(result.error.message);
      users.value = [];
      return false;
    }

    users.value = [...result.data].sort((left, right) => left.displayName.localeCompare(right.displayName));
    return true;
  } finally {
    usersBusy.value = false;
  }
}
</script>

<style scoped>
.member-row-leading {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
}

.member-row-meta {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  min-width: 0;
}

.member-avatar {
  flex-shrink: 0;
}

.member-username {
  color: var(--bo-ink-muted);
  font-size: 0.85rem;
}
</style>
