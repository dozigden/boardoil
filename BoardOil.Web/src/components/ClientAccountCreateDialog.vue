<template>
  <ModalDialog
    :open="open"
    title="Create Client Account"
    close-label="Cancel client account creation"
    @close="emit('close')"
    @submit="submit"
  >
    <fieldset class="client-account-dialog-group">
      <legend>Account</legend>
      <label>
        Username
        <input v-model="userName" :disabled="busy" maxlength="64" required />
      </label>
      <label>
        Role
        <select v-model="role" :disabled="busy">
          <option value="Standard">Standard</option>
          <option value="Admin">Admin</option>
        </select>
      </label>
    </fieldset>

    <fieldset class="client-account-dialog-group">
      <legend>Initial token</legend>
      <label>
        Token name
        <input v-model="tokenName" :disabled="busy" maxlength="120" required />
      </label>
      <label class="client-account-dialog-check">
        <input v-model="includeMcpRead" :disabled="busy" type="checkbox" />
        <span><code>mcp:read</code> (board and column reads)</span>
      </label>
      <label class="client-account-dialog-check">
        <input v-model="includeMcpWrite" :disabled="busy" type="checkbox" />
        <span><code>mcp:write</code> (card create/update/move/delete)</span>
      </label>
      <label class="client-account-dialog-check">
        <input v-model="includeApiRead" :disabled="busy" type="checkbox" />
        <span><code>api:read</code> (REST `GET` and `HEAD` on `/api/*`)</span>
      </label>
      <label class="client-account-dialog-check">
        <input v-model="includeApiWrite" :disabled="busy" type="checkbox" />
        <span><code>api:write</code> (REST `POST`/`PUT`/`PATCH`/`DELETE` on `/api/*`)</span>
      </label>
      <label class="client-account-dialog-check">
        <input v-model="includeApiAdmin" :disabled="busy" type="checkbox" />
        <span><code>api:admin</code> (`/api/admin/*`)</span>
      </label>
      <label class="client-account-dialog-check">
        <input v-model="includeApiSystem" :disabled="busy" type="checkbox" />
        <span><code>api:system</code> (`/api/system/*`)</span>
      </label>
    </fieldset>

    <fieldset class="client-account-dialog-group">
      <legend>Expiry</legend>
      <label class="client-account-dialog-check">
        <input v-model="isNonExpiring" :disabled="busy" type="checkbox" />
        <span>Non-expiring token (not recommended)</span>
      </label>

      <label v-if="!isNonExpiring">
        Expires in days
        <input v-model.number="expiresInDays" :disabled="busy" type="number" min="1" max="3650" />
      </label>

      <div v-else class="client-account-warning">
        <p>This token will remain valid until revoked manually.</p>
        <label class="client-account-dialog-check">
          <input v-model="nonExpiringConfirmed" :disabled="busy" type="checkbox" />
          <span>I understand the risk and still want a non-expiring token.</span>
        </label>
      </div>
    </fieldset>

    <p v-if="draftError" class="error">{{ draftError }}</p>

    <template #actions>
      <div class="editor-actions card-modal-actions client-account-dialog-actions">
        <div class="card-modal-actions-left">
          <button type="submit" class="btn" :disabled="busy" aria-label="Create client account" title="Create client account">
            <Check :size="16" aria-hidden="true" />
            <span>Create client account</span>
          </button>
          <button type="button" class="btn btn--secondary" :disabled="busy" aria-label="Cancel creation" title="Cancel" @click="emit('close')">
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
import type { CreateClientAccountRequest } from '../types/authTypes';
import ModalDialog from './ModalDialog.vue';

const props = defineProps<{
  open: boolean;
  busy: boolean;
}>();

const emit = defineEmits<{
  close: [];
  submit: [payload: CreateClientAccountRequest];
}>();

