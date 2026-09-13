<template>
  <div
    class="md-editor"
    role="group"
    :aria-label="`${props.ariaLabel} editor`"
    :style="{ '--md-editor-min-height': minHeight }"
    @dragover="imageDragOver"
    @drop="imageDropFallback"
  >
    <MdEditorToolbar
      v-if="showToolbar"
      :state="toolbarState"
      :is-plain-text-mode="isPlainTextMode"
      :show-image-action="Boolean(props.imageUpload)"
      :image-action-disabled="imageUploadRunning"
      @action="onToolbarAction"
      @image="selectImage"
      @toggle-plain-text-mode="togglePlainTextMode"
    />

    <input
      v-if="props.imageUpload"
      ref="imagePickerRef"
      type="file"
      multiple
      accept=".png,.jpg,.jpeg,.webp,.gif,image/png,image/jpeg,image/webp,image/gif"
      hidden
      :aria-label="`Choose an image for ${props.ariaLabel}`"
      @change="imageFileSelected"
    />

    <div class="md-editor-input">
      <textarea
        v-if="isPlainTextMode"
        ref="plainTextAreaRef"
        class="md-editor-textarea"
        :value="plainTextDraft"
        :aria-label="`${props.ariaLabel} markdown`"
        spellcheck="false"
        @focus="emit('focus')"
        @blur="emit('blur')"
        @input="plainTextInput"
        @paste="plainTextPaste"
        @drop="plainTextDrop"
        @dragover="plainTextDragOver"
        @keydown.esc.prevent="emit('escape')"
      />
      <EditorContent v-else-if="tiptapEditor" :editor="tiptapEditor" class="md-editor-content" />
    </div>

    <small v-if="imageUploadMessage" class="md-editor-image-message" role="status">{{ imageUploadMessage }}</small>

    <div v-if="plainFailedUploads.length > 0" class="md-editor-image-failures" aria-label="Image upload actions">
      <span v-for="task in plainFailedUploads" :key="task.id" class="md-editor-image-failure">
        <span>{{ task.file.name }}: {{ task.status === 'cancelled' ? 'Upload cancelled.' : 'Upload failed.' }}</span>
        <span class="md-editor-image-failure-actions">
          <button type="button" class="btn btn--secondary" @click="retryImageUpload(task.id)">Retry</button>
          <button type="button" class="btn btn--secondary" @click="dismissImageUpload(task.id)">Dismiss</button>
        </span>
      </span>
    </div>

    <MdLinkDialog
      :open="isLinkDialogOpen"
      :initial-text="linkDraftText"
      :initial-url="linkDraftUrl"
      :can-remove="linkDialogCanRemove"
      @cancel="closeLinkDialog"
      @save="saveLinkDialog"
      @remove="removeLinkFromDialog"
    />

    <MdImageDialog
      :open="activeImage !== null"
      :source-url="activeImage?.sourceUrl ?? null"
      :alt="activeImage?.alt ?? ''"
      editable
      @close="closeImageDialog"
      @save="saveImageAlt"
    />
  </div>
</template>

<script setup lang="ts">
import type { Editor as TiptapEditor } from '@tiptap/core';
import { FileHandler } from '@tiptap/extension-file-handler';
import { TaskItem } from '@tiptap/extension-list/task-item';
import { TaskList } from '@tiptap/extension-list/task-list';
import Link from '@tiptap/extension-link';
import { Markdown } from '@tiptap/markdown';
import StarterKit from '@tiptap/starter-kit';
import { EditorContent, useEditor } from '@tiptap/vue-3';
import { computed, nextTick, ref, watch } from 'vue';
import MdImageDialog from './MdImageDialog.vue';
import MdLinkDialog from './MdLinkDialog.vue';
import MdEditorToolbar from './MdEditorToolbar.vue';
import { mdEditorToolbarActions, type MdEditorToolbarActionEvent, type MdEditorToolbarActionId, type MdEditorToolbarActionState } from './mdEditorToolbarActions';
import { runMdEditorToolbarAction } from './mdEditorController';
import { syncPlainTextAreaHeight } from './mdEditorPlainTextSizing';
import { isHttpOrHttpsUrl } from '../utils/linkUrl';
import { normaliseMarkdown as normaliseMarkdownValue } from '../utils/markdown';
import {
  boardOilAttachmentImageDragType,
  buildAttachmentImageReference,
  createMarkdownImageExtension,
  imageAltFromFileName,
  isSupportedImageFileName,
  parseAttachmentImageReference,
  refreshMarkdownImages,
  type MarkdownImageActivation,
  type MarkdownImageContext,
  type MarkdownImageRemove,
  type MarkdownImageUpload
} from './markdownImages';
import {
  createMarkdownImageUploadExtension,
  type MarkdownImageUploadStatus
} from './markdownImageUploads';

const props = withDefaults(defineProps<{
  modelValue: string;
  ariaLabel?: string;
  maxLength?: number;
  minHeight?: string;
  showToolbar?: boolean;
  imageContext?: MarkdownImageContext | null;
  imageRemove?: MarkdownImageRemove | null;
  imageUpload?: MarkdownImageUpload | null;
  imageUploadCancel?: (() => void) | null;
}>(), {
  ariaLabel: 'Markdown editor',
  maxLength: 20_000,
  minHeight: '12rem',
  showToolbar: true
});

