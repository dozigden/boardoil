<template>
  <ModalDialog
    :open="open"
    title="Create Project Connection"
    close-label="Cancel project connection creation"
    @close="emit('close')"
    @submit="submit"
  >
    <div class="project-connection-fields">
      <label>
        Client account
        <select v-model.number="draft.clientAccountId" :disabled="busy" required>
          <option :value="0" disabled>Select a client account</option>
          <option v-for="client in clients" :key="client.id" :value="client.id">
            {{ client.displayName }} (@{{ client.userName }}){{ client.isActive ? '' : ' — inactive' }}
          </option>
        </select>
      </label>

      <label>
        Connection name
        <input v-model="draft.name" :disabled="busy" maxlength="120" required />
      </label>

      <fieldset class="project-connection-scopes">
        <legend>Allowed MCP scopes</legend>
        <label>
          <input v-model="draft.allowedScopes" :disabled="busy" type="checkbox" value="mcp:read" />
          <span><code>mcp:read</code></span>
        </label>
        <label>
          <input v-model="draft.allowedScopes" :disabled="busy" type="checkbox" value="mcp:write" />
          <span><code>mcp:write</code></span>
        </label>
      </fieldset>
    </div>

    <p v-if="draftError" class="error">{{ draftError }}</p>

    <template #actions>
      <div class="editor-actions card-modal-actions">
        <div class="card-modal-actions-left">
          <button type="submit" class="btn" :disabled="busy">
            <Check :size="16" aria-hidden="true" />
            <span>Create connection</span>
          </button>
          <button type="button" class="btn btn--secondary" :disabled="busy" @click="emit('close')">
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
import ModalDialog from '../../shared/components/ModalDialog.vue';
import type { ClientAccount } from '../../shared/types/authTypes';
import type { CreateMcpProjectConnectionRequest } from '../../shared/types/mcpProjectConnectionTypes';

const props = defineProps<{
  open: boolean;
  busy: boolean;
  clients: ClientAccount[];
}>();

const emit = defineEmits<{
  close: [];
  submit: [request: CreateMcpProjectConnectionRequest];
}>();

const draft = ref<CreateMcpProjectConnectionRequest>(createDefaultDraft());
const draftError = ref<string | null>(null);

function createDefaultDraft(): CreateMcpProjectConnectionRequest {
  return {
    clientAccountId: 0,
    name: '',
    allowedScopes: ['mcp:read', 'mcp:write']
  };
}

function submit() {
  draftError.value = null;
  const request: CreateMcpProjectConnectionRequest = {
    clientAccountId: draft.value.clientAccountId,
    name: draft.value.name.trim(),
    allowedScopes: [...new Set(draft.value.allowedScopes)]
  };

  if (request.clientAccountId <= 0) {
    draftError.value = 'Select a client account.';
    return;
  }

  if (!request.name) {
    draftError.value = 'Connection name is required.';
    return;
  }

  if (request.allowedScopes.length === 0) {
    draftError.value = 'Select at least one MCP scope.';
    return;
  }

  emit('submit', request);
}

watch(
  () => props.open,
  isOpen => {
    if (isOpen) {
      draft.value = createDefaultDraft();
      draftError.value = null;
    }
  }
);
</script>

<style scoped>
.project-connection-fields {
  display: grid;
  gap: 0.75rem;
}

.project-connection-fields > label {
  display: grid;
  gap: 0.3rem;
}

.project-connection-scopes {
  display: grid;
  gap: 0.45rem;
  margin: 0;
  min-inline-size: 0;
  padding: 0.65rem;
  border: 1px solid var(--bo-border-soft);
  border-radius: 10px;
}

.project-connection-scopes > label {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.project-connection-scopes input {
  width: auto;
}
</style>
