<template>
  <ModalDialog :open="open" title="Create Machine Access Token" close-label="Cancel token creation" @close="emit('close')" @submit="submit">
    <p class="machine-pat-dialog-hint">Create a token for MCP clients without sharing your account password.</p>

    <label>
      Token name
      <input v-model="name" :disabled="busy" maxlength="120" required />
    </label>

    <fieldset class="machine-pat-dialog-group">
      <legend>Scopes</legend>
      <label class="machine-pat-dialog-check">
        <input v-model="includeRead" :disabled="busy" type="checkbox" />
        <span><code>mcp:read</code> (board and column reads)</span>
      </label>
      <label class="machine-pat-dialog-check">
        <input v-model="includeWrite" :disabled="busy" type="checkbox" />
        <span><code>mcp:write</code> (card create/update/move/delete)</span>
      </label>
    </fieldset>

    <fieldset class="machine-pat-dialog-group">
      <legend>Board access</legend>
      <label class="machine-pat-dialog-check">
        <input v-model="boardAccessMode" :disabled="busy" type="radio" value="all" />
        <span>All boards</span>
      </label>
      <label class="machine-pat-dialog-check">
        <input v-model="boardAccessMode" :disabled="busy" type="radio" value="selected" />
        <span>Selected boards only</span>
      </label>
      <div v-if="boardAccessMode === 'selected'" class="machine-pat-board-list">
        <p v-if="boards.length === 0" class="machine-pat-board-empty">No boards available yet.</p>
        <label v-for="board in boards" :key="board.id" class="machine-pat-dialog-check">
          <input
            :checked="selectedBoardIds.includes(board.id)"
            :disabled="busy"
            type="checkbox"
            @change="toggleBoard(board.id, ($event.target as HTMLInputElement).checked)"
          />
          <span>{{ board.name }} <span class="card-id">#{{ board.id }}</span></span>
        </label>
      </div>
    </fieldset>

    <fieldset class="machine-pat-dialog-group">
      <legend>Expiry</legend>
      <label class="machine-pat-dialog-check">
        <input v-model="isNonExpiring" :disabled="busy" type="checkbox" />
        <span>Non-expiring token (not recommended)</span>
      </label>

      <label v-if="!isNonExpiring">
        Expires in days
        <input v-model.number="expiresInDays" :disabled="busy" type="number" min="1" max="3650" />
      </label>

      <div v-else class="machine-pat-warning">
        <p>This token will remain valid until revoked manually.</p>
        <label class="machine-pat-dialog-check">
          <input v-model="nonExpiringConfirmed" :disabled="busy" type="checkbox" />
          <span>I understand the risk and still want a non-expiring token.</span>
        </label>
      </div>
    </fieldset>

    <p v-if="draftError" class="error">{{ draftError }}</p>

    <template #actions>
      <div class="editor-actions card-modal-actions">
        <div class="card-modal-actions-left">
          <button type="submit" class="card-modal-save" :disabled="busy" aria-label="Create token" title="Create token">
            <Check :size="16" aria-hidden="true" />
            <span>Create token</span>
          </button>
          <button type="button" class="ghost card-modal-cancel" :disabled="busy" aria-label="Cancel creation" title="Cancel" @click="emit('close')">
            <X :size="16" aria-hidden="true" />
            <span>Cancel</span>
          </button>
        </div>
      </div>
    </template>
  </ModalDialog>
</template>

<script setup lang="ts">
import { Check, X } from 'lucide-vue-next';
import { ref, watch } from 'vue';
import type { BoardSummary } from '../types/boardTypes';
import type { CreateMachinePatRequest } from '../types/authTypes';
import ModalDialog from './ModalDialog.vue';

const props = defineProps<{
  open: boolean;
  busy: boolean;
  boards: BoardSummary[];
}>();

const emit = defineEmits<{
  close: [];
  submit: [payload: CreateMachinePatRequest];
}>();

const name = ref('');
const includeRead = ref(true);
const includeWrite = ref(true);
const boardAccessMode = ref<'all' | 'selected'>('all');
const selectedBoardIds = ref<number[]>([]);
const isNonExpiring = ref(false);
const nonExpiringConfirmed = ref(false);
const expiresInDays = ref(30);
const draftError = ref<string | null>(null);

function resetDraft() {
  name.value = '';
  includeRead.value = true;
  includeWrite.value = true;
  boardAccessMode.value = 'all';
  selectedBoardIds.value = [];
  isNonExpiring.value = false;
  nonExpiringConfirmed.value = false;
  expiresInDays.value = 30;
  draftError.value = null;
}

function submit() {
  draftError.value = null;

  const trimmedName = name.value.trim();
  if (!trimmedName) {
    draftError.value = 'Token name is required.';
    return;
  }

  const scopes: string[] = [];
  if (includeRead.value) {
    scopes.push('mcp:read');
  }
  if (includeWrite.value) {
    scopes.push('mcp:write');
  }

  if (scopes.length === 0) {
    draftError.value = 'Select at least one scope.';
    return;
  }

  if (boardAccessMode.value === 'selected' && selectedBoardIds.value.length === 0) {
    draftError.value = 'Select at least one board for selected-board mode.';
    return;
  }

  if (isNonExpiring.value && !nonExpiringConfirmed.value) {
    draftError.value = 'Confirm non-expiring token risk before creating.';
    return;
  }

  if (!isNonExpiring.value) {
    const parsedDays = Math.trunc(Number(expiresInDays.value));
    if (!Number.isFinite(parsedDays) || parsedDays < 1) {
      draftError.value = 'Expiry must be at least 1 day.';
      return;
    }
  }

  const payload: CreateMachinePatRequest = {
    name: trimmedName,
    expiresInDays: isNonExpiring.value ? null : Math.trunc(Number(expiresInDays.value)),
    scopes,
    boardAccessMode: boardAccessMode.value,
    allowedBoardIds: boardAccessMode.value === 'selected' ? [...selectedBoardIds.value] : []
  };
  emit('submit', payload);
}

function toggleBoard(boardId: number, checked: boolean) {
  if (checked) {
    if (!selectedBoardIds.value.includes(boardId)) {
      selectedBoardIds.value = [...selectedBoardIds.value, boardId].sort((left, right) => left - right);
    }
    return;
  }

  selectedBoardIds.value = selectedBoardIds.value.filter(id => id !== boardId);
}

watch(
  () => props.open,
  isOpen => {
    if (isOpen) {
      resetDraft();
    }
  }
);
</script>

<style scoped>
.machine-pat-dialog-hint {
  margin: 0;
  color: var(--bo-ink-muted);
}

.machine-pat-dialog-group {
  display: grid;
  gap: 0.45rem;
  margin: 0;
  padding: 0.55rem 0.65rem;
  border: 1px solid var(--bo-border-soft);
  border-radius: 10px;
}

.machine-pat-dialog-group > legend {
  font-weight: 700;
  color: var(--bo-link);
  padding: 0 0.25rem;
}

.machine-pat-dialog-check {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  color: var(--bo-ink-default);
}

.machine-pat-board-list {
  display: grid;
  gap: 0.35rem;
  padding-top: 0.1rem;
}

.machine-pat-board-empty {
  margin: 0;
  color: var(--bo-ink-muted);
}

.machine-pat-warning {
  display: grid;
  gap: 0.4rem;
  border: 1px solid var(--bo-colour-warning);
  border-radius: 10px;
  padding: 0.5rem 0.6rem;
  background: color-mix(in srgb, var(--bo-colour-warning) 18%, white);
}

.machine-pat-warning > p {
  margin: 0;
  color: var(--bo-ink-strong);
}
</style>
