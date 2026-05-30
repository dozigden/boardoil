<template>
  <div class="md-editor-toolbar" role="toolbar" aria-label="Markdown formatting">
    <template v-for="action in resolvedActions" :key="action.id">
      <div v-if="action.id === 'heading'" ref="headingSplitRef" class="md-editor-toolbar-split">
        <button
          type="button"
          class="btn btn--toolbar md-editor-toolbar-button"
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
          class="btn btn--toolbar md-editor-toolbar-button md-editor-toolbar-button-caret"
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
            class="btn btn--toolbar-menu md-editor-toolbar-menu-item"
            role="menuitem"
            :disabled="action.disabled"
            :title="`Heading ${level}`"
            @click="emitAction({ id: 'heading', headingLevel: level })"
          >
            H{{ level }}
          </button>
        </div>
      </div>

      <template v-else-if="action.id === 'bullet-list'">
        <button
          type="button"
          class="btn btn--toolbar md-editor-toolbar-button md-editor-toolbar-list-action"
          :class="{ 'is-active': action.isActive }"
          :disabled="action.disabled"
          :aria-label="action.ariaLabel"
          :title="action.title"
          @click="emitAction({ id: action.id })"
        >
          <component :is="action.icon" :size="14" aria-hidden="true" />
          <span class="md-editor-toolbar-sr">{{ action.label }}</span>
        </button>

        <div v-if="listSplitAction" ref="listSplitRef" class="md-editor-toolbar-split md-editor-toolbar-mobile-list-split">
          <button
            type="button"
            class="btn btn--toolbar md-editor-toolbar-button"
            :class="{ 'is-active': listSplitAction.isActive }"
            :disabled="listSplitAction.disabled"
            aria-label="Bullet list"
            title="Bullet list"
            @click="emitAction({ id: 'bullet-list' })"
          >
            <List :size="14" aria-hidden="true" />
            <span class="md-editor-toolbar-sr">Bullet list</span>
          </button>

          <button
            type="button"
            class="btn btn--toolbar md-editor-toolbar-button md-editor-toolbar-button-caret"
            :disabled="listSplitAction.disabled"
            :aria-label="isListMenuOpen ? 'Close list styles' : 'Open list styles'"
            title="List styles"
            @click.stop="toggleListMenu"
          >
            <ChevronDown :size="14" aria-hidden="true" />
            <span class="md-editor-toolbar-sr">List styles</span>
          </button>

          <div v-if="isListMenuOpen" class="md-editor-toolbar-menu" role="menu" aria-label="List styles">
            <button
              v-for="menuAction in listMenuActions"
              :key="menuAction.id"
              type="button"
              class="btn btn--toolbar-menu md-editor-toolbar-menu-item"
              role="menuitem"
              :disabled="menuAction.disabled"
              :title="menuAction.label"
              @click="emitAction({ id: menuAction.id })"
            >
              {{ menuAction.label }}
            </button>
          </div>
        </div>
      </template>

      <button
        v-else
        type="button"
        class="btn btn--toolbar md-editor-toolbar-button"
        :class="[
          { 'is-active': action.isActive },
          action.id === 'ordered-list' || action.id === 'task-list'
            ? 'md-editor-toolbar-list-action'
            : ''
        ]"
        :disabled="action.disabled"
        :aria-label="action.ariaLabel"
        :title="action.title"
        @click="emitAction({ id: action.id })"
      >
        <component :is="action.icon" :size="14" aria-hidden="true" />
        <span class="md-editor-toolbar-sr">{{ action.label }}</span>
      </button>
    </template>

    <button
      type="button"
      class="btn btn--toolbar md-editor-toolbar-mode-button"
      :class="{ 'is-active': isPlainTextMode }"
      :title="isPlainTextMode ? 'Switch to rich editor' : 'Switch to markdown text editor'"
      :aria-label="isPlainTextMode ? 'Switch to rich editor' : 'Switch to markdown text editor'"
      @click="emitToggleMode"
    >
      <FileText :size="14" aria-hidden="true" />
      <span>{{ isPlainTextMode ? 'Rich' : 'Markdown' }}</span>
    </button>
  </div>
</template>

