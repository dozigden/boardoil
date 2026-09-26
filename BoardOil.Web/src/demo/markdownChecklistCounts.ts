import type { JSONContent } from '@tiptap/core';
import { TaskItem } from '@tiptap/extension-list/task-item';
import { TaskList } from '@tiptap/extension-list/task-list';
import { MarkdownManager } from '@tiptap/markdown';
import StarterKit from '@tiptap/starter-kit';
import { normaliseMarkdown } from '../shared/utils/markdown';

// The standalone demo calculates counts on description writes, like the server.
const markdownParser = new MarkdownManager({
  extensions: [
    StarterKit.configure({ link: false }),
    TaskList,
    TaskItem.configure({ nested: true })
  ]
});

export function getMarkdownChecklistCounts(description: string): { completed: number; total: number } {
  const counts = { completed: 0, total: 0 };
  const document = markdownParser.parse(normaliseMarkdown(description));

  function countChecklistItems(node: JSONContent) {
    if (node.type === 'taskItem') {
      counts.total += 1;
      if (node.attrs?.checked === true) {
        counts.completed += 1;
      }
    }

    node.content?.forEach(countChecklistItems);
  }

  countChecklistItems(document);
  return counts;
}
