<template>
  <div class="md-editor" :style="{ '--md-editor-min-height': minHeight }">
    <MdEditorToolbar
      :state="toolbarState"
      :is-plain-text-mode="isPlainTextMode"
      @action="onToolbarAction"
      @toggle-plain-text-mode="togglePlainTextMode"
    />

    <div class="md-editor-input">
      <textarea
        v-if="isPlainTextMode"
        class="md-editor-textarea"
        :value="plainTextDraft"
        :aria-label="`${props.ariaLabel} markdown`"
        spellcheck="false"
        @focus="emit('focus')"
        @blur="emit('blur')"
        @input="onPlainTextInput(($event.target as HTMLTextAreaElement).value)"
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
import Link from '@tiptap/extension-link';
import { Markdown } from '@tiptap/markdown';
import StarterKit from '@tiptap/starter-kit';
import { EditorContent, useEditor } from '@tiptap/vue-3';
import { computed, ref, watch } from 'vue';
import MdLinkDialog from './MdLinkDialog.vue';
import MdEditorToolbar from './MdEditorToolbar.vue';
import { mdEditorToolbarActions, type MdEditorToolbarActionEvent, type MdEditorToolbarActionId, type MdEditorToolbarActionState } from './mdEditorToolbarActions';
import { isHttpOrHttpsUrl } from '../utils/linkUrl';

const props = withDefaults(defineProps<{
  modelValue: string;
  ariaLabel?: string;
  maxLength?: number;
  minHeight?: string;
}>(), {
  ariaLabel: 'Markdown editor',
  maxLength: 5000,
  minHeight: '12rem'
});

const emit = defineEmits<{
  'update:modelValue': [value: string];
  focus: [];
  blur: [];
}>();

const normalisedModelValue = computed(() => normaliseMarkdown(props.modelValue ?? ''));
const isPlainTextMode = ref(false);
const plainTextDraft = ref(normalisedModelValue.value);
const isLinkDialogOpen = ref(false);
const linkDraftText = ref('');
const linkDraftUrl = ref('');
const linkDialogCanRemove = ref(false);
const linkSelectionRange = ref<{ from: number; to: number } | null>(null);

const tiptapEditor = useEditor({
  content: '',
  contentType: 'markdown',
  extensions: [
    StarterKit,
    Link.configure({
      openOnClick: false,
      enableClickSelection: true,
      autolink: true,
      linkOnPaste: true,
      defaultProtocol: 'https',
      isAllowedUri: url => isHttpOrHttpsUrl(url),
      HTMLAttributes: {
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

function onToolbarAction(actionEvent: MdEditorToolbarActionEvent) {
  if (isPlainTextMode.value) {
    return;
  }

  const editor = tiptapEditor.value;
  if (!editor) {
    return;
  }

  const nextActionEvent: MdEditorToolbarActionEvent = actionEvent.id === 'heading'
    ? { id: actionEvent.id, headingLevel: actionEvent.headingLevel ?? 1 }
    : { id: actionEvent.id };

  const action = mdEditorToolbarActions.find(x => x.id === nextActionEvent.id);
  if (!action) {
    return;
  }

  if (!action.canRun(editor, nextActionEvent)) {
    return;
  }

  action.run(editor, { openLinkDialog }, nextActionEvent);
}

function togglePlainTextMode() {
  if (!isPlainTextMode.value) {
    closeLinkDialog();
    const editor = tiptapEditor.value;
    plainTextDraft.value = normaliseMarkdown(editor ? editor.getMarkdown() : normalisedModelValue.value);
    isPlainTextMode.value = true;
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

  if (nextValue === normalisedModelValue.value) {
    return;
  }

  emit('update:modelValue', nextValue);
}

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
  if (!Number.isFinite(props.maxLength) || props.maxLength <= 0) {
    return value;
  }

  return value.slice(0, props.maxLength);
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

  editor.commands.setContent(nextValue, { contentType: 'markdown' });
}

watch(
  normalisedModelValue,
  nextValue => {
    if (isPlainTextMode.value && plainTextDraft.value !== nextValue) {
      plainTextDraft.value = nextValue;
    }

    setEditorContent(nextValue);
  },
  { immediate: true }
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

.md-editor-textarea {
  flex: 1 1 0;
  min-height: var(--md-editor-min-height);
  max-height: 100%;
  resize: none;
  border: 1px solid var(--bo-border-default);
  border-radius: 8px;
  padding: 0.5rem;
  overflow-y: auto;
  white-space: pre;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, 'Liberation Mono', 'Courier New', monospace;
  line-height: 1.35;
}

.md-editor-textarea:focus {
  outline: none;
  border-color: var(--bo-colour-secondary);
}
</style>
