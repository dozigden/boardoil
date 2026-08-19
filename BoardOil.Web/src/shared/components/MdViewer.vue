<template>
  <div class="md-viewer" :style="{ '--md-viewer-min-height': minHeight }">
    <EditorContent v-if="tiptapEditor" :editor="tiptapEditor" class="md-viewer-content" />
  </div>
</template>

<script setup lang="ts">
import Link from '@tiptap/extension-link';
import { TaskItem } from '@tiptap/extension-list/task-item';
import { TaskList } from '@tiptap/extension-list/task-list';
import { Markdown } from '@tiptap/markdown';
import StarterKit from '@tiptap/starter-kit';
import { EditorContent, useEditor } from '@tiptap/vue-3';
import { computed, nextTick, onBeforeUnmount, onMounted, watch } from 'vue';
import { isHttpOrHttpsUrl } from '../utils/linkUrl';
import { normaliseMarkdown as normaliseMarkdownValue } from '../utils/markdown';
import { AnchoredHeading } from './mdViewerHeadingAnchors';

const props = withDefaults(defineProps<{
  modelValue: string;
  ariaLabel?: string;
  activeHeadingAnchor?: string;
  headingAnchors?: boolean;
  maxLength?: number;
  minHeight?: string;
}>(), {
  activeHeadingAnchor: '',
  ariaLabel: 'Markdown content',
  headingAnchors: false,
  maxLength: 20_000,
  minHeight: '12rem'
});

const normalisedModelValue = computed(() => normaliseMarkdown(props.modelValue ?? ''));
let headingScrollFrame: number | null = null;

const tiptapEditor = useEditor({
  content: '',
  editable: false,
  contentType: 'markdown',
  extensions: [
    StarterKit.configure({
      heading: props.headingAnchors ? false : {},
      link: false
    }),
    ...(props.headingAnchors ? [AnchoredHeading] : []),
    TaskList,
    TaskItem.configure({
      nested: true
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
  return normaliseMarkdownValue(value, props.maxLength);
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
  scheduleActiveHeadingScroll();
}

function scrollToActiveHeading() {
  const anchor = props.activeHeadingAnchor;
  const editorElement = tiptapEditor.value?.view.dom;
  if (!anchor || !editorElement?.isConnected) {
    return;
  }

  const target = Array.from(editorElement.querySelectorAll<HTMLElement>('[id]'))
    .find(element => element.id === anchor);
  target?.scrollIntoView({ block: 'start' });
}

function scheduleActiveHeadingScroll() {
  if (headingScrollFrame !== null) {
    window.cancelAnimationFrame(headingScrollFrame);
  }

  void nextTick(() => {
    headingScrollFrame = window.requestAnimationFrame(() => {
      headingScrollFrame = null;
      scrollToActiveHeading();
    });
  });
}

onMounted(scheduleActiveHeadingScroll);
onBeforeUnmount(() => {
  if (headingScrollFrame !== null) {
    window.cancelAnimationFrame(headingScrollFrame);
  }
});

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

watch(
  () => props.activeHeadingAnchor,
  scheduleActiveHeadingScroll
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

.md-viewer-content :deep(.tiptap ul[data-type='taskList']) {
  list-style: none;
  margin: 0.45rem 0;
  padding-left: 0.2rem;
}

.md-viewer-content :deep(.tiptap ul[data-type='taskList'] > li) {
  display: flex;
  align-items: flex-start;
  gap: 0.45rem;
  margin: 0.3rem 0;
}

.md-viewer-content :deep(.tiptap ul[data-type='taskList'] > li > label) {
  display: inline-flex;
  align-items: center;
  flex: 0 0 auto;
  margin-top: 0.18rem;
}

.md-viewer-content :deep(.tiptap ul[data-type='taskList'] > li > label > span) {
  display: none;
}

.md-viewer-content :deep(.tiptap ul[data-type='taskList'] > li > label > input[type='checkbox']) {
  width: 1rem !important;
  height: 1rem !important;
  min-height: 0;
  margin: 0 !important;
  padding: 0 !important;
  flex: 0 0 auto;
}

.md-viewer-content :deep(.tiptap ul[data-type='taskList'] > li > div) {
  flex: 1 1 auto;
  min-width: 0;
}

.md-viewer-content :deep(.tiptap ul[data-type='taskList'] > li > div p) {
  margin: 0;
}

</style>
