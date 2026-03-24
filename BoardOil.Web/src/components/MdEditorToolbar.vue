<template>
  <div class="md-editor-toolbar" role="toolbar" aria-label="Markdown formatting">
    <template v-for="action in resolvedActions" :key="action.id">
      <div v-if="action.id === 'heading'" ref="headingSplitRef" class="md-editor-toolbar-split">
        <button
          type="button"
          class="md-editor-toolbar-button"
          :class="{ 'is-active': action.isActive }"
          :disabled="action.disabled"
          aria-label="Heading 1"
          title="Heading 1"
          @click="emitAction({ id: 'heading', headingLevel: 1 })"
        >
          <component :is="action.icon" :size="14" aria-hidden="true" />
          <span class="md-editor-toolbar-sr">Heading 1</span>
        </button>

        <button
          type="button"
          class="md-editor-toolbar-button md-editor-toolbar-button-caret"
          :disabled="action.disabled"
          :aria-label="isHeadingMenuOpen ? 'Close heading levels' : 'Open heading levels'"
          title="Heading levels"
          @click.stop="toggleHeadingMenu"
        >
          <ChevronDown :size="14" aria-hidden="true" />
          <span class="md-editor-toolbar-sr">Heading levels</span>
        </button>

        <div v-if="isHeadingMenuOpen" class="md-editor-toolbar-menu" role="menu" aria-label="Heading levels">
          <button
            v-for="level in headingMenuLevels"
            :key="level"
            type="button"
            class="md-editor-toolbar-menu-item"
            role="menuitem"
            :disabled="action.disabled"
            :title="`Heading ${level}`"
            @click="emitAction({ id: 'heading', headingLevel: level })"
          >
            H{{ level }}
          </button>
        </div>
      </div>

      <button
        v-else
        type="button"
        class="md-editor-toolbar-button"
        :class="{ 'is-active': action.isActive }"
        :disabled="action.disabled"
        :aria-label="action.ariaLabel"
        :title="action.title"
        @click="emitAction({ id: action.id })"
      >
        <component :is="action.icon" :size="14" aria-hidden="true" />
        <span class="md-editor-toolbar-sr">{{ action.label }}</span>
      </button>
    </template>
  </div>
</template>

<script setup lang="ts">
import { Bold, ChevronDown, Heading1, Italic, Link, List, ListOrdered, Minus, Quote, SquareCode, Strikethrough } from 'lucide-vue-next';
import { computed, onBeforeUnmount, onMounted, ref, type Component } from 'vue';
import { mdEditorToolbarActions, type MdEditorHeadingLevel, type MdEditorToolbarActionEvent, type MdEditorToolbarActionId, type MdEditorToolbarActionState } from './mdEditorToolbarActions';

const props = defineProps<{
  state: Partial<Record<MdEditorToolbarActionId, MdEditorToolbarActionState>>;
}>();

const emit = defineEmits<{
  action: [event: MdEditorToolbarActionEvent];
}>();

const actionIcons: Record<MdEditorToolbarActionId, Component> = {
  bold: Bold,
  italic: Italic,
  strike: Strikethrough,
  heading: Heading1,
  'bullet-list': List,
  'ordered-list': ListOrdered,
  quote: Quote,
  'code-block': SquareCode,
  link: Link,
  rule: Minus
};

const headingMenuLevels: MdEditorHeadingLevel[] = [2, 3];
const isHeadingMenuOpen = ref(false);
const headingSplitRef = ref<HTMLElement | null>(null);

const resolvedActions = computed(() => {
  return mdEditorToolbarActions.map(action => ({
    ...action,
    icon: actionIcons[action.id],
    disabled: props.state[action.id]?.disabled ?? true,
    isActive: props.state[action.id]?.isActive ?? false
  }));
});

function emitAction(event: MdEditorToolbarActionEvent) {
  isHeadingMenuOpen.value = false;
  emit('action', event);
}

function toggleHeadingMenu() {
  isHeadingMenuOpen.value = !isHeadingMenuOpen.value;
}

function onDocumentPointerDown(event: MouseEvent) {
  if (!isHeadingMenuOpen.value) {
    return;
  }

  const target = event.target;
  if (!(target instanceof Node)) {
    return;
  }

  if (headingSplitRef.value?.contains(target)) {
    return;
  }

  isHeadingMenuOpen.value = false;
}

onMounted(() => {
  window.addEventListener('mousedown', onDocumentPointerDown);
});

onBeforeUnmount(() => {
  window.removeEventListener('mousedown', onDocumentPointerDown);
});
</script>

<style scoped>
.md-editor-toolbar {
  display: flex;
  flex-wrap: wrap;
  gap: 0.35rem;
}

.md-editor-toolbar-split {
  position: relative;
  display: inline-flex;
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

.md-editor-toolbar-button-caret {
  min-width: 1.6rem;
  width: 1.6rem;
  border-left: none;
  border-top-left-radius: 0;
  border-bottom-left-radius: 0;
}

.md-editor-toolbar-split .md-editor-toolbar-button:first-child {
  border-top-right-radius: 0;
  border-bottom-right-radius: 0;
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

.md-editor-toolbar-menu {
  position: absolute;
  top: calc(100% + 0.2rem);
  left: 0;
  display: flex;
  flex-direction: column;
  min-width: 4rem;
  padding: 0.2rem;
  border: 1px solid #b8c8df;
  border-radius: 8px;
  background: #ffffff;
  box-shadow: 0 6px 18px rgba(19, 32, 49, 0.18);
  z-index: 5;
}

.md-editor-toolbar-menu-item {
  width: 100%;
  min-width: 0;
  border: 1px solid transparent;
  border-radius: 6px;
  background: transparent;
  color: #1d3b63;
  padding: 0.25rem 0.4rem;
  text-align: left;
  font-size: 0.78rem;
}

.md-editor-toolbar-menu-item:hover:not(:disabled),
.md-editor-toolbar-menu-item:focus-visible:not(:disabled) {
  border-color: #b8c8df;
  background: #edf3fc;
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
