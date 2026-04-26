<template>
  <div class="md-viewer" :style="{ '--md-viewer-min-height': minHeight }">
    <EditorContent v-if="tiptapEditor" :editor="tiptapEditor" class="md-viewer-content" />
  </div>
</template>

<script setup lang="ts">
import Link from '@tiptap/extension-link';
import { Markdown } from '@tiptap/markdown';
import StarterKit from '@tiptap/starter-kit';
import { EditorContent, useEditor } from '@tiptap/vue-3';
import { computed, watch } from 'vue';
import { isHttpOrHttpsUrl } from '../utils/linkUrl';

const props = withDefaults(defineProps<{
  modelValue: string;
  ariaLabel?: string;
  maxLength?: number;
  minHeight?: string;
}>(), {
  ariaLabel: 'Markdown content',
  maxLength: 20_000,
  minHeight: '12rem'
});

const normalisedModelValue = computed(() => normaliseMarkdown(props.modelValue ?? ''));

const tiptapEditor = useEditor({
  content: '',
  editable: false,
  contentType: 'markdown',
  extensions: [
    StarterKit.configure({
      link: false
    }),
    Link.configure({
      openOnClick: false,
      autolink: true,
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
      if (mouseEvent.button !== 0) {
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
  if (editor.getMarkdown() === nextValue) {
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
.md-viewer {
  display: flex;
  flex: 1 1 0;
  min-height: 0;
  overflow: hidden;
}

.md-viewer-content {
  flex: 1 1 0;
  min-height: 0;
  overflow: hidden;
}

.md-viewer-content :deep(.tiptap) {
  height: 100%;
  min-height: var(--md-viewer-min-height);
  max-height: 100%;
  margin: 0;
  border: 1px solid var(--bo-border-soft);
  border-radius: 10px;
  padding: 0.75rem;
  background: var(--bo-surface-panel);
  font-size: 0.82rem;
  line-height: 1.45;
  white-space: pre-wrap;
  word-break: break-word;
  overflow-y: auto;
}

.md-viewer-content :deep(.tiptap > *:first-child) {
  margin-top: 0;
}

.md-viewer-content :deep(.tiptap > *:last-child) {
  margin-bottom: 0;
}
</style>
