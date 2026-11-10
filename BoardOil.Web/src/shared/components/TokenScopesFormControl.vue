<template>
  <div class="token-scopes">
    <p class="token-scopes-hint">{{ scopeHint }}</p>
    <label v-for="scope in visibleScopes" :key="scope.id" class="token-scope-choice">
      <input
        type="checkbox" :value="scope.id" :checked="modelValue.includes(scope.id)" :disabled="disabled"
        @change="changeScope"
      />
      <span><code>{{ scope.id }}</code>{{ scope.description }}</span>
    </label>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { tokenScopeDefinitions } from '../utils/accessTokenDraft';

const props = defineProps<{ modelValue: string[]; disabled: boolean; allowedScopes: readonly string[] }>();
const emit = defineEmits<{ 'update:modelValue': [value: string[]] }>();
const visibleScopes = computed(() => {
  const allowed = new Set(props.allowedScopes.map(scope => scope.toLowerCase()));
  return tokenScopeDefinitions.filter(scope => allowed.has(scope.id));
});
const scopeHint = computed(() => visibleScopes.value.some(scope => scope.id.startsWith('api:'))
  ? 'MCP scopes control `/mcp`. API scopes control `/api` endpoints.'
  : 'MCP scopes control `/mcp`.');

function changeScope(event: Event) {
  const input = event.target as HTMLInputElement;
  const scopes = props.modelValue.filter(scope => scope !== input.value);
  if (input.checked) scopes.push(input.value);
  emit('update:modelValue', scopes);
}
</script>

<style scoped>
.token-scopes { display: grid; gap: 0.45rem; }
.token-scopes-hint { margin: 0; color: var(--bo-ink-muted); line-height: 1.35; }
.token-scope-choice { display: flex; align-items: flex-start; gap: 0.5rem; color: var(--bo-ink-default); line-height: 1.35; }
.token-scope-choice > input { width: auto; padding: 0; margin-top: 0.15rem; flex: 0 0 auto; }
</style>
