import { Editor } from '@tiptap/core';
import { Markdown } from '@tiptap/markdown';
import StarterKit from '@tiptap/starter-kit';
import { describe, expect, it } from 'vitest';
import {
  buildAttachmentImageContentUrl,
  buildAttachmentImageReference,
  createMarkdownImageExtension,
  imageAltFromFileName,
  parseAttachmentImageReference
} from './markdownImages';

describe('card description images', () => {
  it('round-trips an encoded owner-relative attachment reference', () => {
    const fileName = "Architecture (final) #1.png";
    const reference = buildAttachmentImageReference(fileName);
    const markdown = `![Architecture](${reference})`;
    const editor = createEditor(markdown);

    try {
      expect(parseAttachmentImageReference(reference)).toBe(fileName);
      expect(editor.getMarkdown()).toBe(markdown);
    } finally {
      editor.destroy();
    }
  });

  it('preserves external images without accepting ephemeral sources', () => {
    const external = createEditor('![Remote](https://example.test/image.png)');
    const data = createEditor('![Inline](data:image/png;base64,AAAA)');
    const blob = createEditor('![Draft](blob:https://example.test/123)');

    try {
      expect(external.getMarkdown()).toBe('![Remote](https://example.test/image.png)');
      expect(data.getMarkdown()).toBe('Inline');
      expect(blob.getMarkdown()).toBe('Draft');
    } finally {
      external.destroy();
      data.destroy();
      blob.destroy();
    }
  });

  it('builds live and archived protected content URLs', () => {
    expect(buildAttachmentImageContentUrl({ apiBaseUrl: 'https://board.test', boardId: 2, cardId: 3 }, 'a b.png'))
      .toBe('https://board.test/api/boards/2/cards/3/attachments/image-content?fileName=a%20b.png');
    expect(buildAttachmentImageContentUrl({ apiBaseUrl: 'https://board.test', boardId: 2, cardId: 3, archived: true }, 'a.png'))
      .toBe('https://board.test/api/boards/2/cards/archived/3/attachments/image-content?fileName=a.png');
    expect(buildAttachmentImageContentUrl({
      apiBaseUrl: 'https://board.test',
      boardId: 2,
      cardId: 3,
      refreshKey: 42
    }, 'a b.png')).toBe('https://board.test/api/boards/2/cards/3/attachments/image-content?fileName=a%20b.png&v=42');
  });

  it('creates safe default alt text from the filename', () => {
    expect(imageAltFromFileName('Architecture [final].png')).toBe('Architecture final');
  });
});

function createEditor(markdown: string) {
  return new Editor({
    content: markdown,
    contentType: 'markdown',
    extensions: [StarterKit, createMarkdownImageExtension(() => null), Markdown]
  });
}
