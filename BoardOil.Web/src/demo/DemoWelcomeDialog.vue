<template>
  <FixedChromeDialog
    :open="open"
    title="Welcome to the BoardOil demo"
    close-label="Close demo introduction"
    @close="emit('close')"
  >
    <div class="demo-welcome-content">
      <p>This is a demo of the main board interface in BoardOil.</p>

      <p>
        Feel free to move things around and edit to your heart's content. This data doesn't go anywhere;
        it's only running in your browser.
      </p>

      <p>
        Because of this, there's no configuration of columns, card types, or tags here. And no system
        administration either. You'll have to use the power of imagination for those things, or
        <a href="https://boardoil.dozigden.com/installation/" target="_blank" rel="noopener noreferrer">
          install your own BoardOil
        </a>, which I encourage you to do!
      </p>

      <p class="demo-welcome-warning">
        <strong>Anything you enter here will disappear when you leave the page, so don't use it for anything real.</strong>
      </p>

      <p class="demo-welcome-signature">Doz.</p>
    </div>

    <template #actions>
      <div class="fixed-chrome-dialog-actions fixed-chrome-dialog-actions--end">
        <button type="button" class="btn" @click="emit('close')">Explore the demo</button>
      </div>
    </template>
  </FixedChromeDialog>
</template>

<script setup lang="ts">
import FixedChromeDialog from '../shared/components/FixedChromeDialog.vue';

defineProps<{
  open: boolean;
}>();

const emit = defineEmits<{
  close: [];
}>();
</script>

<style scoped>
.demo-welcome-content {
  display: grid;
  gap: 0.85rem;
  line-height: 1.55;
}

.demo-welcome-content p {
  margin: 0;
}

.demo-welcome-warning {
  padding: 0.75rem;
  border: 1px solid var(--bo-toast-warning-border);
  border-radius: 10px;
  background: var(--bo-toast-warning-bg);
  color: var(--bo-toast-warning-text);
}

.demo-welcome-signature {
  font-style: italic;
}

</style>
