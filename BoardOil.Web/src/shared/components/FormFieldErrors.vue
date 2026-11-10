<template>
  <ul v-if="messages.length" :id="id" class="form-field-errors" role="alert">
    <li v-for="message in messages" :key="message">
      <CircleAlert :size="16" class="form-field-error-icon" aria-hidden="true" />
      <span>{{ message }}</span>
    </li>
  </ul>
</template>

<script setup lang="ts">
import { CircleAlert } from '@lucide/vue';
import { computed } from 'vue';
import { useValidationStore } from '../stores/validationStore';

const props = defineProps<{ id: string; field: string; fields?: readonly string[]; category?: string }>();
const validation = useValidationStore();
const messages = computed(() => [...new Set([props.field, ...(props.fields ?? [])].flatMap(field => validation.getFieldErrors(field, props.category)))]);
</script>

<style scoped>
.form-field-errors {
  margin: 0;
  padding: 0;
  list-style: none;
  color: var(--bo-colour-danger-ink);
}

.form-field-errors li {
  display: flex;
  align-items: flex-start;
  gap: 0.4rem;
}

.form-field-error-icon {
  flex-shrink: 0;
  margin-top: calc((1lh - 16px) / 2);
}
</style>
