<template>
  <div class="md-editor" :style="{ '--md-editor-min-height': minHeight }">
    <MdEditorToolbar
      v-if="showToolbar"
      :state="toolbarState"
      :is-plain-text-mode="isPlainTextMode"
      @action="onToolbarAction"
      @toggle-plain-text-mode="togglePlainTextMode"
    />

    <div class="md-editor-input">
      <textarea
        v-if="isPlainTextMode"
        ref="plainTextAreaRef"
        class="md-editor-textarea"
        :value="plainTextDraft"
        :aria-label="`${props.ariaLabel} markdown`"
        spellcheck="false"
        @focus="emit('focus')"
        @blur="emit('blur')"
        @input="onPlainTextInput(($event.target as HTMLTextAreaElement).value)"
        @keydown.esc.prevent="emit('escape')"
      />
      <EditorContent v-else-if="tiptapEditor" :editor="tiptapEditor" class="md-editor-content" />
    </div>

    <MdLinkDialog
      :open="isLinkDialogOpen"
      :initial-text="linkDraftText"
      :initial-url="linkDraftUrl"
      :can-remove="linkDialogCanRemove"
      @cancel="closeLinkDialog"
      @save="saveLinkDialog"
      @remove="removeLinkFromDialog"
    />
  </div>
</template>

<script setup lang="ts">
import type { Editor as TiptapEditor } from '@tiptap/core';
import { TaskItem } from '@tiptap/extension-list/task-item';
import { TaskList } from '@tiptap/extension-list/task-list';
import Link from '@tiptap/extension-link';
import { Markdown } from '@tiptap/markdown';
import StarterKit from '@tiptap/starter-kit';
import { EditorContent, useEditor } from '@tiptap/vue-3';
import { computed, nextTick, ref, watch } from 'vue';
import MdLinkDialog from './MdLinkDialog.vue';
import MdEditorToolbar from './MdEditorToolbar.vue';
import { mdEditorToolbarActions, type MdEditorToolbarActionEvent, type MdEditorToolbarActionId, type MdEditorToolbarActionState } from './mdEditorToolbarActions';
import { runMdEditorToolbarAction } from './mdEditorController';
import { syncPlainTextAreaHeight } from './mdEditorPlainTextSizing';
import { isHttpOrHttpsUrl } from '../utils/linkUrl';
import { normaliseMarkdown as normaliseMarkdownValue } from '../utils/markdown';

const props = withDefaults(defineProps<{
  modelValue: string;
  ariaLabel?: string;
  maxLength?: number;
  minHeight?: string;
  showToolbar?: boolean;
}>(), {
  ariaLabel: 'Markdown editor',
  maxLength: 20_000,
  minHeight: '12rem',
  showToolbar: true
});

const emit = defineEmits<{
  'update:modelValue': [value: string];
  focus: [];
  blur: [];
  escape: [];
  'toolbar-state-change': [value: Partial<Record<MdEditorToolbarActionId, MdEditorToolbarActionState>>];
  'plain-text-mode-change': [value: boolean];
}>();

const normalisedModelValue = computed(() => normaliseMarkdown(props.modelValue ?? ''));
const isPlainTextMode = ref(false);
const plainTextDraft = ref(normalisedModelValue.value);
const plainTextAreaRef = ref<HTMLTextAreaElement | null>(null);
const isLinkDialogOpen = ref(false);
const linkDraftText = ref('');
const linkDraftUrl = ref('');
const linkDialogCanRemove = ref(false);
const linkSelectionRange = ref<{ from: number; to: number } | null>(null);
const linkOpenModifier = /^(Mac|iPhone|iPad|iPod)/i.test(navigator.platform) ? 'Cmd' : 'Ctrl';
const linkTooltip = `${linkOpenModifier}-click to open. Use the Link button to edit.`;

const tiptapEditor = useEditor({
  content: '',
  contentType: 'markdown',
  extensions: [
    StarterKit.configure({
      link: false
    }),
    TaskList,
    TaskItem.configure({
      nested: true
    }),
    Link.configure({
      openOnClick: false,
      enableClickSelection: true,
      autolink: true,
      linkOnPaste: true,
      defaultProtocol: 'https',
      isAllowedUri: url => isHttpOrHttpsUrl(url),
      HTMLAttributes: {
        'data-link-tooltip': linkTooltip,
        'aria-description': linkTooltip,
        target: null,
        rel: null
      }
    }),
    Markdown
  ],
  editorProps: {
    attributes: {
      'aria-label': props.ariaLabel
    },
    handleKeyDown: (_view, event) => {
      if (event.key !== 'Escape') {
        return false;
      }

      emit('escape');
      return true;
    },
    handleClick: (_view, _pos, event) => {
      const mouseEvent = event as MouseEvent;
      if (mouseEvent.button !== 0 || (!mouseEvent.metaKey && !mouseEvent.ctrlKey)) {
        return false;
      }

      const target = mouseEvent.target;
      if (!(target instanceof Element)) {
        return false;
      }

      const link = target.closest('a');
      const href = link?.getAttribute('href');
      if (!href) {
        return false;
      }

      window.open(href, '_blank', 'noopener,noreferrer');
      mouseEvent.preventDefault();
      return true;
    }
  },
  onFocus: () => {
    emit('focus');
  },
  onBlur: () => {
    emit('blur');
  },
  onUpdate: ({ editor }) => {
    const currentValue = editor.getMarkdown();
    const nextValue = normaliseMarkdown(currentValue);
    if (nextValue !== currentValue) {
      setEditorContent(nextValue);
    }

    if (nextValue === normalisedModelValue.value) {
      return;
    }

    emit('update:modelValue', nextValue);
  }
});

