<template>
  <FormDialog v-if="open" :definition="form" @close="close" @saved="close" />
</template>

<script setup lang="ts">
import FormDialog from '../../shared/forms/FormDialog.vue';
import { createUserForm } from '../forms/createUserForm';
import { useSystemUsersManagerStore } from '../stores/systemUsersManagerStore';

defineProps<{ open: boolean }>();
const emit = defineEmits<{ close: [] }>();
const store = useSystemUsersManagerStore();
const form = createUserForm(store.createUser);

function close() {
  emit('close');
}
</script>
