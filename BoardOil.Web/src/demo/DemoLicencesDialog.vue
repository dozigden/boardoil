<template>
  <FixedChromeDialog
    :open="open"
    title="Third-Party Licences"
    size="fill"
    close-label="Close licences"
    @close="emit('close')"
  >
    <LicencesView embedded />

    <template #actions>
      <div class="fixed-chrome-dialog-actions fixed-chrome-dialog-actions--end">
        <button type="button" class="btn" @click="emit('close')">Close</button>
      </div>
    </template>
  </FixedChromeDialog>
</template>

<script setup lang="ts">
import FixedChromeDialog from '../shared/components/FixedChromeDialog.vue';
import LicencesView from '../site/views/LicencesView.vue';

defineProps<{
  open: boolean;
}>();

const emit = defineEmits<{
  close: [];
}>();
</script>
