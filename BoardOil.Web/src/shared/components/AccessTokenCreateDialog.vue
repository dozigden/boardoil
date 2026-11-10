<template>
  <FormDialog v-if="open" :definition="form" @close="emit('close')" @saved="saved">
    <template #before-fields>
      <p class="access-token-hint">Create an access token for MCP and REST API clients without sharing your account password.</p>
    </template>
  </FormDialog>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import FormDialog from '../forms/FormDialog.vue';
import { accessTokenForm, type AccessTokenFormOptions } from '../forms/accessTokenForm';
import type { CreatedAccessToken } from '../types/authTypes';

const props = defineProps<AccessTokenFormOptions & { open: boolean }>();
const emit = defineEmits<{ close: []; saved: [value: CreatedAccessToken] }>();
const form = computed(() => accessTokenForm(props));

function saved(value: CreatedAccessToken) {
  emit('saved', value);
}
</script>

<style scoped>
.access-token-hint {
  margin: 0;
  color: var(--bo-ink-muted);
  line-height: 1.4;
}
</style>
