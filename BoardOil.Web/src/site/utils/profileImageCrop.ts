export const PROFILE_IMAGE_CROP_MIN_ZOOM = 1;
export const PROFILE_IMAGE_CROP_MAX_ZOOM = 4;

export type ProfileImageCropRect = {
  x: number;
  y: number;
  size: number;
};

export type ProfileImageCropState = {
  imageWidth: number;
  imageHeight: number;
  centerX: number;
  centerY: number;
  zoom: number;
};

export function clampProfileImageCropZoom(zoom: number) {
  return clamp(zoom, PROFILE_IMAGE_CROP_MIN_ZOOM, PROFILE_IMAGE_CROP_MAX_ZOOM);
}

export function resolveProfileImageCropRect(state: ProfileImageCropState): ProfileImageCropRect {
  const cropSize = resolveProfileImageCropSize(state.imageWidth, state.imageHeight, state.zoom);
  const halfCropSize = cropSize / 2;
  const clampedCenter = clampProfileImageCropCenter(
    state.centerX,
    state.centerY,
    state.imageWidth,
    state.imageHeight,
    cropSize
  );

  return {
    x: clampedCenter.centerX - halfCropSize,
    y: clampedCenter.centerY - halfCropSize,
    size: cropSize
  };
}

export function createInitialProfileImageCropState(imageWidth: number, imageHeight: number): ProfileImageCropState {
  const zoom = PROFILE_IMAGE_CROP_MIN_ZOOM;
  return {
    imageWidth,
    imageHeight,
    centerX: imageWidth / 2,
    centerY: imageHeight / 2,
    zoom
  };
}

export function clampProfileImageCropState(state: ProfileImageCropState): ProfileImageCropState {
  const zoom = clampProfileImageCropZoom(state.zoom);
  const cropSize = resolveProfileImageCropSize(state.imageWidth, state.imageHeight, zoom);
  const clampedCenter = clampProfileImageCropCenter(
    state.centerX,
    state.centerY,
    state.imageWidth,
    state.imageHeight,
    cropSize
  );

  return {
    ...state,
    zoom,
    centerX: clampedCenter.centerX,
    centerY: clampedCenter.centerY
  };
}

export function moveProfileImageCropCenterByDrag(
  state: ProfileImageCropState,
  dragDeltaX: number,
  dragDeltaY: number,
  previewPixelSize: number
): ProfileImageCropState {
  const safePreviewSize = Math.max(previewPixelSize, 1);
  const cropSize = resolveProfileImageCropSize(state.imageWidth, state.imageHeight, state.zoom);
  const sourceUnitsPerPreviewPixel = cropSize / safePreviewSize;

  const nextCenterX = state.centerX - dragDeltaX * sourceUnitsPerPreviewPixel;
  const nextCenterY = state.centerY - dragDeltaY * sourceUnitsPerPreviewPixel;

  return clampProfileImageCropState({
    ...state,
    centerX: nextCenterX,
    centerY: nextCenterY
  });
}

export function resolveProfileImageCroppedFileName(fileName: string) {
  const trimmed = fileName.trim();
  if (!trimmed) {
    return 'profile-image.png';
  }

  const lastDotIndex = trimmed.lastIndexOf('.');
  if (lastDotIndex <= 0) {
    return `${trimmed}.png`;
  }

  return `${trimmed.slice(0, lastDotIndex)}.png`;
}

function resolveProfileImageCropSize(imageWidth: number, imageHeight: number, zoom: number) {
  const safeWidth = Math.max(imageWidth, 1);
  const safeHeight = Math.max(imageHeight, 1);
  const safeZoom = Math.max(clampProfileImageCropZoom(zoom), PROFILE_IMAGE_CROP_MIN_ZOOM);
  return Math.min(safeWidth, safeHeight) / safeZoom;
}

function clampProfileImageCropCenter(
  centerX: number,
  centerY: number,
  imageWidth: number,
  imageHeight: number,
  cropSize: number
) {
  const halfCropSize = cropSize / 2;

  return {
    centerX: clamp(centerX, halfCropSize, Math.max(halfCropSize, imageWidth - halfCropSize)),
    centerY: clamp(centerY, halfCropSize, Math.max(halfCropSize, imageHeight - halfCropSize))
  };
}

function clamp(value: number, minimum: number, maximum: number) {
  if (value < minimum) {
    return minimum;
  }

  if (value > maximum) {
    return maximum;
  }

  return value;
}
