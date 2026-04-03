<template>
  <section class="entity-rows-page">
    <header class="entity-rows-header">
      <h2>Members</h2>
      <span v-if="currentRole" class="badge">Your role: {{ currentRole }}</span>
    </header>

    <p v-if="!isCurrentUserOwner" class="entity-rows-empty">Owner permission required to manage members.</p>

    <template v-else>
      <section class="entity-rows-list">
        <article class="entity-row members-add-row">
          <div class="entity-row-main members-add-fields">
            <label>
              User ID
              <input
                v-model="newMemberUserId"
                type="number"
                min="1"
                step="1"
                placeholder="User id"
                :disabled="busy"
              />
            </label>
            <label>
              Role
              <select v-model="newMemberRole" :disabled="busy">
                <option value="Contributor">Contributor</option>
                <option value="Owner">Owner</option>
              </select>
            </label>
          </div>
          <div class="entity-row-actions">
            <button type="button" class="btn" :disabled="busy" @click="addMember">
              Add member
            </button>
          </div>
        </article>

        <article v-for="member in members" :key="member.userId" class="entity-row">
          <div class="entity-row-main">
            <h3 class="entity-row-title">{{ member.userName }}</h3>
            <span class="badge">#{{ member.userId }}</span>
          </div>
          <div class="entity-row-actions">
            <select
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
              @click="removeMember(member.userId)"
            >
              Remove
            </button>
          </div>
        </article>
      </section>
    </template>
  </section>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed, onUnmounted, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useBoardMembersStore } from '../stores/boardMembersStore';
import { useBoardStore } from '../stores/boardStore';
import { useUiFeedbackStore } from '../stores/uiFeedbackStore';
import type { BoardMemberRole } from '../types/boardTypes';

const route = useRoute();
const router = useRouter();
const boardStore = useBoardStore();
const boardMembersStore = useBoardMembersStore();
const feedback = useUiFeedbackStore();
const { currentUserRole, isCurrentUserOwner } = storeToRefs(boardStore);
const { members, busy } = storeToRefs(boardMembersStore);
const newMemberUserId = ref('');
const newMemberRole = ref<BoardMemberRole>('Contributor');

const currentRole = computed(() => currentUserRole.value);

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
  },
  { immediate: true }
);

async function addMember() {
  const boardId = resolveBoardId();
  if (boardId === null) {
    return;
  }

  const parsedUserId = Number.parseInt(newMemberUserId.value, 10);
  if (!Number.isFinite(parsedUserId) || parsedUserId <= 0) {
    feedback.setError('User id must be a positive number.');
    return;
  }

  const added = await boardMembersStore.addMember(parsedUserId, newMemberRole.value, boardId);
  if (!added) {
    return;
  }

  newMemberUserId.value = '';
  newMemberRole.value = 'Contributor';
}

async function updateRole(userId: number, role: string) {
  const boardId = resolveBoardId();
  if (boardId === null) {
    return;
  }

  await boardMembersStore.updateMemberRole(userId, role, boardId);
}

async function removeMember(userId: number) {
  const boardId = resolveBoardId();
  if (boardId === null) {
    return;
  }

  await boardMembersStore.removeMember(userId, boardId);
}

function resolveBoardId() {
  const parsed = Number.parseInt(String(route.params.boardId ?? ''), 10);
  return Number.isFinite(parsed) ? parsed : null;
}
</script>

<style scoped>
.members-add-row {
  border-style: dashed;
}

.members-add-fields {
  display: flex;
  gap: 0.75rem;
  align-items: end;
  flex-wrap: wrap;
}

.members-add-fields label {
  display: grid;
  gap: 0.35rem;
  font-size: 0.85rem;
}
</style>
