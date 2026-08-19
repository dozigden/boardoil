import { mergeAttributes } from '@tiptap/core';
import { Heading } from '@tiptap/extension-heading';

export const AnchoredHeading = Heading.extend({
  renderHTML({ node, HTMLAttributes }) {
    const configuredLevels = this.options.levels;
    const level = configuredLevels.includes(node.attrs.level)
      ? node.attrs.level
      : configuredLevels[0];
    const id = slugifyMarkdownHeading(node.textContent);

    return [
      `h${level}`,
      mergeAttributes(this.options.HTMLAttributes, HTMLAttributes, id ? { id } : {}),
      0
    ];
  }
});

export function slugifyMarkdownHeading(value: string) {
  return value
    .trim()
    .toLowerCase()
    .normalize('NFKD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
}
