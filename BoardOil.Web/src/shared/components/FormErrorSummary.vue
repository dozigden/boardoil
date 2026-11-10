<template>
  <div v-if="messages.length" ref="summary" class="form-error-summary" role="alert" tabindex="-1">
    <ul>
      <li v-for="message in messages" :key="message">{{ message }}</li>
    </ul>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';
import { useValidationStore } from '../stores/validationStore';

const props = defineProps<{ renderedFields?: readonly string[]; category?: string }>();
const validation = useValidationStore();
const summary = ref<HTMLElement | null>(null);
const messages = computed(() => validation.getValidationMessages(props.renderedFields, props.category));

defineExpose({ focus: () => summary.value?.focus() });
</script>

<style scoped>
.form-error-summary {
  padding: 0.65rem;
  border: 1px solid var(--bo-colour-danger-ink);
  border-radius: 6px;
  color: var(--bo-colour-danger-ink);
  overflow-wrap: anywhere;
}

.form-error-summary ul {
  margin: 0;
  padding-left: 1.25rem;
}
</style>
