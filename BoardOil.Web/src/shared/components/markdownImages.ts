import type { Editor } from '@tiptap/core';
import Image from '@tiptap/extension-image';
import type { Node as ProseMirrorNode } from '@tiptap/pm/model';
import { Plugin, PluginKey } from '@tiptap/pm/state';
import { Decoration, DecorationSet, type NodeView } from '@tiptap/pm/view';

const attachmentReferencePrefix = 'boardoil-attachment:';
const imageRefreshPluginKey = new PluginKey<number>('boardoilMarkdownImageRefresh');
export const boardOilAttachmentImageDragType = 'application/x-boardoil-attachment-image';

export type MarkdownImageContext = {
  apiBaseUrl: string;
  boardId: number;
  cardId: number;
  archived?: boolean;
  refreshKey?: string | number;
};

export type MarkdownImageUploadResult = { fileName: string } | null;
export type MarkdownImageUploadProgress = (file: File, percent: number) => void;
export type MarkdownImageUploadOptions = { reuseExisting: boolean };
export type MarkdownImageUpload = (
  files: File[],
  onProgress: MarkdownImageUploadProgress,
  options: MarkdownImageUploadOptions
) => Promise<MarkdownImageUploadResult[]>;

export type MarkdownImageSource =
  | { kind: 'attachment'; url: string }
  | { kind: 'external'; url: string }
  | { kind: 'unavailable' };

export type MarkdownImageActivation = {
  alt: string;
  attachmentFileName: string | null;
  position: number;
  source: MarkdownImageSource;
};

export type MarkdownImageRemove = (image: MarkdownImageActivation) => Promise<boolean>;

export type MarkdownImageExtensionOptions = {
  editable?: boolean;
  onActivate?: (image: MarkdownImageActivation) => void;
  onRemove?: (image: MarkdownImageActivation) => void;
};

export function createMarkdownImageExtension(
  getContext: () => MarkdownImageContext | null,
  options: MarkdownImageExtensionOptions = {}
) {
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
      return ({ node, getPos }) => createImageNodeView(node, getPos, getContext, options);
    },
    addProseMirrorPlugins() {
      return [createImageRefreshPlugin()];
    }
  }).configure({
    allowBase64: false,
    inline: false,
    resize: false
  });
}

export function refreshMarkdownImages(editor: Editor | null): void {
  if (!editor || editor.isDestroyed) {
    return;
  }
  editor.view.dispatch(editor.state.tr.setMeta(imageRefreshPluginKey, true));
}

