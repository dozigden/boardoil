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
      <component :is="action.icon" :size="14" aria-hidden="true" />
      <span class="md-editor-toolbar-sr">{{ action.label }}</span>
    </button>
  </div>
</template>

<script setup lang="ts">
import { Bold, Heading2, Italic, Link, List, ListOrdered, Minus, Quote, SquareCode, Strikethrough } from 'lucide-vue-next';
import { computed, type Component } from 'vue';
import { mdEditorToolbarActions, type MdEditorToolbarActionId, type MdEditorToolbarActionState } from './mdEditorToolbarActions';

const props = defineProps<{
  state: Partial<Record<MdEditorToolbarActionId, MdEditorToolbarActionState>>;
}>();

const emit = defineEmits<{
  action: [id: MdEditorToolbarActionId];
}>();

const actionIcons: Record<MdEditorToolbarActionId, Component> = {
  bold: Bold,
  italic: Italic,
  strike: Strikethrough,
  heading: Heading2,
  'bullet-list': List,
  'ordered-list': ListOrdered,
  quote: Quote,
  'code-block': SquareCode,
  link: Link,
  rule: Minus
};

const resolvedActions = computed(() => {
  return mdEditorToolbarActions.map(action => ({
    ...action,
    icon: actionIcons[action.id],
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
  position: relative;
  width: 2rem;
  min-width: 2rem;
  height: 2rem;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: 1px solid #b8c8df;
  border-radius: 8px;
  background: #ffffff;
  color: #1d3b63;
  padding: 0.2rem;
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

.md-editor-toolbar-sr {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border: 0;
}
</style>
