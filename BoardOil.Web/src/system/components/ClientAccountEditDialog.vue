<template>
  <FormDialog :definition="form" @close="close" @saved="close" />
</template>

<script setup lang="ts">
import FormDialog from '../../shared/forms/FormDialog.vue';
import type { ClientAccount } from '../../shared/types/authTypes';
import { createAccountEditForm } from '../forms/accountEditForm';
import { useSystemClientAccountsStore } from '../stores/systemClientAccountsStore';

const props = defineProps<{ client: ClientAccount }>();
const emit = defineEmits<{ close: [] }>();
const store = useSystemClientAccountsStore();
const account = props.client;
const form = createAccountEditForm(account, 'client', values => store.updateClientAccount(account.id, values));

function close() {
  emit('close');
}
</script>
