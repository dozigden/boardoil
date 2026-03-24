<template>
  <div v-if="editor" class="md-editor-toolbar" role="toolbar" aria-label="Markdown formatting">
    <button
      v-for="action in toolbarActions"
      :key="action.id"
      type="button"
      class="md-editor-toolbar-button"
      :class="{ 'is-active': isActionActive(action) }"
      :disabled="!canRunAction(action)"
      :aria-label="action.ariaLabel"
      :title="action.title"
      @click="runAction(action)"
    >
      {{ action.label }}
    </button>
  </div>
</template>

<script setup lang="ts">
import type { Editor as TiptapEditor } from '@tiptap/core';
import { computed } from 'vue';

type ToolbarAction = {
  id: string;
  label: string;
  ariaLabel: string;
  title: string;
  canRun: (editor: TiptapEditor) => boolean;
  run: (editor: TiptapEditor) => void;
  isActive?: (editor: TiptapEditor) => boolean;
};

const props = defineProps<{
  editor: TiptapEditor | null | undefined;
}>();

const editor = computed(() => props.editor ?? null);

function canRunCommand(command: (editor: TiptapEditor) => boolean) {
  if (!editor.value) {
    return false;
  }

  return command(editor.value);
}

function runCommand(command: (editor: TiptapEditor) => void) {
  if (!editor.value) {
    return;
  }

  command(editor.value);
}

function normaliseLinkUrl(value: string) {
  if (value.length === 0) {
    return '';
  }

  if (/^[a-z][a-z0-9+\-.]*:/i.test(value)) {
    return value;
  }

  return `https://${value}`;
}

function setLink(editor: TiptapEditor) {
  const currentUrl = (editor.getAttributes('link').href as string | undefined) ?? '';
  const enteredUrl = window.prompt('Enter URL', currentUrl);
  if (enteredUrl === null) {
    return;
  }

  const trimmedUrl = enteredUrl.trim();
  if (trimmedUrl.length === 0) {
    editor.chain().focus().extendMarkRange('link').unsetLink().run();
    return;
  }

  const href = normaliseLinkUrl(trimmedUrl);
  if (editor.isActive('link')) {
    editor.chain().focus().extendMarkRange('link').setLink({ href }).run();
    return;
  }

  if (editor.state.selection.empty) {
    editor.chain().focus().insertContent({
      type: 'text',
      text: href,
      marks: [{ type: 'link', attrs: { href } }]
    }).run();
    return;
  }

  editor.chain().focus().extendMarkRange('link').setLink({ href }).run();
}

const toolbarActions: ToolbarAction[] = [
  {
    id: 'bold',
    label: 'Bold',
    ariaLabel: 'Bold',
    title: 'Bold',
    isActive: editor => editor.isActive('bold'),
    canRun: editor => editor.can().chain().focus().toggleBold().run(),
    run: editor => {
      editor.chain().focus().toggleBold().run();
    }
  },
  {
    id: 'italic',
    label: 'Italic',
    ariaLabel: 'Italic',
    title: 'Italic',
    isActive: editor => editor.isActive('italic'),
    canRun: editor => editor.can().chain().focus().toggleItalic().run(),
    run: editor => {
      editor.chain().focus().toggleItalic().run();
    }
  },
  {
    id: 'strike',
    label: 'Strike',
    ariaLabel: 'Strike',
    title: 'Strike',
    isActive: editor => editor.isActive('strike'),
    canRun: editor => editor.can().chain().focus().toggleStrike().run(),
    run: editor => {
      editor.chain().focus().toggleStrike().run();
    }
  },
  {
    id: 'heading',
    label: 'H2',
    ariaLabel: 'Heading',
    title: 'Heading',
    isActive: editor => editor.isActive('heading', { level: 2 }),
    canRun: editor => editor.can().chain().focus().toggleHeading({ level: 2 }).run(),
    run: editor => {
      editor.chain().focus().toggleHeading({ level: 2 }).run();
    }
  },
  {
    id: 'bullet-list',
    label: 'Bullets',
    ariaLabel: 'Bullet list',
    title: 'Bullet list',
    isActive: editor => editor.isActive('bulletList'),
    canRun: editor => editor.can().chain().focus().toggleBulletList().run(),
    run: editor => {
      editor.chain().focus().toggleBulletList().run();
    }
  },
  {
    id: 'ordered-list',
    label: 'Numbers',
    ariaLabel: 'Numbered list',
    title: 'Numbered list',
    isActive: editor => editor.isActive('orderedList'),
    canRun: editor => editor.can().chain().focus().toggleOrderedList().run(),
    run: editor => {
      editor.chain().focus().toggleOrderedList().run();
    }
  },
  {
    id: 'quote',
    label: 'Quote',
    ariaLabel: 'Block quote',
    title: 'Block quote',
    isActive: editor => editor.isActive('blockquote'),
    canRun: editor => editor.can().chain().focus().toggleBlockquote().run(),
    run: editor => {
      editor.chain().focus().toggleBlockquote().run();
    }
  },
  {
    id: 'code-block',
    label: 'Code',
    ariaLabel: 'Code block',
    title: 'Code block',
    isActive: editor => editor.isActive('codeBlock'),
    canRun: editor => editor.can().chain().focus().toggleCodeBlock().run(),
    run: editor => {
      editor.chain().focus().toggleCodeBlock().run();
    }
  },
  {
    id: 'link',
    label: 'Link',
    ariaLabel: 'Set link',
    title: 'Set link',
    isActive: editor => editor.isActive('link'),
    canRun: () => true,
    run: editor => {
      setLink(editor);
    }
  },
  {
    id: 'unlink',
    label: 'Unlink',
    ariaLabel: 'Remove link',
    title: 'Remove link',
    canRun: editor => editor.isActive('link'),
    run: editor => {
      editor.chain().focus().extendMarkRange('link').unsetLink().run();
    }
  },
  {
    id: 'rule',
    label: 'Rule',
    ariaLabel: 'Horizontal rule',
    title: 'Horizontal rule',
    canRun: editor => editor.can().chain().focus().setHorizontalRule().run(),
    run: editor => {
      editor.chain().focus().setHorizontalRule().run();
    }
  },
  {
    id: 'undo',
    label: 'Undo',
    ariaLabel: 'Undo',
    title: 'Undo',
    canRun: editor => editor.can().chain().focus().undo().run(),
    run: editor => {
      editor.chain().focus().undo().run();
    }
  },
  {
    id: 'redo',
    label: 'Redo',
    ariaLabel: 'Redo',
    title: 'Redo',
    canRun: editor => editor.can().chain().focus().redo().run(),
    run: editor => {
      editor.chain().focus().redo().run();
    }
  }
];

function canRunAction(action: ToolbarAction) {
  return canRunCommand(editor => action.canRun(editor));
}

function runAction(action: ToolbarAction) {
  runCommand(editor => {
    action.run(editor);
  });
}

function isActionActive(action: ToolbarAction) {
  if (!action.isActive) {
    return false;
  }

  return canRunCommand(editor => action.isActive?.(editor) ?? false);
}
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
