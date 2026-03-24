<template>
  <div class="md-editor" :style="{ '--md-editor-min-height': minHeight }">
    <MdEditorToolbar :editor="tiptapEditor" />

    <div class="md-editor-input">
      <EditorContent v-if="tiptapEditor" :editor="tiptapEditor" class="md-editor-content" />
    </div>
  </div>
</template>

<script setup lang="ts">
import Link from '@tiptap/extension-link';
import { Markdown } from '@tiptap/markdown';
import StarterKit from '@tiptap/starter-kit';
import { EditorContent, useEditor } from '@tiptap/vue-3';
import { computed, watch } from 'vue';
import MdEditorToolbar from './MdEditorToolbar.vue';

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
  border: 1px solid #b8c8df;
  border-radius: 8px;
  padding: 0.5rem;
  white-space: pre-wrap;
  word-break: break-word;
  overflow-y: auto;
}

.md-editor-content :deep(.tiptap:focus) {
  outline: none;
  border-color: #5b7ca8;
}
</style>
