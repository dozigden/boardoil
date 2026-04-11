<template>
  <ModalDialog :open="open" title="Copy token now" close-label="Close token reminder" @close="emit('close')" @submit="emit('copy')">
    <div class="machine-pat-secret">
      <p class="machine-pat-secret-note">
        This value is only shown once for <strong>{{ tokenName || 'new token' }}</strong>.
      </p>
      <div class="machine-pat-secret-frame">
        <div class="machine-pat-secret-code">
          <code class="machine-pat-secret-value">{{ token }}</code>
          <button
            type="button"
            class="btn btn--secondary machine-pat-secret-copy"
            :disabled="busy"
            aria-label="Copy token"
            title="Copy token"
            @click="emit('copy')"
          >
            <Copy :size="14" aria-hidden="true" />
          </button>
        </div>
        <p class="machine-pat-secret-warning">Store it somewhere secure. You will need to create a new token if you lose it.</p>
      </div>
    </div>

    <template #actions>
      <div class="card-modal-actions">
        <div class="card-modal-actions-left">
          <button type="submit" class="btn" :disabled="busy">Copy token</button>
          <button type="button" class="btn btn--secondary" :disabled="busy" @click="emit('close')">Done</button>
        </div>
      </div>
    </template>
  </ModalDialog>
</template>

<script setup lang="ts">
import { Copy } from 'lucide-vue-next';
import ModalDialog from './ModalDialog.vue';

defineProps<{
  open: boolean;
  busy: boolean;
  token: string | null;
  tokenName: string;
}>();

const emit = defineEmits<{
  close: [];
  copy: [];
}>();
</script>

<style scoped>
.machine-pat-secret {
  display: grid;
  gap: 0.6rem;
}

.machine-pat-secret-note {
  margin: 0;
  color: var(--bo-ink-muted);
}

.machine-pat-secret-frame {
  border: 1px solid color-mix(in oklab, var(--bo-colour-danger) 50%, var(--bo-border-soft));
  border-radius: 12px;
  background: color-mix(in oklab, var(--bo-colour-danger) 10%, var(--bo-surface-base));
  padding: 0.65rem;
  display: grid;
  gap: 0.5rem;
}

.machine-pat-secret-code {
  position: relative;
  min-width: 0;
}

.machine-pat-secret-value {
  display: block;
  padding: 0.65rem 2.4rem 0.65rem 0.65rem;
  border: 1px solid color-mix(in oklab, var(--bo-colour-danger) 45%, var(--bo-border-soft));
  border-radius: 10px;
  background: color-mix(in oklab, var(--bo-colour-danger) 6%, var(--bo-surface-base));
  color: var(--bo-colour-danger-ink);
  font-family: "Cascadia Mono", "Consolas", "Liberation Mono", monospace;
  font-size: 0.83rem;
  overflow-wrap: anywhere;
}

.machine-pat-secret-copy {
  position: absolute;
  top: 0.42rem;
  right: 0.42rem;
  width: 1.75rem;
  min-width: 1.75rem;
  height: 1.75rem;
  padding: 0;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-color: color-mix(in oklab, var(--bo-colour-danger) 50%, var(--bo-border-soft));
  background: color-mix(in oklab, var(--bo-colour-danger) 15%, var(--bo-surface-base));
  color: var(--bo-colour-danger-ink);
  transition: background 120ms ease-in-out, border-color 120ms ease-in-out, color 120ms ease-in-out;
}

.machine-pat-secret-copy:hover:not(:disabled),
.machine-pat-secret-copy:focus-visible {
  border-color: var(--bo-colour-danger);
  background: var(--bo-colour-danger);
  color: var(--bo-surface-base);
}

.machine-pat-secret-warning {
  margin: 0;
  color: var(--bo-colour-danger-ink);
}

@media (max-width: 720px) {
  .machine-pat-secret-copy {
    position: static;
    width: auto;
    min-width: auto;
    height: auto;
    padding: 0.35rem 0.6rem;
    border-radius: 8px;
  }

  .machine-pat-secret-code {
    display: grid;
    gap: 0.35rem;
  }

  .machine-pat-secret-value {
    padding-right: 0.65rem;
  }
}
</style>
