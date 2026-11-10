<template>
  <FormDialog :definition="form" @close="close" @saved="close" />
</template>

<script setup lang="ts">
import FormDialog from '../../shared/forms/FormDialog.vue';
import type { ManagedUser } from '../../shared/types/authTypes';
import { createAccountEditForm } from '../forms/accountEditForm';
import { useSystemUsersManagerStore } from '../stores/systemUsersManagerStore';

const props = defineProps<{ user: ManagedUser }>();
const emit = defineEmits<{ close: [] }>();
const store = useSystemUsersManagerStore();
const account = props.user;
const form = createAccountEditForm(account, 'user', values => store.updateUser(account.id, values));

function close() {
  emit('close');
}
</script>
