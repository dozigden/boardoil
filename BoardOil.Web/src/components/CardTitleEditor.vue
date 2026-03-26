<template>
  <div class="card-title-editor">
    <h2 v-if="!isEditing" class="card-title-heading">
      <button type="button" class="card-title-button" @click="beginEdit">
        #{{ cardId }} {{ titleModel }}
      </button>
    </h2>

    <span v-else class="card-title-edit">
      <span class="card-title-id">#{{ cardId }}</span>
      <input
        ref="titleInputRef"
        :value="titleModel"
        :maxlength="maxLength"
        aria-label="Card title"
        @focus="emit('focus')"
        @blur="finishEdit"
        @input="titleModel = ($event.target as HTMLInputElement).value"
        @keydown.enter.prevent="finishEdit"
        @keydown.esc.stop.prevent="cancelEdit"
      />
    </span>
  </div>
</template>

<script setup lang="ts">
import { nextTick, ref, watch } from 'vue';

const props = withDefaults(defineProps<{
  cardId: number;
  maxLength?: number;
}>(), {
  maxLength: 200
});

const titleModel = defineModel<string>('title', { required: true });

const emit = defineEmits<{
  focus: [];
  blur: [];
}>();

const isEditing = ref(false);
const titleBeforeEdit = ref<string | null>(null);
const titleInputRef = ref<HTMLInputElement | null>(null);

async function beginEdit() {
  titleBeforeEdit.value = titleModel.value;
  isEditing.value = true;
  await nextTick();
  titleInputRef.value?.focus();
  titleInputRef.value?.select();
}

function finishEdit() {
  isEditing.value = false;
  titleBeforeEdit.value = null;
  emit('blur');
}

function cancelEdit() {
  if (titleBeforeEdit.value !== null) {
    titleModel.value = titleBeforeEdit.value;
  }

  finishEdit();
}

watch(
  () => props.cardId,
  () => {
    isEditing.value = false;
    titleBeforeEdit.value = null;
  }
);
</script>

<style scoped>
.card-title-heading {
  margin: 0;
  font-size: 1.1rem;
  font-weight: 600;
  line-height: 1.2;
}

.card-title-button {
  width: auto;
  min-width: 0;
  margin: 0;
  border: none;
  border-radius: 4px;
  padding: 0;
  background: transparent;
  color: inherit;
  font: inherit;
  text-align: left;
}

.card-title-button:hover {
  background: var(--bo-surface-energy);
  color: var(--bo-colour-energy);
}

.card-title-button:focus-visible {
  background: var(--bo-surface-energy);
  color: var(--bo-colour-energy);
  outline: 2px solid var(--bo-colour-energy);
  outline-offset: 2px;
  border-radius: 4px;
}

.card-title-edit {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
}

.card-title-id {
  font-weight: 600;
}

.card-title-edit input {
  width: min(42rem, calc(100vw - 12rem));
  min-width: 14rem;
  margin: 0;
}
</style>
