<template>
  <section class="entity-rows-page">
    <header class="entity-rows-header">
      <h2 class="system-members-title">
        <RouterLink :to="{ name: 'system-admin-boards' }" class="system-members-title-link">Boards</RouterLink>
        <span class="system-members-title-separator" aria-hidden="true">&gt;</span>
        <span>{{ boardLabel }}</span>
      </h2>
      <button type="button" class="btn" :disabled="busy" @click="openAddMemberDialog">
        Add member
      </button>
    </header>

    <section class="entity-rows-list">
      <article v-for="member in members" :key="member.userId" class="entity-row">
        <button
          type="button"
          class="entity-row-main entity-row-main-button"
          :disabled="busy"
          :aria-label="`Edit member ${member.userName}`"
          @click="focusMemberRoleControl(member.userId)"
        >
          <span class="entity-row-title">{{ member.userName }}</span>
          <span class="badge">#{{ member.userId }}</span>
        </button>
        <div class="entity-row-actions">
          <select
            :id="`system-board-member-role-${member.userId}`"
            :value="member.role"
            :disabled="busy"
            @change="updateRole(member.userId, ($event.target as HTMLSelectElement).value)"
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
  </section>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed, onUnmounted, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { createSystemApi } from '../api/systemApi';
import { createUsersApi } from '../api/usersApi';
import AddBoardMemberDialog from '../components/AddBoardMemberDialog.vue';
import { useSystemBoardMembersStore } from '../stores/systemBoardMembersStore';
import { useUiFeedbackStore } from '../stores/uiFeedbackStore';
import type { UserDirectoryEntry } from '../types/authTypes';
import type { BoardMember, BoardMemberRole } from '../types/boardTypes';

const route = useRoute();
const router = useRouter();
const boardMembersStore = useSystemBoardMembersStore();
const systemApi = createSystemApi();
const usersApi = createUsersApi();
const feedback = useUiFeedbackStore();
const { members, busy } = storeToRefs(boardMembersStore);
const users = ref<UserDirectoryEntry[]>([]);
const usersBusy = ref(false);
const isAddMemberDialogOpen = ref(false);
const boardId = computed(() => resolveBoardId());
const boardName = ref<string | null>(null);
const boardLabel = computed(() => {
  if (boardId.value === null) {
    return 'Members';
  }

  return `${boardName.value ?? 'Board'} (${boardId.value}) - Members`;
});

onUnmounted(() => {
  boardMembersStore.dispose();
});

watch(
  () => route.params.boardId,
  async () => {
    const resolvedBoardId = resolveBoardId();
    if (resolvedBoardId === null) {
      boardName.value = null;
      await router.replace({ name: 'system-admin-boards' });
      return;
    }

    const loaded = await boardMembersStore.loadMembers(resolvedBoardId);
    if (!loaded && resolveBoardId() === resolvedBoardId) {
      await router.replace({ name: 'system-admin-boards' });
      return;
    }

    await loadBoardName(resolvedBoardId);
    await loadUsers();
  },
  { immediate: true }
);

async function openAddMemberDialog() {
  const resolvedBoardId = resolveBoardId();
  if (resolvedBoardId === null) {
    return;
  }

  await loadUsers();
  isAddMemberDialogOpen.value = true;
}

function closeAddMemberDialog() {
  isAddMemberDialogOpen.value = false;
}

async function addMember(payload: { userId: number; role: BoardMemberRole }) {
  const resolvedBoardId = resolveBoardId();
  if (resolvedBoardId === null) {
    return;
  }

  const added = await boardMembersStore.addMember(payload.userId, payload.role, resolvedBoardId);
  if (!added) {
    return;
  }

  isAddMemberDialogOpen.value = false;
}

async function updateRole(userId: number, role: string) {
  const resolvedBoardId = resolveBoardId();
  if (resolvedBoardId === null) {
    return;
  }

  await boardMembersStore.updateMemberRole(userId, role, resolvedBoardId);
}

async function removeMember(member: BoardMember) {
  const shouldRemove = window.confirm(`Remove ${member.userName} from this board?`);
  if (!shouldRemove) {
    return;
  }

  const resolvedBoardId = resolveBoardId();
  if (resolvedBoardId === null) {
    return;
  }

  await boardMembersStore.removeMember(member.userId, resolvedBoardId);
}

function focusMemberRoleControl(userId: number) {
  if (busy.value) {
    return;
  }

  const roleControl = document.getElementById(`system-board-member-role-${userId}`);
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

    users.value = [...result.data].sort((left, right) => left.userName.localeCompare(right.userName));
    return true;
  } finally {
    usersBusy.value = false;
  }
}

async function loadBoardName(resolvedBoardId: number) {
  const result = await systemApi.getBoards();
  if (!result.ok) {
    boardName.value = null;
    return false;
  }

  const matched = result.data.find(x => x.id === resolvedBoardId);
  boardName.value = matched?.name ?? null;
  return matched !== undefined;
}
</script>

<style scoped>
.system-members-title {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
}

.system-members-title-link {
  color: var(--bo-link);
  text-decoration: none;
}

.system-members-title-link:hover,
.system-members-title-link:focus-visible {
  text-decoration: underline;
}

.system-members-title-separator {
  color: var(--bo-ink-muted);
  opacity: 0.45;
}
</style>
