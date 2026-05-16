import { describe, expect, it } from 'vitest';
import {
  clampProfileImageCropState,
  clampProfileImageCropZoom,
  createInitialProfileImageCropState,
  moveProfileImageCropCenterByDrag,
  resolveProfileImageCropRect,
  resolveProfileImageCroppedFileName
} from './profileImageCrop';

describe('profileImageCrop', () => {
  it('creates an initial centered crop state', () => {
    const state = createInitialProfileImageCropState(900, 600);

    expect(state.zoom).toBe(1);
    expect(state.centerX).toBe(450);
    expect(state.centerY).toBe(300);
  });

  it('clamps zoom and center to image bounds', () => {
    const state = clampProfileImageCropState({
      imageWidth: 900,
      imageHeight: 600,
      centerX: 50,
      centerY: 800,
      zoom: 99
    });

    expect(state.zoom).toBe(4);
    expect(state.centerX).toBe(75);
    expect(state.centerY).toBe(525);
  });

  it('resolves crop rect from clamped center and zoom', () => {
    const rect = resolveProfileImageCropRect({
      imageWidth: 900,
      imageHeight: 600,
      centerX: 450,
      centerY: 300,
      zoom: 2
    });

    expect(rect).toEqual({
      x: 300,
      y: 150,
      size: 300
    });
  });

  it('moves crop center with drag deltas', () => {
    const moved = moveProfileImageCropCenterByDrag(
      {
        imageWidth: 1000,
        imageHeight: 500,
        centerX: 500,
        centerY: 250,
        zoom: 1
      },
      64,
      -32,
      320
    );

    expect(moved.centerX).toBe(400);
    expect(moved.centerY).toBe(250);
  });

  it('clamps zoom to supported range', () => {
    expect(clampProfileImageCropZoom(0.5)).toBe(1);
    expect(clampProfileImageCropZoom(5)).toBe(4);
    expect(clampProfileImageCropZoom(2.25)).toBe(2.25);
  });

  it('normalises cropped file names to png extension', () => {
    expect(resolveProfileImageCroppedFileName('avatar.jpeg')).toBe('avatar.png');
    expect(resolveProfileImageCroppedFileName('avatar')).toBe('avatar.png');
    expect(resolveProfileImageCroppedFileName('  ')).toBe('profile-image.png');
  });
});
