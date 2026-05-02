import { Editor } from '@tiptap/core';
import { TaskItem } from '@tiptap/extension-list/task-item';
import { TaskList } from '@tiptap/extension-list/task-list';
import { Markdown } from '@tiptap/markdown';
import StarterKit from '@tiptap/starter-kit';
import { describe, expect, it } from 'vitest';

function createMarkdownEditor(content: string) {
  return new Editor({
    content,
    contentType: 'markdown',
    extensions: [
      StarterKit.configure({
        link: false
      }),
      TaskList,
      TaskItem.configure({
        nested: true
      }),
      Markdown
    ]
  });
}

describe('markdown checklist support', () => {
  it('round-trips markdown task list items', () => {
    const markdown = '- [ ] Open item\n- [x] Done item';
    const editor = createMarkdownEditor(markdown);

    try {
      expect(editor.getMarkdown()).toBe(markdown);
    } finally {
      editor.destroy();
    }
  });
});