const toolbarState = computed<Partial<Record<MdEditorToolbarActionId, MdEditorToolbarActionState>>>(() => {
  const editor = tiptapEditor.value;
  const state: Partial<Record<MdEditorToolbarActionId, MdEditorToolbarActionState>> = {};

  for (const action of mdEditorToolbarActions) {
    const defaultActionEvent: MdEditorToolbarActionEvent = action.id === 'heading'
      ? { id: action.id, headingLevel: 1 }
      : { id: action.id };

    state[action.id] = {
      disabled: !editor || !action.canRun(editor, defaultActionEvent),
      isActive: editor ? (action.isActive?.(editor, defaultActionEvent) ?? false) : false
    };
  }

  return state;
});

watch(toolbarState, value => {
  emit('toolbar-state-change', value);
}, { immediate: true });

watch(isPlainTextMode, value => {
  emit('plain-text-mode-change', value);
}, { immediate: true });

function onToolbarAction(actionEvent: MdEditorToolbarActionEvent) {
  runMdEditorToolbarAction(actionEvent, tiptapEditor.value ?? null, isPlainTextMode.value, openLinkDialog);
}

function runToolbarAction(actionEvent: MdEditorToolbarActionEvent) {
  onToolbarAction(actionEvent);
}

function togglePlainTextMode() {
  if (!isPlainTextMode.value) {
    closeLinkDialog();
    const editor = tiptapEditor.value;
    plainTextDraft.value = normaliseMarkdown(editor ? editor.getMarkdown() : normalisedModelValue.value);
    isPlainTextMode.value = true;
    void nextTick(() => {
      syncPlainTextAreaHeight(plainTextAreaRef.value);
    });
    return;
  }

  isPlainTextMode.value = false;
  const nextValue = normaliseMarkdown(plainTextDraft.value);
  plainTextDraft.value = nextValue;
  setEditorContent(nextValue);

  if (nextValue === normalisedModelValue.value) {
    return;
  }

  emit('update:modelValue', nextValue);
}

function onPlainTextInput(value: string) {
  const nextValue = normaliseMarkdown(value);
  plainTextDraft.value = nextValue;
  syncPlainTextAreaHeight(plainTextAreaRef.value);

  if (nextValue === normalisedModelValue.value) {
    return;
  }

  emit('update:modelValue', nextValue);
}

defineExpose({
  runToolbarAction,
  togglePlainTextMode
});

function openLinkDialog(editor: TiptapEditor) {
  editor.chain().focus().run();
  if (editor.isActive('link')) {
    editor.chain().focus().extendMarkRange('link').run();
  }

  const from = editor.state.selection.from;
  const to = editor.state.selection.to;
  const selectedText = from === to ? '' : editor.state.doc.textBetween(from, to, ' ', ' ');
  const currentUrl = (editor.getAttributes('link').href as string | undefined) ?? '';

  linkSelectionRange.value = { from, to };
  linkDraftText.value = selectedText.length > 0 ? selectedText : currentUrl;
  linkDraftUrl.value = currentUrl;
  linkDialogCanRemove.value = editor.isActive('link');
  isLinkDialogOpen.value = true;
}

function closeLinkDialog() {
  isLinkDialogOpen.value = false;
  linkSelectionRange.value = null;
}

function saveLinkDialog(nextLink: { text: string; url: string }) {
  const editor = tiptapEditor.value;
  if (!editor || !linkSelectionRange.value) {
    closeLinkDialog();
    return;
  }

  const range = linkSelectionRange.value;
  const href = nextLink.url;
  const text = nextLink.text.trim().length > 0 ? nextLink.text : href;
  const from = range.from;
  const to = range.to;

  if (from === to) {
    editor.chain().focus().setTextSelection(from).insertContent(text).setTextSelection({
      from,
      to: from + text.length
    }).setLink({ href }).run();
    closeLinkDialog();
    return;
  }

  editor.chain().focus().setTextSelection({ from, to }).insertContent(text).setTextSelection({
    from,
    to: from + text.length
  }).setLink({ href }).run();
  closeLinkDialog();
}

