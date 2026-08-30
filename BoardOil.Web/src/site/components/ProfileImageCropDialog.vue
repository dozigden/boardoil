<template>
  <FixedChromeDialog
    :open="open"
    title="Crop Profile Image"
    close-label="Cancel image upload"
    @close="emit('close')"
    @submit="submitCrop"
  >
    <p class="profile-image-crop-dialog-hint">Drag to reposition and use zoom to frame your avatar.</p>

    <div class="profile-image-crop-dialog-layout">
      <section class="profile-image-crop-dialog-editor" aria-label="Image crop editor">
        <canvas
          ref="cropCanvasRef"
          class="profile-image-crop-dialog-canvas"
          width="320"
          height="320"
          role="img"
          aria-label="Square crop preview"
          @pointerdown="onCropPointerDown"
          @pointermove="onCropPointerMove"
          @pointerup="onCropPointerUp"
          @pointercancel="onCropPointerUp"
        />
      </section>

      <aside class="profile-image-crop-dialog-preview" aria-label="Avatar preview">
        <span class="profile-image-crop-dialog-preview-label">Avatar preview</span>
        <canvas
          ref="avatarCanvasRef"
          class="profile-image-crop-dialog-avatar-canvas"
          width="96"
          height="96"
          role="img"
          aria-label="Final avatar preview"
        />
      </aside>
    </div>

    <label class="profile-image-crop-dialog-zoom">
      <span>Zoom {{ cropZoomLabel }}</span>
      <input
        v-model.number="zoomValue"
        type="range"
        min="1"
        max="4"
        step="0.01"
        :disabled="isBusy || !cropState"
        @input="onZoomInput"
      />
    </label>

    <p v-if="activeErrorMessage" class="profile-image-crop-dialog-error" role="alert">{{ activeErrorMessage }}</p>

    <template #actions>
      <section class="fixed-chrome-dialog-actions">
        <button type="button" class="btn btn--secondary" :disabled="isBusy" @click="emit('close')">Cancel</button>
        <div class="fixed-chrome-dialog-actions-left">
          <button type="submit" class="btn" :disabled="isBusy || !canSubmit">Upload image</button>
        </div>
      </section>
    </template>
  </FixedChromeDialog>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue';
import FixedChromeDialog from '../../shared/components/FixedChromeDialog.vue';
import {
  clampProfileImageCropState,
  createInitialProfileImageCropState,
  moveProfileImageCropCenterByDrag,
  resolveProfileImageCropRect,
  resolveProfileImageCroppedFileName,
  type ProfileImageCropState
} from '../utils/profileImageCrop';

const props = defineProps<{
  open: boolean;
  sourceFile: File | null;
  busy: boolean;
  errorMessage: string | null;
}>();

const emit = defineEmits<{
  close: [];
  confirm: [croppedFile: File];
}>();

const CROP_PREVIEW_SIZE = 320;
const AVATAR_PREVIEW_SIZE = 96;
const EXPORT_SIZE = 512;

const cropCanvasRef = ref<HTMLCanvasElement | null>(null);
const avatarCanvasRef = ref<HTMLCanvasElement | null>(null);
const cropState = ref<ProfileImageCropState | null>(null);
const zoomValue = ref(1);
const localErrorMessage = ref<string | null>(null);
const loadingImage = ref(false);
const exportingImage = ref(false);

let loadedImage: HTMLImageElement | null = null;
let sourceImageObjectUrl: string | null = null;
let loadingVersion = 0;
let draggingPointerId: number | null = null;
let lastDragPoint: { x: number; y: number } | null = null;

const isBusy = computed(() => props.busy || loadingImage.value || exportingImage.value);
const canSubmit = computed(() => Boolean(props.open && props.sourceFile && loadedImage && cropState.value));
const cropZoomLabel = computed(() => `${zoomValue.value.toFixed(2)}x`);
const activeErrorMessage = computed(() => localErrorMessage.value ?? props.errorMessage);

watch(
  () => props.sourceFile,
  () => {
    void loadSourceFileImage();
  },
  { immediate: true }
);

watch(
  () => props.open,
  (isOpen) => {
    if (!isOpen) {
      localErrorMessage.value = null;
      draggingPointerId = null;
      lastDragPoint = null;
      return;
    }

    drawCanvases();
  }
);

watch(cropState, () => {
  zoomValue.value = cropState.value?.zoom ?? 1;
  drawCanvases();
}, { deep: true });