const userName = ref('');
const role = ref<'Admin' | 'Standard'>('Standard');
const tokenName = ref('Initial token');
const includeMcpRead = ref(false);
const includeMcpWrite = ref(false);
const includeApiRead = ref(true);
const includeApiWrite = ref(true);
const includeApiAdmin = ref(true);
const includeApiSystem = ref(true);
const isNonExpiring = ref(false);
const nonExpiringConfirmed = ref(false);
const expiresInDays = ref(30);
const draftError = ref<string | null>(null);

function resetDraft() {
  userName.value = '';
  role.value = 'Standard';
  tokenName.value = 'Initial token';
  includeMcpRead.value = false;
  includeMcpWrite.value = false;
  includeApiRead.value = true;
  includeApiWrite.value = true;
  includeApiAdmin.value = true;
  includeApiSystem.value = true;
  isNonExpiring.value = false;
  nonExpiringConfirmed.value = false;
  expiresInDays.value = 30;
  draftError.value = null;
}

function submit() {
  draftError.value = null;

  const trimmedUserName = userName.value.trim();
  if (!trimmedUserName) {
    draftError.value = 'Username is required.';
    return;
  }

  if (trimmedUserName.length < 1 || trimmedUserName.length > 64) {
    draftError.value = 'Username must be between 1 and 64 characters.';
    return;
  }

  const trimmedTokenName = tokenName.value.trim();
  if (!trimmedTokenName) {
    draftError.value = 'Token name is required.';
    return;
  }

  const scopes: string[] = [];
  if (includeMcpRead.value) {
    scopes.push('mcp:read');
  }
  if (includeMcpWrite.value) {
    scopes.push('mcp:write');
  }
  if (includeApiRead.value) {
    scopes.push('api:read');
  }
  if (includeApiWrite.value) {
    scopes.push('api:write');
  }
  if (includeApiAdmin.value) {
    scopes.push('api:admin');
  }
  if (includeApiSystem.value) {
    scopes.push('api:system');
  }

  if (scopes.length === 0) {
    draftError.value = 'Select at least one scope.';
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

  const payload: CreateClientAccountRequest = {
    userName: trimmedUserName,
    role: role.value,
    tokenName: trimmedTokenName,
    expiresInDays: isNonExpiring.value ? null : Math.trunc(Number(expiresInDays.value)),
    scopes
  };

  emit('submit', payload);
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
.client-account-dialog-group {
  display: grid;
  gap: 0.45rem;
  margin: 0;
  min-inline-size: 0;
  padding: 0.55rem 0.65rem;
  border: 1px solid var(--bo-border-soft);
  border-radius: 10px;
  background: var(--bo-surface-base);
}

.client-account-dialog-group > legend {
  font-weight: 700;
  color: var(--bo-link);
  padding: 0 0.25rem;
}

.client-account-dialog-group > label:not(.client-account-dialog-check) {
  display: grid;
  gap: 0.3rem;
}

.client-account-dialog-group input[type="number"],
.client-account-dialog-group input[type="text"] {
  width: 100%;
}

.client-account-dialog-check {
  display: flex;
  align-items: flex-start;
  gap: 0.5rem;
  color: var(--bo-ink-default);
  line-height: 1.35;
}

.client-account-dialog-check > input {
  width: auto;
  padding: 0;
  margin-top: 0.15rem;
  flex: 0 0 auto;
}

.client-account-dialog-check code {
  font-family: "Cascadia Mono", "Consolas", "Liberation Mono", monospace;
}

.client-account-warning {
  display: grid;
  gap: 0.4rem;
  border-radius: 10px;
  padding: 0.5rem 0.6rem;
  border: 1px solid var(--bo-colour-warning);
  background: color-mix(in srgb, var(--bo-colour-warning) 18%, white);
}

.client-account-warning > p {
  margin: 0;
  color: var(--bo-ink-strong);
}

.client-account-dialog-actions {
  margin-top: 0.35rem;
}
</style>
