<template>
  <FormDialog v-if="open" :definition="form" @close="emit('close')" @saved="saved" />
</template>

<script setup lang="ts">
import FormDialog from '../../shared/forms/FormDialog.vue';
import type { CreatedClientAccount } from '../../shared/types/authTypes';
import { createClientAccountForm } from '../forms/createClientAccountForm';
import { useSystemClientAccountsStore } from '../stores/systemClientAccountsStore';

defineProps<{ open: boolean }>();
const emit = defineEmits<{ close: []; saved: [value: CreatedClientAccount] }>();
const store = useSystemClientAccountsStore();
const form = createClientAccountForm(store.createClientAccount);

function saved(value: CreatedClientAccount) {
  emit('saved', value);
}
</script>