watch([cropCanvasRef, avatarCanvasRef], () => {
  drawCanvases();
});

onBeforeUnmount(() => {
  clearSourceImageObjectUrl();
});

async function loadSourceFileImage() {
  const sourceFile = props.sourceFile;
  clearSourceImageObjectUrl();
  localErrorMessage.value = null;
  loadedImage = null;
  cropState.value = null;

  if (!sourceFile) {
    return;
  }

  if (!sourceFile.type.startsWith('image/')) {
    localErrorMessage.value = 'Please choose a valid image file.';
    return;
  }

  loadingImage.value = true;
  const nextVersion = loadingVersion + 1;
  loadingVersion = nextVersion;

  sourceImageObjectUrl = URL.createObjectURL(sourceFile);

  try {
    const image = await loadImageFromObjectUrl(sourceImageObjectUrl);
    if (loadingVersion !== nextVersion) {
      return;
    }

    loadedImage = image;
    cropState.value = createInitialProfileImageCropState(image.naturalWidth, image.naturalHeight);
  } catch {
    if (loadingVersion !== nextVersion) {
      return;
    }

    localErrorMessage.value = 'Unable to load this image. Please choose a different file.';
    loadedImage = null;
    cropState.value = null;
  } finally {
    if (loadingVersion === nextVersion) {
      loadingImage.value = false;
    }
  }
}

function drawCanvases() {
  drawCanvas(cropCanvasRef.value, CROP_PREVIEW_SIZE, true);
  drawCanvas(avatarCanvasRef.value, AVATAR_PREVIEW_SIZE, false);
}

function drawCanvas(canvas: HTMLCanvasElement | null, canvasSize: number, showGuides: boolean) {
  if (!canvas) {
    return;
  }

  const context = canvas.getContext('2d');
  if (!context) {
    return;
  }

  context.clearRect(0, 0, canvasSize, canvasSize);
  context.fillStyle = '#171b22';
  context.fillRect(0, 0, canvasSize, canvasSize);

  if (!loadedImage || !cropState.value) {
    return;
  }

  const cropRect = resolveProfileImageCropRect(cropState.value);
  context.imageSmoothingEnabled = true;
  context.drawImage(
    loadedImage,
    cropRect.x,
    cropRect.y,
    cropRect.size,
    cropRect.size,
    0,
    0,
    canvasSize,
    canvasSize
  );

  if (!showGuides) {
    return;
  }

  const third = canvasSize / 3;
  context.strokeStyle = 'rgba(255, 255, 255, 0.4)';
  context.lineWidth = 1;
  context.beginPath();
  context.moveTo(third, 0);
  context.lineTo(third, canvasSize);
  context.moveTo(third * 2, 0);
  context.lineTo(third * 2, canvasSize);
  context.moveTo(0, third);
  context.lineTo(canvasSize, third);
  context.moveTo(0, third * 2);
  context.lineTo(canvasSize, third * 2);
  context.stroke();

  context.strokeStyle = 'rgba(255, 255, 255, 0.9)';
  context.strokeRect(0.5, 0.5, canvasSize - 1, canvasSize - 1);
}

function onZoomInput() {
  if (!cropState.value) {
    return;
  }

  cropState.value = clampProfileImageCropState({
    ...cropState.value,
    zoom: zoomValue.value
  });
}

function onCropPointerDown(event: PointerEvent) {
  if (isBusy.value || !cropCanvasRef.value || !cropState.value) {
    return;
  }

  const canvas = cropCanvasRef.value;
  draggingPointerId = event.pointerId;
  lastDragPoint = resolveCanvasPointerPoint(event, canvas);
  canvas.setPointerCapture(event.pointerId);
}

function onCropPointerMove(event: PointerEvent) {
  if (!cropCanvasRef.value || draggingPointerId !== event.pointerId || !lastDragPoint || !cropState.value) {
    return;
  }

  const nextPoint = resolveCanvasPointerPoint(event, cropCanvasRef.value);
  const deltaX = nextPoint.x - lastDragPoint.x;
  const deltaY = nextPoint.y - lastDragPoint.y;

  cropState.value = moveProfileImageCropCenterByDrag(cropState.value, deltaX, deltaY, CROP_PREVIEW_SIZE);
  lastDragPoint = nextPoint;
}

function onCropPointerUp(event: PointerEvent) {
  if (!cropCanvasRef.value || draggingPointerId !== event.pointerId) {
    return;
  }

  if (cropCanvasRef.value.hasPointerCapture(event.pointerId)) {
    cropCanvasRef.value.releasePointerCapture(event.pointerId);
  }

  draggingPointerId = null;
  lastDragPoint = null;
}