const emit = defineEmits<{
  'update:modelValue': [value: string];
  'user-edit': [value: string];
  focus: [];
  blur: [];
  escape: [];
  'toolbar-state-change': [value: Partial<Record<MdEditorToolbarActionId, MdEditorToolbarActionState>>];
  'plain-text-mode-change': [value: boolean];
  'image-upload-blocking-change': [value: boolean];
}>();

type ImageUploadTask = {
  id: string;
  file: File;
  mode: 'rich' | 'plain';
  status: MarkdownImageUploadStatus;
  progress: number;
  attempt: number;
  plainMarker: string | null;
  reuseExisting: boolean;
};

const normalisedModelValue = computed(() => normaliseMarkdown(props.modelValue ?? ''));
const isPlainTextMode = ref(false);
const plainTextDraft = ref(normalisedModelValue.value);
const plainTextAreaRef = ref<HTMLTextAreaElement | null>(null);
const imagePickerRef = ref<HTMLInputElement | null>(null);
const imageUploadMessage = ref('');
const imageUploadTasks = ref<ImageUploadTask[]>([]);
const activeImage = ref<{ position: number; sourceUrl: string | null; alt: string } | null>(null);
const isLinkDialogOpen = ref(false);
const linkDraftText = ref('');
const linkDraftUrl = ref('');
const linkDialogCanRemove = ref(false);
const linkSelectionRange = ref<{ from: number; to: number } | null>(null);
const linkOpenModifier = /^(Mac|iPhone|iPad|iPod)/i.test(navigator.platform) ? 'Cmd' : 'Ctrl';
const linkTooltip = `${linkOpenModifier}-click to open. Use the Link button to edit.`;
let imageUploadSequence = 0;

const imageUploadRunning = computed(() => imageUploadTasks.value.some(task => task.status === 'uploading'));
const plainFailedUploads = computed(() => imageUploadTasks.value.filter(task =>
  task.mode === 'plain' && task.status !== 'uploading'));

