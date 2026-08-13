<template>
  <ModalDialog
    :open="open"
    title="Third-Party Licences"
    size="fill"
    close-label="Close licences"
    @close="emit('close')"
  >
    <LicencesView embedded />
  </ModalDialog>
</template>

<script setup lang="ts">
import ModalDialog from '../shared/components/ModalDialog.vue';
import LicencesView from '../site/views/LicencesView.vue';

defineProps<{
  open: boolean;
}>();

const emit = defineEmits<{
  close: [];
}>();
</script>