async function submitCrop() {
  if (isBusy.value || !props.sourceFile || !loadedImage || !cropState.value) {
    return;
  }

  localErrorMessage.value = null;
  exportingImage.value = true;

  try {
    const cropRect = resolveProfileImageCropRect(cropState.value);
    const exportCanvas = document.createElement('canvas');
    exportCanvas.width = EXPORT_SIZE;
    exportCanvas.height = EXPORT_SIZE;

    const context = exportCanvas.getContext('2d');
    if (!context) {
      localErrorMessage.value = 'Unable to prepare cropped image for upload.';
      return;
    }

    context.imageSmoothingEnabled = true;
    context.drawImage(
      loadedImage,
      cropRect.x,
      cropRect.y,
      cropRect.size,
      cropRect.size,
      0,
      0,
      EXPORT_SIZE,
      EXPORT_SIZE
    );

    const blob = await canvasToBlob(exportCanvas, 'image/png');
    if (!blob) {
      localErrorMessage.value = 'Unable to prepare cropped image for upload.';
      return;
    }

    const fileName = resolveProfileImageCroppedFileName(props.sourceFile.name);
    const croppedFile = new File([blob], fileName, { type: 'image/png' });
    emit('confirm', croppedFile);
  } finally {
    exportingImage.value = false;
  }
}

function resolveCanvasPointerPoint(event: PointerEvent, canvas: HTMLCanvasElement) {
  const bounds = canvas.getBoundingClientRect();
  return {
    x: (event.clientX - bounds.left) * (canvas.width / Math.max(bounds.width, 1)),
    y: (event.clientY - bounds.top) * (canvas.height / Math.max(bounds.height, 1))
  };
}

function clearSourceImageObjectUrl() {
  if (!sourceImageObjectUrl) {
    return;
  }

  URL.revokeObjectURL(sourceImageObjectUrl);
  sourceImageObjectUrl = null;
}

function loadImageFromObjectUrl(objectUrl: string) {
  return new Promise<HTMLImageElement>((resolve, reject) => {
    const image = new Image();
    image.onload = () => resolve(image);
    image.onerror = () => reject(new Error('Image load failed.'));
    image.src = objectUrl;
  });
}

function canvasToBlob(canvas: HTMLCanvasElement, type: string) {
  return new Promise<Blob | null>((resolve) => {
    canvas.toBlob((blob) => {
      resolve(blob);
    }, type);
  });
}
</script>

<style scoped>
.profile-image-crop-dialog-hint {
  margin: 0 0 0.75rem;
  color: var(--bo-ink-muted);
}

.profile-image-crop-dialog-layout {
  display: grid;
  gap: 0.9rem;
  grid-template-columns: minmax(0, 1fr) auto;
  align-items: start;
}

.profile-image-crop-dialog-editor {
  display: grid;
  gap: 0.4rem;
}

.profile-image-crop-dialog-canvas {
  width: min(100%, 20rem);
  max-width: 20rem;
  aspect-ratio: 1 / 1;
  border-radius: 10px;
  border: 1px solid var(--bo-border-default);
  touch-action: none;
  cursor: grab;
}

.profile-image-crop-dialog-canvas:active {
  cursor: grabbing;
}

.profile-image-crop-dialog-preview {
  display: grid;
  justify-items: center;
  gap: 0.4rem;
}

.profile-image-crop-dialog-preview-label {
  font-size: 0.8rem;
  color: var(--bo-ink-muted);
}

.profile-image-crop-dialog-avatar-canvas {
  width: 6rem;
  height: 6rem;
  border-radius: 999px;
  border: 1px solid var(--bo-border-default);
}

.profile-image-crop-dialog-zoom {
  display: grid;
  gap: 0.4rem;
  margin-top: 0.75rem;
}

.profile-image-crop-dialog-zoom > span {
  color: var(--bo-ink-muted);
  font-size: 0.85rem;
}

.profile-image-crop-dialog-zoom > input {
  width: 100%;
}

.profile-image-crop-dialog-error {
  margin: 0.5rem 0 0;
  color: var(--bo-colour-danger-ink);
}

@media (max-width: 760px) {
  .profile-image-crop-dialog-layout {
    grid-template-columns: 1fr;
  }

  .profile-image-crop-dialog-preview {
    justify-items: start;
  }
}
</style>
