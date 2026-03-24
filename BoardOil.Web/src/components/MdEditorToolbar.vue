<template>
  <div class="md-editor-toolbar" role="toolbar" aria-label="Markdown formatting">
    <button
      v-for="action in resolvedActions"
      :key="action.id"
      type="button"
      class="md-editor-toolbar-button"
      :class="{ 'is-active': action.isActive }"
      :disabled="action.disabled"
      :aria-label="action.ariaLabel"
      :title="action.title"
      @click="emit('action', action.id)"
    >
      {{ action.label }}
    </button>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { mdEditorToolbarActions, type MdEditorToolbarActionId, type MdEditorToolbarActionState } from './mdEditorToolbarActions';

const props = defineProps<{
  state: Partial<Record<MdEditorToolbarActionId, MdEditorToolbarActionState>>;
}>();

const emit = defineEmits<{
  action: [id: MdEditorToolbarActionId];
}>();

const resolvedActions = computed(() => {
  return mdEditorToolbarActions.map(action => ({
    ...action,
    disabled: props.state[action.id]?.disabled ?? true,
    isActive: props.state[action.id]?.isActive ?? false
  }));
});
</script>

<style scoped>
.md-editor-toolbar {
  display: flex;
  flex-wrap: wrap;
  gap: 0.35rem;
}

.md-editor-toolbar-button {
  width: auto;
  border: 1px solid #b8c8df;
  border-radius: 8px;
  background: #ffffff;
  color: #1d3b63;
  padding: 0.3rem 0.5rem;
  font-size: 0.76rem;
  line-height: 1.1;
}

.md-editor-toolbar-button.is-active {
  border-color: #5b7ca8;
  background: #edf3fc;
  color: #234264;
}

.md-editor-toolbar-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
</style>
