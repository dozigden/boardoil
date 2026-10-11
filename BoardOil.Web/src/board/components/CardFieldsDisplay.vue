<template>
  <dl v-if="fields.length" class="card-custom-fields" aria-label="Custom Fields">
    <div v-for="field in fields" :key="field.key" :title="`${field.displayName}: ${field.value}`">
      <dt>
        <span v-if="field.emoji" class="bo-emoji" aria-hidden="true">{{ field.emoji }}</span>
        <span v-else class="card-field-bullet" aria-hidden="true"></span>
        <span class="card-field-label--hidden">{{ field.displayName }}</span>
      </dt>
      <dd :class="{ 'card-field-value--text': field.type === 'text' }">{{ field.value }}</dd>
    </div>
  </dl>
</template>

<script setup lang="ts">
import type { CardFieldDisplay } from '../utils/cardFieldValues';

defineProps<{ fields: CardFieldDisplay[] }>();
</script>

<style scoped>
.card-custom-fields {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.2rem 0.6rem;
  margin: 0.4rem 0 0;
  font-size: 0.9rem;
  font-weight: 400;
}
.card-custom-fields > div { display: flex; align-items: baseline; gap: var(--card-icon-gap, 0.3rem); min-width: 0; }
.card-custom-fields > div:last-child:nth-child(odd) { grid-column: 1 / -1; }
.card-custom-fields dt {
  flex: 0 0 var(--card-icon-width, 1.5rem);
  opacity: 0.8;
  text-align: center;
}
.card-field-bullet {
  display: inline-block;
  width: 0.5rem;
  height: 0.5rem;
  border-radius: 50%;
  background: currentColor;
  vertical-align: middle;
}
.card-field-label--hidden {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip-path: inset(50%);
  white-space: nowrap;
  border: 0;
}
.card-custom-fields dd { margin: 0; min-width: 0; overflow-wrap: anywhere; }
.card-field-value--text {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
