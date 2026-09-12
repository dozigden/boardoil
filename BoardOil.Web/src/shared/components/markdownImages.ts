import Image from '@tiptap/extension-image';
import type { Node as ProseMirrorNode } from '@tiptap/pm/model';
import type { NodeView } from '@tiptap/pm/view';

const attachmentReferencePrefix = 'boardoil-attachment:';

export type MarkdownImageContext = {
  apiBaseUrl: string;
  boardId: number;
  cardId: number;
  archived?: boolean;
};

export type MarkdownImageUploadResult = { fileName: string } | null;

type MarkdownImageSource =
  | { kind: 'attachment'; url: string }
  | { kind: 'external'; url: string }
  | { kind: 'unavailable' };

export function createMarkdownImageExtension(getContext: () => MarkdownImageContext | null) {
  return Image.extend({
    parseMarkdown(token, helpers) {
      const source = typeof token.href === 'string' ? token.href : '';
      if (isEphemeralImageSource(source)) {
        return [];
      }
      return helpers.createNode('image', {
        src: source,
        title: token.title,
        alt: token.text
      });
    },
    renderMarkdown(node) {
      const source = typeof node.attrs?.src === 'string' ? node.attrs.src : '';
      if (isEphemeralImageSource(source)) {
        return '';
      }
      const alt = node.attrs?.alt ?? '';
      const title = node.attrs?.title ?? '';
      return title ? `![${alt}](${source} "${title}")` : `![${alt}](${source})`;
    },
    addNodeView() {
      return ({ node }) => createImageNodeView(node, getContext);
    }
  }).configure({
    allowBase64: false,
    inline: false,
    resize: false
  });
}

function isEphemeralImageSource(value: string): boolean {
  const source = value.trimStart().toLowerCase();
  return source.startsWith('data:') || source.startsWith('blob:');
}