function removeLinkFromDialog() {
  const editor = tiptapEditor.value;
  if (!editor || !linkSelectionRange.value) {
    closeLinkDialog();
    return;
  }

  const range = linkSelectionRange.value;
  if (range.from === range.to) {
    editor.chain().focus().setTextSelection(range.from).extendMarkRange('link').unsetLink().run();
    closeLinkDialog();
    return;
  }

  editor.chain().focus().setTextSelection({
    from: range.from,
    to: range.to
  }).extendMarkRange('link').unsetLink().run();
  closeLinkDialog();
}

function normaliseMarkdown(value: string) {
  return normaliseMarkdownValue(value, props.maxLength);
}

function setEditorContent(value: string) {
  const editor = tiptapEditor.value;
  if (!editor) {
    return;
  }

  const nextValue = normaliseMarkdown(value);
  const currentValue = normaliseMarkdown(editor.getMarkdown());
  if (currentValue === nextValue) {
    return;
  }

  editor.commands.setContent(nextValue, {
    contentType: 'markdown',
    emitUpdate: false
  });
}

watch(
  normalisedModelValue,
  nextValue => {
    if (isPlainTextMode.value) {
      if (plainTextDraft.value !== nextValue) {
        plainTextDraft.value = nextValue;
        void nextTick(() => {
          syncPlainTextAreaHeight(plainTextAreaRef.value);
        });
      }

      return;
    }

    setEditorContent(nextValue);
  },
  { immediate: true }
);

watch(
  isPlainTextMode,
  isEnabled => {
    if (!isEnabled) {
      return;
    }

    void nextTick(() => {
      syncPlainTextAreaHeight(plainTextAreaRef.value);
    });
  }
);

watch(
  tiptapEditor,
  editor => {
    if (!editor) {
      return;
    }

    setEditorContent(normalisedModelValue.value);
  },
  { immediate: true }
);
</script>

<style scoped>
.md-editor {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  flex: 1 1 0;
  min-height: 0;
  overflow: hidden;
}

.md-editor-input {
  flex: 1 1 0;
  min-height: 0;
  display: flex;
  overflow: hidden;
}

.md-editor-content {
  flex: 1 1 0;
  min-height: 0;
  overflow: hidden;
}

.md-editor-content :deep(.tiptap) {
  height: 100%;
  min-height: var(--md-editor-min-height);
  max-height: 100%;
  border: 1px solid var(--bo-border-default);
  border-radius: 8px;
  padding: 0.5rem;
  white-space: pre-wrap;
  word-break: break-word;
  overflow-y: auto;
}

.md-editor-content :deep(.tiptap:focus) {
  outline: none;
  border-color: var(--bo-colour-secondary);
}

.md-editor-content :deep(.tiptap a[data-link-tooltip]) {
  position: relative;
}

.md-editor-content :deep(.tiptap a[data-link-tooltip]:is(:hover, :focus-visible)::after) {
  content: attr(data-link-tooltip);
  position: absolute;
  z-index: 3;
  top: calc(100% + 0.35rem);
  left: 0;
  width: max-content;
  max-width: min(22rem, calc(100vw - 2rem));
  border: 1px solid var(--bo-border-soft);
  border-radius: 6px;
  padding: 0.3rem 0.45rem;
  background: var(--bo-surface-base);
  box-shadow: var(--bo-shadow-pop);
  color: var(--bo-ink-default);
  font-size: 0.78rem;
  font-weight: 400;
  line-height: 1.25;
  pointer-events: none;
}

.md-editor-content :deep(.tiptap ul[data-type='taskList']) {
  list-style: none;
  margin: 0.45rem 0;
  padding-left: 0.2rem;
}

.md-editor-content :deep(.tiptap ul[data-type='taskList'] > li) {
  display: flex;
  align-items: flex-start;
  gap: 0.45rem;
  margin: 0.3rem 0;
}

.md-editor-content :deep(.tiptap ul[data-type='taskList'] > li > label) {
  display: inline-flex;
  align-items: center;
  flex: 0 0 auto;
  margin-top: 0.18rem;
}

.md-editor-content :deep(.tiptap ul[data-type='taskList'] > li > label > span) {
  display: none;
}

.md-editor-content :deep(.tiptap ul[data-type='taskList'] > li > label > input[type='checkbox']) {
  width: 1rem !important;
  height: 1rem !important;
  min-height: 0;
  margin: 0 !important;
  padding: 0 !important;
  flex: 0 0 auto;
}

.md-editor-content :deep(.tiptap ul[data-type='taskList'] > li > div) {
  flex: 1 1 auto;
  min-width: 0;
}

.md-editor-content :deep(.tiptap ul[data-type='taskList'] > li > div p) {
  margin: 0;
}

.md-editor-textarea {
  flex: 1 1 0;
  min-height: var(--md-editor-min-height);
  resize: none;
  border: 1px solid var(--bo-border-default);
  border-radius: 8px;
  padding: 0.5rem;
  overflow-y: hidden;
  white-space: pre-wrap;
  overflow-wrap: anywhere;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, 'Liberation Mono', 'Courier New', monospace;
  line-height: 1.35;
}

.md-editor-textarea:focus {
  outline: none;
  border-color: var(--bo-colour-secondary);
}
</style>