<script setup lang="ts">
import { Bold, CheckSquare, ChevronDown, FileText, Heading1, Italic, Link, List, ListOrdered, Minus, Quote, SquareCode, Strikethrough } from 'lucide-vue-next';
import { computed, ref, type Component } from 'vue';
import { useClickOutside } from '../composables/useClickOutside';
import { mdEditorToolbarActions, type MdEditorHeadingLevel, type MdEditorToolbarActionEvent, type MdEditorToolbarActionId, type MdEditorToolbarActionState } from './mdEditorToolbarActions';

const props = defineProps<{
  state: Partial<Record<MdEditorToolbarActionId, MdEditorToolbarActionState>>;
  isPlainTextMode: boolean;
}>();

const emit = defineEmits<{
  action: [event: MdEditorToolbarActionEvent];
  'toggle-plain-text-mode': [];
}>();

const actionIcons: Record<MdEditorToolbarActionId, Component> = {
  bold: Bold,
  italic: Italic,
  strike: Strikethrough,
  heading: Heading1,
  'bullet-list': List,
  'ordered-list': ListOrdered,
  'task-list': CheckSquare,
  quote: Quote,
  'code-block': SquareCode,
  link: Link,
  rule: Minus
};

const headingMenuLevels: MdEditorHeadingLevel[] = [2, 3];
const isHeadingMenuOpen = ref(false);
const headingSplitRef = ref<HTMLElement | null>(null);
const isListMenuOpen = ref(false);
const listSplitRef = ref<HTMLElement | null>(null);

const resolvedActions = computed(() => {
  return mdEditorToolbarActions.map(action => ({
    ...action,
    icon: actionIcons[action.id],
    disabled: props.isPlainTextMode || (props.state[action.id]?.disabled ?? true),
    isActive: props.state[action.id]?.isActive ?? false
  }));
});

const listMenuActions = computed(() => resolvedActions.value.filter(action =>
  action.id === 'bullet-list' || action.id === 'ordered-list' || action.id === 'task-list'
));

const listSplitAction = computed(() => {
  const bulletAction = listMenuActions.value.find(action => action.id === 'bullet-list');
  if (!bulletAction) {
    return null;
  }

  const isActive = listMenuActions.value.some(action => action.isActive);
  return {
    ...bulletAction,
    isActive
  };
});

function emitAction(event: MdEditorToolbarActionEvent) {
  isHeadingMenuOpen.value = false;
  isListMenuOpen.value = false;
  emit('action', event);
}

function toggleHeadingMenu() {
  isListMenuOpen.value = false;
  isHeadingMenuOpen.value = !isHeadingMenuOpen.value;
}

function toggleListMenu() {
  isHeadingMenuOpen.value = false;
  isListMenuOpen.value = !isListMenuOpen.value;
}

function emitToggleMode() {
  isHeadingMenuOpen.value = false;
  isListMenuOpen.value = false;
  emit('toggle-plain-text-mode');
}

useClickOutside(headingSplitRef, () => {
  isHeadingMenuOpen.value = false;
}, () => isHeadingMenuOpen.value);

useClickOutside(listSplitRef, () => {
  isListMenuOpen.value = false;
}, () => isListMenuOpen.value);
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

.md-editor-toolbar-menu {
  position: absolute;
  top: calc(100% + 0.2rem);
  left: 0;
  display: flex;
  flex-direction: column;
  min-width: 4rem;
  padding: 0.2rem;
  border: 1px solid var(--bo-border-brand);
  border-radius: 8px;
  background: var(--bo-surface-base);
  box-shadow: var(--bo-shadow-pop);
  z-index: 5;
}

.md-editor-toolbar-menu-item {
  min-width: 0;
  text-align: left;
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

.md-editor-toolbar-mode-button {
  margin-left: auto;
  min-width: 0;
  height: 2rem;
  gap: 0.3rem;
  padding: 0.2rem 0.45rem;
  font-size: 0.78rem;
}

.md-editor-toolbar-mobile-list-split {
  display: none;
}

@media (max-width: 720px) {
  .md-editor-toolbar-list-action {
    display: none;
  }

  .md-editor-toolbar-mobile-list-split {
    display: inline-flex;
  }
}
</style>