export function buildAttachmentImageReference(fileName: string): string {
  const encoded = encodeURIComponent(fileName).replace(/[!'()*]/g, value =>
    `%${value.charCodeAt(0).toString(16).toUpperCase()}`);
  return `${attachmentReferencePrefix}${encoded}`;
}

export function parseAttachmentImageReference(value: string): string | null {
  if (!value.startsWith(attachmentReferencePrefix)) {
    return null;
  }

  try {
    const fileName = decodeURIComponent(value.slice(attachmentReferencePrefix.length));
    if (!fileName || fileName === '.' || fileName === '..' || fileName.length > 255
      || /[/\\\u0000-\u001f\u007f]/.test(fileName)) {
      return null;
    }
    return fileName;
  } catch {
    return null;
  }
}

export function buildAttachmentImageContentUrl(context: MarkdownImageContext, fileName: string): string {
  const ownerPath = context.archived
    ? `cards/archived/${context.cardId}`
    : `cards/${context.cardId}`;
  return `${context.apiBaseUrl}/api/boards/${context.boardId}/${ownerPath}/attachments/image-content?fileName=${encodeURIComponent(fileName)}`;
}

export function imageAltFromFileName(fileName: string): string {
  const extensionIndex = fileName.lastIndexOf('.');
  const withoutExtension = extensionIndex > 0 ? fileName.slice(0, extensionIndex) : fileName;
  return withoutExtension.replace(/[\\[\]\r\n]/g, ' ').replace(/\s+/g, ' ').trim();
}

function resolveImageSource(value: unknown, context: MarkdownImageContext | null): MarkdownImageSource {
  if (typeof value !== 'string') {
    return { kind: 'unavailable' };
  }

  const fileName = parseAttachmentImageReference(value);
  if (fileName) {
    return context
      ? { kind: 'attachment', url: buildAttachmentImageContentUrl(context, fileName) }
      : { kind: 'unavailable' };
  }

  try {
    const url = new URL(value);
    if (url.protocol === 'http:' || url.protocol === 'https:') {
      return { kind: 'external', url: url.toString() };
    }
  } catch {
    // Invalid and unsupported sources use the non-fetching placeholder.
  }

  return { kind: 'unavailable' };
}

function createImageNodeView(node: ProseMirrorNode, getContext: () => MarkdownImageContext | null): NodeView {
  const dom = document.createElement('figure');
  dom.className = 'md-image-node';
  dom.contentEditable = 'false';
  let currentNode = node;
  let image: HTMLImageElement | null = null;

  const render = () => {
    image = null;
    dom.replaceChildren();
    const alt = typeof currentNode.attrs.alt === 'string' ? currentNode.attrs.alt : '';
    const source = resolveImageSource(currentNode.attrs.src, getContext());

    if (source.kind === 'attachment') {
      const nextImage = document.createElement('img');
      nextImage.className = 'md-image-node-image';
      nextImage.alt = alt;
      nextImage.src = source.url;
      if (typeof currentNode.attrs.title === 'string' && currentNode.attrs.title) {
        nextImage.title = currentNode.attrs.title;
      }

      const placeholder = createPlaceholder(alt || 'Attachment image', 'Image unavailable');
      placeholder.hidden = true;
      nextImage.addEventListener('load', () => {
        nextImage.hidden = false;
        placeholder.hidden = true;
      });
      nextImage.addEventListener('error', () => {
        nextImage.hidden = true;
        placeholder.hidden = false;
      });
      image = nextImage;
      dom.append(nextImage, placeholder);
      return;
    }

    if (source.kind === 'external') {
      const nextImage = document.createElement('img');
      nextImage.className = 'md-image-node-image';
      nextImage.alt = alt;
      nextImage.loading = 'lazy';
      nextImage.referrerPolicy = 'no-referrer';
      if (typeof currentNode.attrs.title === 'string' && currentNode.attrs.title) {
        nextImage.title = currentNode.attrs.title;
      }

      const fallback = createExternalFallback(alt, source.url);
      fallback.hidden = true;
      nextImage.addEventListener('load', () => {
        nextImage.hidden = false;
        fallback.hidden = true;
      });
      nextImage.addEventListener('error', () => {
        nextImage.hidden = true;
        fallback.hidden = false;
      });
      nextImage.src = source.url;
      image = nextImage;
      dom.append(nextImage, fallback);
      return;
    }

    dom.append(createPlaceholder(alt || 'Image', 'Image unavailable'));
  };

  render();
  return {
    dom,
    update(updatedNode) {
      if (updatedNode.type !== currentNode.type) {
        return false;
      }
      currentNode = updatedNode;
      render();
      return true;
    },
    selectNode() {
      dom.classList.add('md-image-node--selected');
    },
    deselectNode() {
      dom.classList.remove('md-image-node--selected');
    },
    stopEvent(event) {
      return event.target instanceof HTMLAnchorElement;
    },
    ignoreMutation() {
      return true;
    },
    destroy() {
      if (image) {
        image.removeAttribute('src');
      }
    }
  };
}

function createPlaceholder(label: string, message: string): HTMLSpanElement {
  const placeholder = document.createElement('span');
  placeholder.className = 'md-image-node-placeholder';
  placeholder.setAttribute('role', 'img');
  placeholder.setAttribute('aria-label', label);
  placeholder.textContent = message;
  return placeholder;
}

function createExternalFallback(alt: string, url: string): HTMLSpanElement {
  const fallback = document.createElement('span');
  fallback.className = 'md-image-node-placeholder';
  fallback.textContent = alt ? `${alt}: External image unavailable` : 'External image unavailable';
  const link = document.createElement('a');
  link.className = 'md-image-node-link';
  link.href = url;
  link.target = '_blank';
  link.rel = 'noopener noreferrer';
  link.textContent = 'Open external image';
  fallback.append(document.createTextNode(' · '), link);
  return fallback;
}