const tiptapEditor = useEditor({
  content: '',
  contentType: 'markdown',
  extensions: [
    StarterKit.configure({
      link: false
    }),
    TaskList,
    TaskItem.configure({
      nested: true
    }),
    Link.configure({
      openOnClick: false,
      enableClickSelection: true,
      autolink: true,
      linkOnPaste: true,
      defaultProtocol: 'https',
      isAllowedUri: url => isHttpOrHttpsUrl(url),
      HTMLAttributes: {
        'data-link-tooltip': linkTooltip,
        'aria-description': linkTooltip,
        target: null,
        rel: null
      }
    }),
    ...(props.imageContext ? [
      createMarkdownImageExtension(() => props.imageContext ?? null, {
        editable: true,
        onActivate: openImageDialog,
        onRemove: removeImage
      }),
      createMarkdownImageUploadExtension({
        cancel: cancelImageUpload,
        dismiss: dismissImageUpload,
        retry: retryImageUpload
      }),
      FileHandler.configure({
        consumePasteEvent: true,
        onPaste: (editor, files) => queueRichImages(editor, files, editor.state.selection.from, false),
        onDrop: (editor, files, position) => queueRichImages(editor, files, position, true)
      })
    ] : []),
    Markdown
  ],
  editorProps: {
    attributes: {
      'aria-label': props.ariaLabel
    },
    handleDrop: (_view, event) => {
      const reference = draggedAttachmentReference(event);
      const externalImage = draggedExternalImage(event);
      const editor = tiptapEditor.value;
      if ((!reference && !externalImage) || !editor) {
        return false;
      }
      event.preventDefault();
      event.stopPropagation();
      const position = richDropPosition(editor, event);
      if (reference) {
        insertRichAttachmentReference(editor, reference, position);
      } else if (externalImage) {
        insertRichExternalImage(editor, externalImage, position);
      }
      return true;
    },
    handleKeyDown: (_view, event) => {
      if (event.key !== 'Escape') {
        return false;
      }

      emit('escape');
      return true;
    },
    handleClick: (_view, _pos, event) => {
      const mouseEvent = event as MouseEvent;
      if (mouseEvent.button !== 0 || (!mouseEvent.metaKey && !mouseEvent.ctrlKey)) {
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
  },
  onFocus: () => {
    emit('focus');
  },
  onBlur: () => {
    emit('blur');
  },
  onUpdate: ({ editor }) => {
    reconcileResolvedUploadTasks(editor);
    const currentValue = editor.getMarkdown();
    const nextValue = normaliseMarkdown(currentValue);
    if (nextValue !== currentValue) {
      setEditorContent(nextValue);
    }

    if (nextValue === normalisedModelValue.value) {
      return;
    }

    emit('update:modelValue', nextValue);
  }
});

const toolbarState = computed<Partial<Record<MdEditorToolbarActionId, MdEditorToolbarActionState>>>(() => {
  const editor = tiptapEditor.value;
  const state: Partial<Record<MdEditorToolbarActionId, MdEditorToolbarActionState>> = {};

  for (const action of mdEditorToolbarActions) {
    const defaultActionEvent: MdEditorToolbarActionEvent = action.id === 'heading'
      ? { id: action.id, headingLevel: 1 }
      : { id: action.id };

    state[action.id] = {
      disabled: !editor || !action.canRun(editor, defaultActionEvent),
      isActive: editor ? (action.isActive?.(editor, defaultActionEvent) ?? false) : false
    };
  }

  return state;
});

watch(toolbarState, value => {
  emit('toolbar-state-change', value);
}, { immediate: true });

watch(isPlainTextMode, value => {
  emit('plain-text-mode-change', value);
}, { immediate: true });

watch(
  () => imageUploadTasks.value.length > 0,
  value => emit('image-upload-blocking-change', value),
  { immediate: true }
);

function onToolbarAction(actionEvent: MdEditorToolbarActionEvent) {
  runMdEditorToolbarAction(actionEvent, tiptapEditor.value ?? null, isPlainTextMode.value, openLinkDialog);
}

function runToolbarAction(actionEvent: MdEditorToolbarActionEvent) {
  onToolbarAction(actionEvent);
}

function togglePlainTextMode() {
  if (imageUploadTasks.value.length > 0) {
    imageUploadMessage.value = 'Finish, retry or dismiss image uploads before switching editor mode.';
    return;
  }
  if (!isPlainTextMode.value) {
    closeLinkDialog();
    const editor = tiptapEditor.value;
    plainTextDraft.value = normaliseMarkdown(editor ? editor.getMarkdown() : normalisedModelValue.value);
    isPlainTextMode.value = true;
    void nextTick(() => {
      syncPlainTextAreaHeight(plainTextAreaRef.value);
    });
    return;
  }

  isPlainTextMode.value = false;
  const nextValue = normaliseMarkdown(plainTextDraft.value);
  plainTextDraft.value = nextValue;
  setEditorContent(nextValue);

  if (nextValue === normalisedModelValue.value) {
    return;
  }

  emit('update:modelValue', nextValue);
}

function onPlainTextInput(value: string) {
  const nextValue = normaliseMarkdown(value);
  plainTextDraft.value = nextValue;
  syncPlainTextAreaHeight(plainTextAreaRef.value);

  if (nextValue === normalisedModelValue.value) {
    return;
  }

  emit('update:modelValue', nextValue);
}

function plainTextInput(event: Event) {
  onPlainTextInput((event.target as HTMLTextAreaElement).value);
}

function selectImage() {
  if (!props.imageUpload || imageUploadRunning.value) {
    return;
  }
  imageUploadMessage.value = '';
  imagePickerRef.value?.click();
}

async function imageFileSelected(event: Event) {
  const input = event.target as HTMLInputElement;
  const files = Array.from(input.files ?? []);
  input.value = '';
  if (files.length === 0 || !props.imageUpload || imageUploadRunning.value) {
    return;
  }
  if (isPlainTextMode.value) {
    queuePlainImages(files, plainTextAreaRef.value?.selectionStart ?? plainTextDraft.value.length, true);
    return;
  }
  const editor = tiptapEditor.value;
  if (editor) {
    queueRichImages(editor, files, editor.state.selection.from, true);
  }
}

function plainTextPaste(event: ClipboardEvent) {
  const files = Array.from(event.clipboardData?.files ?? []);
  if (!hasSupportedImage(files)) {
    return;
  }
  event.preventDefault();
  queuePlainImages(files, plainTextAreaRef.value?.selectionStart ?? plainTextDraft.value.length, false);
}

function plainTextDrop(event: DragEvent) {
  const attachmentReference = draggedAttachmentReference(event);
  if (attachmentReference) {
    event.preventDefault();
    insertPlainAttachmentReference(attachmentReference, plainTextAreaRef.value?.selectionStart ?? plainTextDraft.value.length);
    return;
  }
  const externalImage = draggedExternalImage(event);
  if (externalImage) {
    event.preventDefault();
    insertPlainExternalImage(externalImage, plainTextAreaRef.value?.selectionStart ?? plainTextDraft.value.length);
    return;
  }
  const files = Array.from(event.dataTransfer?.files ?? []);
  if (files.length === 0) {
    return;
  }
  event.preventDefault();
  queuePlainImages(files, plainTextAreaRef.value?.selectionStart ?? plainTextDraft.value.length, true);
}

function plainTextDragOver(event: DragEvent) {
  const files = Array.from(event.dataTransfer?.files ?? []);
  if (hasSupportedImage(files) || event.dataTransfer?.types.includes('Files')) {
    event.preventDefault();
  }
}

function imageDragOver(event: DragEvent) {
  const transfer = event.dataTransfer;
  if (!transfer || !hasImageDragType(transfer)) {
    return;
  }
  event.preventDefault();
  transfer.dropEffect = 'copy';
}

function imageDropFallback(event: DragEvent) {
  if (event.defaultPrevented) {
    return;
  }
  const attachmentReference = draggedAttachmentReference(event);
  const externalImage = draggedExternalImage(event);
  const files = Array.from(event.dataTransfer?.files ?? []);
  const hasExternalDragData = hasExternalDragType(event.dataTransfer);
  if (!attachmentReference && !externalImage && files.length === 0 && !hasExternalDragData) {
    return;
  }
  event.preventDefault();
  event.stopPropagation();
  const plainPosition = plainTextAreaRef.value?.selectionStart ?? plainTextDraft.value.length;
  if (attachmentReference) {
    if (isPlainTextMode.value) {
      insertPlainAttachmentReference(attachmentReference, plainPosition);
      return;
    }
    const editor = tiptapEditor.value;
    if (editor) {
      insertRichAttachmentReference(editor, attachmentReference, richDropPosition(editor, event));
    }
    return;
  }
  if (externalImage) {
    if (isPlainTextMode.value) {
      insertPlainExternalImage(externalImage, plainPosition);
      return;
    }
    const editor = tiptapEditor.value;
    if (editor) {
      insertRichExternalImage(editor, externalImage, richDropPosition(editor, event));
    }
    return;
  }
  if (files.length === 0) {
    imageUploadMessage.value = 'The dropped browser content did not contain an image.';
    return;
  }
  if (isPlainTextMode.value) {
    queuePlainImages(files, plainPosition, true);
    return;
  }
  const editor = tiptapEditor.value;
  if (editor) {
    queueRichImages(editor, files, richDropPosition(editor, event), true);
  }
}

function queueRichImages(editor: TiptapEditor, files: File[], position: number, reuseExisting: boolean) {
  if (imageUploadRunning.value) {
    imageUploadMessage.value = 'Wait for the current image upload to finish before adding more.';
    return;
  }
  const tasks = createUploadTasks(files, 'rich', reuseExisting);
  if (tasks.length === 0) {
    return;
  }
  const inserted = editor.chain().focus().insertContentAt(position, tasks.map(task => ({
    type: 'imageUpload',
    attrs: uploadNodeAttributes(task)
  }))).run();
  if (!inserted) {
    removeTasks(tasks);
    imageUploadMessage.value = 'The images could not be inserted at that position.';
    return;
  }
  void uploadImageTasks(tasks);
}

function queuePlainImages(files: File[], position: number, reuseExisting: boolean) {
  if (imageUploadRunning.value) {
    imageUploadMessage.value = 'Wait for the current image upload to finish before adding more.';
    return;
  }
  const tasks = createUploadTasks(files, 'plain', reuseExisting);
  if (tasks.length === 0) {
    return;
  }
  const markers = tasks.map(task => task.plainMarker!).join('\n\n');
  const before = plainTextDraft.value.slice(0, position);
  const after = plainTextDraft.value.slice(position);
  const prefix = before.length > 0 && !before.endsWith('\n') ? '\n\n' : '';
  const suffix = after.length > 0 && !after.startsWith('\n') ? '\n\n' : '';
  const inserted = `${prefix}${markers}${suffix}`;
  onPlainTextInput(`${before}${inserted}${after}`);
  void nextTick(() => {
    const caret = position + inserted.length;
    plainTextAreaRef.value?.setSelectionRange(caret, caret);
  });
  void uploadImageTasks(tasks);
}

function draggedAttachmentReference(event: DragEvent): string | null {
  const reference = event.dataTransfer?.getData(boardOilAttachmentImageDragType) ?? '';
  return parseAttachmentImageReference(reference) ? reference : null;
}

type DraggedExternalImage = { url: string; alt: string };

function hasImageDragType(transfer: DataTransfer): boolean {
  return transfer.types.includes('Files')
    || transfer.types.includes(boardOilAttachmentImageDragType)
    || hasExternalDragType(transfer);
}

function hasExternalDragType(transfer: DataTransfer | null): boolean {
  return Boolean(transfer?.types.includes('text/html') || transfer?.types.includes('text/uri-list'));
}

function draggedExternalImage(event: DragEvent): DraggedExternalImage | null {
  const transfer = event.dataTransfer;
  if (!transfer) {
    return null;
  }
  const html = transfer.getData('text/html');
  if (html) {
    const document = new DOMParser().parseFromString(html, 'text/html');
    const image = document.querySelector('img[src]');
    const url = externalImageUrl(image?.getAttribute('src') ?? '');
    if (url) {
      return { url, alt: image?.getAttribute('alt')?.trim() ?? '' };
    }
  }
  const uri = transfer.getData('text/uri-list')
    .split(/\r?\n/)
    .find(value => value.length > 0 && !value.startsWith('#')) ?? '';
  const url = externalImageUrl(uri);
  if (!url || !isSupportedImageFileName(new URL(url).pathname)) {
    return null;
  }
  return { url, alt: externalImageAlt(url) };
}

function externalImageUrl(value: string): string | null {
  if (!isHttpOrHttpsUrl(value)) {
    return null;
  }
  return new URL(value).toString();
}

function externalImageAlt(url: string): string {
  const pathSegments = new URL(url).pathname.split('/').filter(Boolean);
  const pathSegment = pathSegments[pathSegments.length - 1] ?? '';
  try {
    return imageAltFromFileName(decodeURIComponent(pathSegment));
  } catch {
    return imageAltFromFileName(pathSegment);
  }
}

function richDropPosition(editor: TiptapEditor, event: DragEvent): number {
  return editor.view.posAtCoords({ left: event.clientX, top: event.clientY })?.pos
    ?? editor.state.selection.from;
}

function insertRichAttachmentReference(editor: TiptapEditor, reference: string, position: number) {
  const fileName = parseAttachmentImageReference(reference);
  if (!fileName) {
    return;
  }
  const markdown = imageMarkdown(fileName);
  if (editor.getMarkdown().length + markdown.length + 2 > props.maxLength) {
    imageUploadMessage.value = 'The image could not be inserted because the description is at its length limit.';
    return;
  }
  const inserted = editor.chain().focus().insertContentAt(position, {
    type: 'image',
    attrs: {
      src: reference,
      alt: imageAltFromFileName(fileName)
    }
  }).run();
  if (inserted) {
    emit('user-edit', normaliseMarkdown(editor.getMarkdown()));
  }
  imageUploadMessage.value = inserted ? `${fileName} inserted.` : 'The image could not be inserted at that position.';
}

function insertRichExternalImage(editor: TiptapEditor, image: DraggedExternalImage, position: number) {
  const markdown = externalImageMarkdown(image);
  if (editor.getMarkdown().length + markdown.length + 2 > props.maxLength) {
    imageUploadMessage.value = 'The image could not be inserted because the description is at its length limit.';
    return;
  }
  const inserted = editor.chain().focus().insertContentAt(position, {
    type: 'image',
    attrs: { src: image.url, alt: image.alt }
  }).run();
  if (inserted) {
    emit('user-edit', normaliseMarkdown(editor.getMarkdown()));
  }
  imageUploadMessage.value = inserted ? 'External image inserted.' : 'The image could not be inserted at that position.';
}

function insertPlainAttachmentReference(reference: string, position: number) {
  const fileName = parseAttachmentImageReference(reference);
  if (!fileName) {
    return;
  }
  insertPlainImageMarkdown(imageMarkdown(fileName), position, `${fileName} inserted.`);
}

function insertPlainExternalImage(image: DraggedExternalImage, position: number) {
  insertPlainImageMarkdown(externalImageMarkdown(image), position, 'External image inserted.');
}

function externalImageMarkdown(image: DraggedExternalImage): string {
  const alt = image.alt.replace(/[\\[\]\r\n]/g, ' ').replace(/\s+/g, ' ').trim();
  return `![${alt}](${image.url})`;
}

function insertPlainImageMarkdown(markdown: string, position: number, successMessage: string) {
  const before = plainTextDraft.value.slice(0, position);
  const after = plainTextDraft.value.slice(position);
  const prefix = before.length > 0 && !before.endsWith('\n') ? '\n\n' : '';
  const suffix = after.length > 0 && !after.startsWith('\n') ? '\n\n' : '';
  const inserted = `${prefix}${markdown}${suffix}`;
  const nextValue = `${before}${inserted}${after}`;
  if (nextValue.length > props.maxLength) {
    imageUploadMessage.value = 'The image could not be inserted because the description is at its length limit.';
    return;
  }
  onPlainTextInput(nextValue);
  emit('user-edit', normaliseMarkdown(nextValue));
  imageUploadMessage.value = successMessage;
  void nextTick(() => {
    const caret = position + inserted.length;
    plainTextAreaRef.value?.setSelectionRange(caret, caret);
  });
}

function createUploadTasks(
  files: File[],
  mode: ImageUploadTask['mode'],
  reuseExisting: boolean
): ImageUploadTask[] {
  const accepted = files.filter(isSupportedImageFile);
  if (accepted.length !== files.length) {
    imageUploadMessage.value = accepted.length > 0
      ? 'Unsupported files were skipped. Choose PNG, JPEG, WebP or GIF images.'
      : 'Choose PNG, JPEG, WebP or GIF images.';
  }
  const tasks = accepted.map(file => {
    const id = `image-upload-${Date.now()}-${++imageUploadSequence}`;
    const task: ImageUploadTask = {
      id,
      file,
      mode,
      status: 'uploading',
      progress: 0,
      attempt: 0,
      plainMarker: null,
      reuseExisting
    };
    if (mode === 'plain') {
      task.plainMarker = plainUploadMarker(task);
    }
    return task;
  });
  imageUploadTasks.value.push(...tasks);
  return tasks;
}

async function uploadImageTasks(tasks: ImageUploadTask[]) {
  const upload = props.imageUpload;
  if (!upload || tasks.length === 0) {
    failTasks(tasks);
    return;
  }

  const attempts = new Map<string, number>();
  for (const task of tasks) {
    task.attempt += 1;
    task.status = 'uploading';
    task.progress = 0;
    attempts.set(task.id, task.attempt);
    updateUploadPlaceholder(task);
  }
  imageUploadMessage.value = tasks.length === 1
    ? `Uploading ${tasks[0]!.file.name}…`
    : `Uploading ${tasks.length} images…`;

  const insertedFileNames: string[] = [];
  try {
    const results = await upload(tasks.map(task => task.file), (file, percent) => {
      for (const task of tasks) {
        if (task.file === file && task.status === 'uploading') {
          task.progress = percent;
          updateRichUploadNode(task);
        }
      }
    }, { reuseExisting: tasks.every(task => task.reuseExisting) });
    tasks.forEach((task, index) => {
      if (!isCurrentAttempt(task, attempts)) {
        return;
      }
      const result = results[index] ?? null;
      if (!result) {
        failTask(task);
        return;
      }
      if (completeTask(task, result.fileName)) {
        insertedFileNames.push(result.fileName);
      }
    });
  } catch {
    for (const task of tasks) {
      if (isCurrentAttempt(task, attempts)) {
        failTask(task);
      }
    }
  }

  const failedCount = tasks.filter(task => task.status === 'failed').length;
  if (failedCount > 0) {
    imageUploadMessage.value = `${failedCount} image upload${failedCount === 1 ? '' : 's'} failed. Retry or dismiss below.`;
    return;
  }
  const insertedCount = insertedFileNames.length;
  if (insertedCount > 0) {
    imageUploadMessage.value = insertedCount === 1
      ? `${insertedFileNames[0]} inserted.`
      : `${insertedCount} images inserted.`;
  }
}

function completeTask(task: ImageUploadTask, fileName: string): boolean {
  const markdown = imageMarkdown(fileName);
  const inserted = task.mode === 'rich'
    ? replaceRichUploadWithImage(task, fileName, markdown)
    : replacePlainUploadWithImage(task, markdown);
  removeTask(task);
  if (!inserted) {
    if (task.mode === 'rich') {
      removeRichUploadNode(task);
    } else if (task.plainMarker) {
      onPlainTextInput(plainTextDraft.value.replace(task.plainMarker, ''));
    }
    imageUploadMessage.value = `${fileName} was uploaded as an attachment, but its insertion was removed or the description is at its length limit.`;
  } else {
    emitCompletedImageUserEdit(task.mode);
  }
  return inserted;
}

function emitCompletedImageUserEdit(mode: ImageUploadTask['mode']) {
  if (mode === 'plain') {
    emit('user-edit', normaliseMarkdown(plainTextDraft.value));
    return;
  }

  const editor = tiptapEditor.value;
  if (editor) {
    emit('user-edit', normaliseMarkdown(editor.getMarkdown()));
  }
}

function replaceRichUploadWithImage(task: ImageUploadTask, fileName: string, markdown: string): boolean {
  const editor = tiptapEditor.value;
  if (!editor || editor.getMarkdown().length + markdown.length + 2 > props.maxLength) {
    return false;
  }
  const position = findUploadNodePosition(editor, task.id);
  const imageType = editor.schema.nodes.image;
  if (position === null || !imageType) {
    return false;
  }
  const transaction = editor.state.tr.replaceWith(position, position + 1, imageType.create({
    src: buildAttachmentImageReference(fileName),
    alt: imageAltFromFileName(fileName)
  }));
  transaction.setMeta('addToHistory', false);
  editor.view.dispatch(transaction);
  return true;
}

function replacePlainUploadWithImage(task: ImageUploadTask, markdown: string): boolean {
  const marker = task.plainMarker;
  if (!marker || !plainTextDraft.value.includes(marker)) {
    return false;
  }
  const nextValue = plainTextDraft.value.replace(marker, markdown);
  if (nextValue.length > props.maxLength) {
    plainTextDraft.value = plainTextDraft.value.replace(marker, '');
    onPlainTextInput(plainTextDraft.value);
    return false;
  }
  onPlainTextInput(nextValue);
  return true;
}

function failTasks(tasks: ImageUploadTask[]) {
  for (const task of tasks) {
    failTask(task);
  }
}

function failTask(task: ImageUploadTask) {
  task.status = 'failed';
  updateUploadPlaceholder(task);
}

function retryImageUpload(id: string) {
  const task = imageUploadTasks.value.find(candidate => candidate.id === id);
  if (!task || imageUploadRunning.value) {
    return;
  }
  void uploadImageTasks([task]);
}

function cancelImageUpload(_id: string) {
  props.imageUploadCancel?.();
  const cancelled = imageUploadTasks.value.filter(task => task.status === 'uploading');
  for (const task of cancelled) {
    task.attempt += 1;
    task.status = 'cancelled';
    updateUploadPlaceholder(task);
  }
  imageUploadMessage.value = 'Image upload cancelled. Retry or dismiss the placeholder.';
}

function dismissImageUpload(id: string) {
  const task = imageUploadTasks.value.find(candidate => candidate.id === id);
  if (!task || task.status === 'uploading') {
    return;
  }
  if (task.mode === 'rich') {
    removeRichUploadNode(task);
  } else if (task.plainMarker) {
    onPlainTextInput(plainTextDraft.value.replace(task.plainMarker, ''));
  }
  removeTask(task);
}

function updateUploadPlaceholder(task: ImageUploadTask) {
  if (task.mode === 'rich') {
    updateRichUploadNode(task);
    return;
  }
  const previousMarker = task.plainMarker;
  const nextMarker = plainUploadMarker(task);
  task.plainMarker = nextMarker;
  if (previousMarker && plainTextDraft.value.includes(previousMarker)) {
    onPlainTextInput(plainTextDraft.value.replace(previousMarker, nextMarker));
  }
}

function updateRichUploadNode(task: ImageUploadTask) {
  const editor = tiptapEditor.value;
  if (!editor) {
    return;
  }
  const position = findUploadNodePosition(editor, task.id);
  if (position === null) {
    return;
  }
  const node = editor.state.doc.nodeAt(position);
  if (!node) {
    return;
  }
  const transaction = editor.state.tr.setNodeMarkup(position, undefined, uploadNodeAttributes(task), node.marks);
  transaction.setMeta('addToHistory', false);
  editor.view.dispatch(transaction);
}

function removeRichUploadNode(task: ImageUploadTask) {
  const editor = tiptapEditor.value;
  if (!editor) {
    return;
  }
  const position = findUploadNodePosition(editor, task.id);
  if (position !== null) {
    const transaction = editor.state.tr.delete(position, position + 1);
    transaction.setMeta('addToHistory', false);
    editor.view.dispatch(transaction);
  }
}

function findUploadNodePosition(editor: TiptapEditor, id: string): number | null {
  let match: number | null = null;
  editor.state.doc.descendants((node, position) => {
    if (node.type.name === 'imageUpload' && node.attrs.id === id) {
      match = position;
      return false;
    }
    return true;
  });
  return match;
}

function uploadNodeAttributes(task: ImageUploadTask) {
  return {
    id: task.id,
    fileName: task.file.name,
    progress: task.progress,
    status: task.status
  };
}

function plainUploadMarker(task: ImageUploadTask): string {
  const safeName = task.file.name.replace(/--/g, '—').replace(/[\r\n]/g, ' ');
  let status = 'Uploading';
  if (task.status === 'failed') {
    status = 'Upload failed';
  } else if (task.status === 'cancelled') {
    status = 'Upload cancelled';
  }
  return `<!-- boardoil-image-upload:${task.id} ${status}: ${safeName} -->`;
}

function imageMarkdown(fileName: string): string {
  return `![${imageAltFromFileName(fileName)}](${buildAttachmentImageReference(fileName)})`;
}

function isCurrentAttempt(task: ImageUploadTask, attempts: Map<string, number>): boolean {
  const currentTask = imageUploadTasks.value.find(candidate => candidate.id === task.id);
  return currentTask?.attempt === attempts.get(task.id);
}

function removeTasks(tasks: ImageUploadTask[]) {
  const taskIds = new Set(tasks.map(task => task.id));
  imageUploadTasks.value = imageUploadTasks.value.filter(task => !taskIds.has(task.id));
}

function removeTask(task: ImageUploadTask) {
  removeTasks([task]);
}

function reconcileResolvedUploadTasks(editor: TiptapEditor) {
  const richUploadIds = new Set<string>();
  editor.state.doc.descendants(node => {
    if (node.type.name === 'imageUpload' && typeof node.attrs.id === 'string') {
      richUploadIds.add(node.attrs.id);
    }
  });
  imageUploadTasks.value = imageUploadTasks.value.filter(task => {
    if (task.status === 'uploading') {
      return true;
    }
    if (task.mode === 'rich') {
      return richUploadIds.has(task.id);
    }
    return Boolean(task.plainMarker && plainTextDraft.value.includes(task.plainMarker));
  });
}

function hasSupportedImage(files: File[]): boolean {
  return files.some(isSupportedImageFile);
}

function isSupportedImageFile(file: File): boolean {
  if (['image/png', 'image/jpeg', 'image/webp', 'image/gif'].includes(file.type.toLowerCase())) {
    return true;
  }
  return isSupportedImageFileName(file.name);
}

function openImageDialog(image: MarkdownImageActivation) {
  let sourceUrl: string | null = null;
  if (image.source.kind !== 'unavailable') {
    sourceUrl = image.source.url;
  }
  activeImage.value = { position: image.position, sourceUrl, alt: image.alt };
}

function closeImageDialog() {
  activeImage.value = null;
}

async function removeImage(image: MarkdownImageActivation) {
  const editor = tiptapEditor.value;
  if (!editor || !props.imageRemove || !await props.imageRemove(image)) {
    return;
  }
  const node = editor.state.doc.nodeAt(image.position);
  if (node?.type.name !== 'image') {
    return;
  }
  const removed = editor.commands.command(({ tr, dispatch }) => {
    if (dispatch) {
      dispatch(tr.delete(image.position, image.position + node.nodeSize));
    }
    return true;
  });
  if (removed) {
    emit('user-edit', normaliseMarkdown(editor.getMarkdown()));
  }
}

function saveImageAlt(alt: string) {
  const editor = tiptapEditor.value;
  const image = activeImage.value;
  if (!editor || !image) {
    closeImageDialog();
    return;
  }
  const node = editor.state.doc.nodeAt(image.position);
  if (node?.type.name === 'image') {
    editor.commands.command(({ tr, dispatch }) => {
      if (dispatch) {
        dispatch(tr.setNodeMarkup(image.position, undefined, { ...node.attrs, alt }, node.marks));
      }
      return true;
    });
  }
  closeImageDialog();
}

defineExpose({
  runToolbarAction,
  selectImage,
  togglePlainTextMode
});

function openLinkDialog(editor: TiptapEditor) {
  editor.chain().focus().run();
  if (editor.isActive('link')) {
    editor.chain().focus().extendMarkRange('link').run();
  }

  const from = editor.state.selection.from;
  const to = editor.state.selection.to;
  const selectedText = from === to ? '' : editor.state.doc.textBetween(from, to, ' ', ' ');
  const currentUrl = (editor.getAttributes('link').href as string | undefined) ?? '';

  linkSelectionRange.value = { from, to };
  linkDraftText.value = selectedText.length > 0 ? selectedText : currentUrl;
  linkDraftUrl.value = currentUrl;
  linkDialogCanRemove.value = editor.isActive('link');
  isLinkDialogOpen.value = true;
}

function closeLinkDialog() {
  isLinkDialogOpen.value = false;
  linkSelectionRange.value = null;
}

function saveLinkDialog(nextLink: { text: string; url: string }) {
  const editor = tiptapEditor.value;
  if (!editor || !linkSelectionRange.value) {
    closeLinkDialog();
    return;
  }

  const range = linkSelectionRange.value;
  const href = nextLink.url;
  const text = nextLink.text.trim().length > 0 ? nextLink.text : href;
  const from = range.from;
  const to = range.to;

  if (from === to) {
    editor.chain().focus().setTextSelection(from).insertContent(text).setTextSelection({
      from,
      to: from + text.length
    }).setLink({ href }).run();
    closeLinkDialog();
    return;
  }

  editor.chain().focus().setTextSelection({ from, to }).insertContent(text).setTextSelection({
    from,
    to: from + text.length
  }).setLink({ href }).run();
  closeLinkDialog();
}

function removeLinkFromDialog() {
  const editor = tiptapEditor.value;
  if (!editor || !linkSelectionRange.value) {
    closeLinkDialog();
    return;
  }

  const range = linkSelectionRange.value;
  if (range.from === range.to) {
    editor.chain().focus().setTextSelection(range.from).extendMarkRange('link').unsetLink().run();
    closeLinkDialog();
    return;
  }

  editor.chain().focus().setTextSelection({
    from: range.from,
    to: range.to
  }).extendMarkRange('link').unsetLink().run();
  closeLinkDialog();
}

function normaliseMarkdown(value: string) {
  return normaliseMarkdownValue(value, props.maxLength);
}

function setEditorContent(value: string) {
  const editor = tiptapEditor.value;
  if (!editor) {
    return;
  }

  const nextValue = normaliseMarkdown(value);
  const currentValue = normaliseMarkdown(editor.getMarkdown());
  if (currentValue === nextValue) {
    return;
  }

  editor.commands.setContent(nextValue, {
    contentType: 'markdown',
    emitUpdate: false
  });
}

watch(
  normalisedModelValue,
  nextValue => {
    if (isPlainTextMode.value) {
      if (plainTextDraft.value !== nextValue) {
        plainTextDraft.value = nextValue;
        void nextTick(() => {
          syncPlainTextAreaHeight(plainTextAreaRef.value);
        });
      }

      return;
    }

    setEditorContent(nextValue);
  },
  { immediate: true }
);

watch(
  isPlainTextMode,
  isEnabled => {
    if (!isEnabled) {
      return;
    }

    void nextTick(() => {
      syncPlainTextAreaHeight(plainTextAreaRef.value);
    });
  }
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
  () => props.imageContext?.refreshKey,
  () => refreshMarkdownImages(tiptapEditor.value ?? null)
);
</script>

<style scoped>
.md-editor {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  flex: 1 1 0;
  min-height: 0;
  overflow: hidden;
}

.md-editor-input {
  flex: 1 1 0;
  min-height: 0;
  display: flex;
  overflow: hidden;
}

.md-editor-content {
  flex: 1 1 0;
  min-height: 0;
  overflow: hidden;
}

.md-editor-content :deep(.tiptap) {
  height: 100%;
  min-height: var(--md-editor-min-height);
  max-height: 100%;
  border: 1px solid var(--bo-border-default);
  border-radius: 8px;
  padding: 0.5rem;
  white-space: pre-wrap;
  word-break: break-word;
  overflow-y: auto;
}

.md-editor-content :deep(.tiptap:focus) {
  outline: none;
  border-color: var(--bo-colour-secondary);
}

.md-editor-content :deep(.tiptap a[data-link-tooltip]) {
  position: relative;
}

.md-editor-content :deep(.tiptap a[data-link-tooltip]:is(:hover, :focus-visible)::after) {
  content: attr(data-link-tooltip);
  position: absolute;
  z-index: 3;
  top: calc(100% + 0.35rem);
  left: 0;
  width: max-content;
  max-width: min(22rem, calc(100vw - 2rem));
  border: 1px solid var(--bo-border-soft);
  border-radius: 6px;
  padding: 0.3rem 0.45rem;
  background: var(--bo-surface-base);
  box-shadow: var(--bo-shadow-pop);
  color: var(--bo-ink-default);
  font-size: 0.78rem;
  font-weight: 400;
  line-height: 1.25;
  pointer-events: none;
}

.md-editor-content :deep(.tiptap ul[data-type='taskList']) {
  list-style: none;
  margin: 0.45rem 0;
  padding-left: 0.2rem;
}

.md-editor-content :deep(.tiptap ul[data-type='taskList'] > li) {
  display: flex;
  align-items: flex-start;
  gap: 0.45rem;
  margin: 0.3rem 0;
}

.md-editor-content :deep(.tiptap ul[data-type='taskList'] > li > label) {
  display: inline-flex;
  align-items: center;
  flex: 0 0 auto;
  margin-top: 0.18rem;
}

.md-editor-content :deep(.tiptap ul[data-type='taskList'] > li > label > span) {
  display: none;
}

.md-editor-content :deep(.tiptap ul[data-type='taskList'] > li > label > input[type='checkbox']) {
  width: 1rem !important;
  height: 1rem !important;
  min-height: 0;
  margin: 0 !important;
  padding: 0 !important;
  flex: 0 0 auto;
}

.md-editor-content :deep(.tiptap ul[data-type='taskList'] > li > div) {
  flex: 1 1 auto;
  min-width: 0;
}

.md-editor-content :deep(.tiptap ul[data-type='taskList'] > li > div p) {
  margin: 0;
}

.md-editor-textarea {
  flex: 1 1 0;
  min-height: var(--md-editor-min-height);
  resize: none;
  border: 1px solid var(--bo-border-default);
  border-radius: 8px;
  padding: 0.5rem;
  overflow-y: hidden;
  white-space: pre-wrap;
  overflow-wrap: anywhere;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, 'Liberation Mono', 'Courier New', monospace;
  line-height: 1.35;
}

.md-editor-textarea:focus {
  outline: none;
  border-color: var(--bo-colour-secondary);
}

.md-editor-image-message {
  display: block;
  min-width: 0;
  color: var(--bo-ink-muted);
  overflow-wrap: anywhere;
}

.md-editor-image-failures {
  display: grid;
  gap: 0.35rem;
}

.md-editor-image-failure {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
  border: 1px dashed var(--bo-border-default);
  border-radius: 8px;
  padding: 0.45rem 0.55rem;
  color: var(--bo-ink-muted);
}

.md-editor-image-failure-actions {
  display: inline-flex;
  gap: 0.35rem;
}
</style>
