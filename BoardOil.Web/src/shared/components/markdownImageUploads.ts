import { Node } from '@tiptap/core';
import type { Node as ProseMirrorNode } from '@tiptap/pm/model';
import type { NodeView } from '@tiptap/pm/view';

export type MarkdownImageUploadStatus = 'uploading' | 'failed' | 'cancelled';

export type MarkdownImageUploadNodeActions = {
  cancel: (id: string) => void;
  dismiss: (id: string) => void;
  retry: (id: string) => void;
};

export function createMarkdownImageUploadExtension(actions: MarkdownImageUploadNodeActions) {
  return Node.create({
    name: 'imageUpload',
    group: 'block',
    atom: true,
    selectable: false,
    draggable: false,

    addAttributes() {
      return {
        id: { default: '' },
        fileName: { default: '' },
        progress: { default: 0 },
        status: { default: 'uploading' }
      };
    },

    renderHTML({ HTMLAttributes }) {
      return ['div', { ...HTMLAttributes, 'data-type': 'image-upload' }];
    },

    renderMarkdown() {
      return '';
    },

    addNodeView() {
      return ({ node }) => createUploadNodeView(node, actions);
    }
  });
}

function createUploadNodeView(node: ProseMirrorNode, actions: MarkdownImageUploadNodeActions): NodeView {
  const dom = document.createElement('div');
  dom.className = 'md-image-upload';
  dom.contentEditable = 'false';
  let currentNode = node;

  const render = () => {
    const id = attribute(currentNode, 'id');
    const fileName = attribute(currentNode, 'fileName');
    const status = uploadStatus(currentNode.attrs.status);
    const progress = progressValue(currentNode.attrs.progress);
    dom.replaceChildren();
    dom.setAttribute('role', 'status');

    const details = document.createElement('span');
    details.className = 'md-image-upload-details';
    const label = document.createElement('strong');
    label.textContent = fileName;
    const message = document.createElement('span');
    message.textContent = statusMessage(status, progress);
    details.append(label, message);
    dom.append(details);

    if (status === 'uploading') {
      const progressElement = document.createElement('progress');
      progressElement.max = 100;
      progressElement.value = progress;
      progressElement.setAttribute('aria-label', `Uploading ${fileName}`);
      dom.append(progressElement, actionButton('Cancel', () => actions.cancel(id)));
      return;
    }

    const actionGroup = document.createElement('span');
    actionGroup.className = 'md-image-upload-actions';
    actionGroup.append(
      actionButton('Retry', () => actions.retry(id)),
      actionButton('Dismiss', () => actions.dismiss(id))
    );
    dom.append(actionGroup);
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
    stopEvent(event) {
      return event.target instanceof HTMLButtonElement;
    },
    ignoreMutation() {
      return true;
    }
  };
}

function actionButton(label: string, action: () => void): HTMLButtonElement {
  const button = document.createElement('button');
  button.type = 'button';
  button.className = 'btn btn--secondary md-image-upload-action';
  button.textContent = label;
  button.addEventListener('click', event => {
    event.preventDefault();
    event.stopPropagation();
    action();
  });
  return button;
}

function attribute(node: ProseMirrorNode, name: string): string {
  return typeof node.attrs[name] === 'string' ? node.attrs[name] : '';
}

function progressValue(value: unknown): number {
  if (typeof value !== 'number' || !Number.isFinite(value)) {
    return 0;
  }
  return Math.min(100, Math.max(0, Math.round(value)));
}

function uploadStatus(value: unknown): MarkdownImageUploadStatus {
  if (value === 'failed' || value === 'cancelled') {
    return value;
  }
  return 'uploading';
}

function statusMessage(status: MarkdownImageUploadStatus, progress: number): string {
  if (status === 'failed') {
    return 'Upload failed.';
  }
  if (status === 'cancelled') {
    return 'Upload cancelled.';
  }
  return progress >= 100 ? 'Finishing…' : `Uploading… ${progress}%`;
}