function createImageRefreshPlugin(): Plugin<number> {
  return new Plugin<number>({
    key: imageRefreshPluginKey,
    state: {
      init: () => 0,
      apply(transaction, revision) {
        return transaction.getMeta(imageRefreshPluginKey) ? revision + 1 : revision;
      }
    },
    props: {
      decorations(state) {
        const revision = imageRefreshPluginKey.getState(state) ?? 0;
        const decorations: Decoration[] = [];
        state.doc.descendants((node, position) => {
          if (node.type.name === 'image') {
            decorations.push(Decoration.node(position, position + node.nodeSize, {
              'data-image-refresh': String(revision)
            }));
          }
        });
        return DecorationSet.create(state.doc, decorations);
      }
    }
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
  const baseUrl = `${context.apiBaseUrl}/api/boards/${context.boardId}/${ownerPath}/attachments/image-content?fileName=${encodeURIComponent(fileName)}`;
  const version = context.refreshKey;
  return version === undefined
    ? baseUrl
    : `${baseUrl}&v=${encodeURIComponent(String(version))}`;
}

export function imageAltFromFileName(fileName: string): string {
  const extensionIndex = fileName.lastIndexOf('.');
  const withoutExtension = extensionIndex > 0 ? fileName.slice(0, extensionIndex) : fileName;
  return withoutExtension.replace(/[\\[\]\r\n]/g, ' ').replace(/\s+/g, ' ').trim();
}

export function isSupportedImageFileName(fileName: string): boolean {
  return /\.(?:png|jpe?g|webp|gif)$/i.test(fileName);
}

export function resolveImageSource(value: unknown, context: MarkdownImageContext | null): MarkdownImageSource {
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

function createImageNodeView(
  node: ProseMirrorNode,
  getPos: () => number | undefined,
  getContext: () => MarkdownImageContext | null,
  options: MarkdownImageExtensionOptions
): NodeView {
  const dom = document.createElement('figure');
  dom.className = 'md-image-node';
  dom.contentEditable = 'false';
  let currentNode = node;
  let image: HTMLImageElement | null = null;

  if (options.onActivate) {
    dom.tabIndex = 0;
    dom.setAttribute('role', 'button');
    dom.classList.add('md-image-node--interactive');
    dom.addEventListener('click', event => {
      if (event.target instanceof HTMLAnchorElement) {
        return;
      }
      activateImage();
    });
    dom.addEventListener('keydown', event => {
      if (event.target !== dom) {
        return;
      }
      if (event.key !== 'Enter' && event.key !== ' ') {
        return;
      }
      event.preventDefault();
      activateImage();
    });
  }

  const imageActivation = (): MarkdownImageActivation | null => {
    const position = getPos();
    if (position === undefined) {
      return null;
    }
    const alt = typeof currentNode.attrs.alt === 'string' ? currentNode.attrs.alt : '';
    const reference = typeof currentNode.attrs.src === 'string' ? currentNode.attrs.src : '';
    return {
      alt,
      attachmentFileName: parseAttachmentImageReference(reference),
      position,
      source: resolveImageSource(reference, getContext())
    };
  };

  const activateImage = () => {
    const image = imageActivation();
    if (image && options.onActivate) {
      options.onActivate(image);
    }
  };

  const removeImage = () => {
    const image = imageActivation();
    if (image && options.onRemove) {
      options.onRemove(image);
    }
  };

  const render = () => {
    image = null;
    dom.replaceChildren();
    const alt = typeof currentNode.attrs.alt === 'string' ? currentNode.attrs.alt : '';
    const source = resolveImageSource(currentNode.attrs.src, getContext());
    if (options.onActivate) {
      const action = 'Enlarge image';
      dom.setAttribute('aria-label', alt ? `${action}: ${alt}` : action);
    }

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
        frame.classList.remove('md-image-node-frame--unavailable');
      });
      nextImage.addEventListener('error', () => {
        nextImage.hidden = true;
        placeholder.hidden = false;
        frame.classList.add('md-image-node-frame--unavailable');
      });
      image = nextImage;
      const frame = createImageFrame(alt, removeImage, options.onRemove, nextImage, placeholder);
      dom.append(frame);
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
        frame.classList.remove('md-image-node-frame--unavailable');
      });
      nextImage.addEventListener('error', () => {
        nextImage.hidden = true;
        fallback.hidden = false;
        frame.classList.add('md-image-node-frame--unavailable');
      });
      nextImage.src = source.url;
      image = nextImage;
      const frame = createImageFrame(alt, removeImage, options.onRemove, nextImage, fallback);
      dom.append(frame);
      return;
    }

    const placeholder = createPlaceholder(alt || 'Image', 'Image unavailable');
    const frame = createImageFrame(alt, removeImage, options.onRemove, placeholder);
    frame.classList.add('md-image-node-frame--unavailable');
    dom.append(frame);
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
      return event.target instanceof HTMLAnchorElement || Boolean(options.onActivate);
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

function createImageFrame(
  alt: string,
  onRemove: () => void,
  removeEnabled: MarkdownImageExtensionOptions['onRemove'],
  ...content: HTMLElement[]
): HTMLSpanElement {
  const frame = document.createElement('span');
  frame.className = 'md-image-node-frame';
  frame.append(...content);
  if (removeEnabled) {
    frame.append(createImageRemoveButton(alt, onRemove));
  }
  return frame;
}

function createImageRemoveButton(alt: string, onRemove: () => void): HTMLButtonElement {
  const button = document.createElement('button');
  button.type = 'button';
  button.className = 'md-image-node-remove';
  button.title = 'Remove image';
  button.setAttribute('aria-label', alt ? `Remove image: ${alt}` : 'Remove image');
  button.addEventListener('click', event => {
    event.preventDefault();
    event.stopPropagation();
    onRemove();
  });

  const icon = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
  icon.setAttribute('viewBox', '0 0 24 24');
  icon.setAttribute('width', '16');
  icon.setAttribute('height', '16');
  icon.setAttribute('fill', 'none');
  icon.setAttribute('stroke', 'currentColor');
  icon.setAttribute('stroke-width', '2');
  icon.setAttribute('stroke-linecap', 'round');
  icon.setAttribute('stroke-linejoin', 'round');
  icon.setAttribute('aria-hidden', 'true');
  const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
  path.setAttribute('d', 'M3 6h18M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2M10 11v6M14 11v6');
  icon.append(path);
  button.append(icon);
  return button;
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
