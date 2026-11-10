<template>
  <div class="token-expiry">
    <label class="token-expiry-choice">
      <input type="checkbox" :checked="modelValue.nonExpiring" :disabled="disabled" @change="changeNonExpiring" />
      <span>Non-expiring token (not recommended)</span>
    </label>
    <label v-if="!modelValue.nonExpiring" class="token-expiry-days">
      Expires in days
      <input type="number" :value="modelValue.days" :disabled="disabled" @input="changeDays" />
    </label>
    <div v-else class="token-expiry-warning">
      <p>This token will remain valid until revoked manually.</p>
      <label class="token-expiry-choice">
        <input type="checkbox" :checked="modelValue.confirmed" :disabled="disabled" @change="changeConfirmed" />
        <span>I understand the risk and still want a non-expiring token.</span>
      </label>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { TokenExpiryDraft } from '../utils/accessTokenDraft';

const props = defineProps<{ modelValue: TokenExpiryDraft; disabled: boolean }>();
const emit = defineEmits<{ 'update:modelValue': [value: TokenExpiryDraft] }>();

function changeNonExpiring(event: Event) {
  emit('update:modelValue', { ...props.modelValue, nonExpiring: (event.target as HTMLInputElement).checked });
}
function changeConfirmed(event: Event) {
  emit('update:modelValue', { ...props.modelValue, confirmed: (event.target as HTMLInputElement).checked });
}
function changeDays(event: Event) {
  const days = (event.target as HTMLInputElement).valueAsNumber;
  emit('update:modelValue', { ...props.modelValue, days: Number.isNaN(days) ? null : days });
}
</script>

<style scoped>
.token-expiry { display: grid; gap: 0.45rem; }
.token-expiry-days { display: grid; gap: 0.3rem; }
.token-expiry-days input { width: 100%; }
.token-expiry-choice { display: flex; align-items: flex-start; gap: 0.5rem; color: var(--bo-ink-default); line-height: 1.35; }
.token-expiry-choice > input { width: auto; padding: 0; margin-top: 0.15rem; flex: 0 0 auto; }
.token-expiry-warning { display: grid; gap: 0.4rem; border: 1px solid var(--bo-colour-warning); border-radius: 10px; padding: 0.5rem 0.6rem; background: color-mix(in srgb, var(--bo-colour-warning) 18%, white); }
.token-expiry-warning > p { margin: 0; color: var(--bo-ink-strong); }
</style>
