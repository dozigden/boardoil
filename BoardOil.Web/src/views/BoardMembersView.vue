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
            :aria-label="`Edit member ${member.userName}`"
            @click="focusMemberRoleControl(member.userId)"
          >
            <span class="entity-row-title">{{ member.userName }}</span>
            <span class="badge">#{{ member.userId }}</span>
          </button>
          <div class="entity-row-actions">
            <select
              :id="`board-member-role-${member.userId}`"
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
    </template>
  </section>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { onUnmounted, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { createUsersApi } from '../api/usersApi';
import AddBoardMemberDialog from '../components/AddBoardMemberDialog.vue';
import { useBoardMembersStore } from '../stores/boardMembersStore';
import { useBoardStore } from '../stores/boardStore';
import { useUiFeedbackStore } from '../stores/uiFeedbackStore';
import type { UserDirectoryEntry } from '../types/authTypes';
import type { BoardMember, BoardMemberRole } from '../types/boardTypes';

const route = useRoute();
const router = useRouter();
const boardStore = useBoardStore();
const boardMembersStore = useBoardMembersStore();
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
  const boardId = resolveBoardId();
  if (boardId === null) {
    return;
  }

  await loadUsers();
  isAddMemberDialogOpen.value = true;
}

function closeAddMemberDialog() {
  isAddMemberDialogOpen.value = false;
}

async function addMember(payload: { userId: number; role: BoardMemberRole }) {
  const boardId = resolveBoardId();
  if (boardId === null) {
    return;
  }

  const added = await boardMembersStore.addMember(payload.userId, payload.role, boardId);
  if (!added) {
    return;
  }

  isAddMemberDialogOpen.value = false;
}

async function updateRole(userId: number, role: string) {
  const boardId = resolveBoardId();
  if (boardId === null) {
    return;
  }

  await boardMembersStore.updateMemberRole(userId, role, boardId);
}

async function removeMember(member: BoardMember) {
  const shouldRemove = window.confirm(`Remove ${member.userName} from this board?`);
  if (!shouldRemove) {
    return;
  }

  const boardId = resolveBoardId();
  if (boardId === null) {
    return;
  }

  await boardMembersStore.removeMember(member.userId, boardId);
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

    users.value = [...result.data].sort((left, right) => left.userName.localeCompare(right.userName));
    return true;
  } finally {
    usersBusy.value = false;
  }
}
</script>
