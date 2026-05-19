export type RectLike = {
  left: number;
  top: number;
  width: number;
  height: number;
};

export type GooItem = {
  id: string;
  groupKey: string;
  colour: string;
  rect: RectLike;
  clipRect: RectLike | null;
};

export type GooRenderBlob = {
  id: string;
  top: number;
  left: number;
  width: number;
  height: number;
  clipPath?: string;
  borderRadius?: string;
};

export type GooRenderGroup = {
  id: string;
  colour: string;
  blobs: GooRenderBlob[];
};

export type GooConfig = {
  widthAdjustPx: number;
  horizontalOffsetPx: number;
  heightAdjustPx: number;
  verticalOffsetPx: number;
  blurStdDeviation: number;
  alphaMultiplier: number;
  alphaOffset: number;
  bridgeMaxGapPx: number;
  bridgeMaxVerticalDeltaPx: number;
  bridgeOverlapPx: number;
  bridgeHeightRatio: number;
  minBlobSizePx: number;
  blobBorderRadiusPx: number;
  clipHorizontalInsetPx: number;
};

export function buildGooGroups(items: GooItem[], boardRect: RectLike, config: GooConfig): GooRenderGroup[] {
  const groups = new Map<string, GooComputedGroup>();
  for (const item of items) {
    const group = getOrCreateGroup(groups, item.groupKey, item.colour);
    const blobWidth = Math.max(config.minBlobSizePx, item.rect.width + config.widthAdjustPx);
    const blobHeight = Math.max(config.minBlobSizePx, item.rect.height + config.heightAdjustPx);
    const clippedBlob = toClippedBlob(
      item.id,
      item.rect,
      blobWidth,
      blobHeight,
      boardRect,
      item.clipRect,
      config
    );
    if (!clippedBlob) {
      continue;
    }

    group.blobs.push(clippedBlob);
  }

  for (const group of groups.values()) {
    appendBridgeBlobs(group, config);
  }

  return [...groups.values()]
    .map(group => ({
      id: group.id,
      colour: group.colour,
      blobs: group.blobs
        .sort((left, right) => left.top - right.top)
        .map(toRenderBlob)
    }))
    .filter(group => group.blobs.length > 0);
}

type GooComputedBlob = GooRenderBlob & {
  centerX: number;
  centerY: number;
};

type GooComputedGroup = {
  id: string;
  colour: string;
  blobs: GooComputedBlob[];
};

function getOrCreateGroup(groups: Map<string, GooComputedGroup>, groupKey: string, colour: string): GooComputedGroup {
  const existing = groups.get(groupKey);
  if (existing) {
    return existing;
  }

  const created: GooComputedGroup = {
    id: groupKey,
    colour,
    blobs: []
  };
  groups.set(groupKey, created);
  return created;
}

function toClippedBlob(
  id: string,
  rect: RectLike,
  blobWidth: number,
  blobHeight: number,
  boardRect: RectLike,
  clipRect: RectLike | null,
  config: GooConfig
): GooComputedBlob | null {
  const desiredLeft = rect.left - boardRect.left + ((rect.width - blobWidth) / 2) + config.horizontalOffsetPx;
  const desiredTop = rect.top - boardRect.top + (rect.height * 0.5) + config.verticalOffsetPx - (blobHeight / 2);
  const desiredRight = desiredLeft + blobWidth;
  const desiredBottom = desiredTop + blobHeight;

  if (!clipRect) {
    return createBlob(id, desiredLeft, desiredTop, blobWidth, blobHeight);
  }

  const clipLeft = clipRect.left - boardRect.left;
  const clipTop = clipRect.top - boardRect.top;
  const clipRight = clipLeft + clipRect.width;
  const clipBottom = clipTop + clipRect.height;

  const clampedLeft = Math.max(desiredLeft, clipLeft);
  const clampedTop = Math.max(desiredTop, clipTop);
  const clampedRight = Math.min(desiredRight, clipRight);
  const clampedBottom = Math.min(desiredBottom, clipBottom);
  const width = clampedRight - clampedLeft;
  const height = clampedBottom - clampedTop;
  if (width < config.minBlobSizePx || height < config.minBlobSizePx) {
    return null;
  }

  const clipBleedPx = 20;
  const clipBottomBleedPx = 6;
  const clipInsetTop = Math.max(0, (clipTop - desiredTop) - clipBleedPx);
  const clipInsetRight = Math.max(0, (desiredRight - clipRight) - clipBleedPx);
  const clipInsetBottom = Math.max(0, (desiredBottom - clipBottom) - clipBottomBleedPx);
  const clipInsetLeft = Math.max(0, (clipLeft - desiredLeft) - clipBleedPx);
  const clipPath = toClipInsetPath(clipInsetTop, clipInsetRight, clipInsetBottom, clipInsetLeft);
  const borderRadius = toClipAwareBorderRadius(clipInsetTop, clipInsetRight, clipInsetBottom, clipInsetLeft);

  return createBlob(id, desiredLeft, desiredTop, blobWidth, blobHeight, clipPath, borderRadius);
}

function createBlob(
  id: string,
  left: number,
  top: number,
  width: number,
  height: number,
  clipPath?: string,
  borderRadius?: string
): GooComputedBlob {
  return {
    id,
    left,
    top: top + (height / 2),
    width,
    height,
    clipPath,
    borderRadius,
    centerX: left + (width / 2),
    centerY: top + (height / 2)
  };
}

function appendBridgeBlobs(group: GooComputedGroup, config: GooConfig) {
  const baseBlobs = [...group.blobs]
    .sort((left, right) => {
      if (left.left !== right.left) {
        return left.left - right.left;
      }

      if (left.centerX !== right.centerX) {
        return left.centerX - right.centerX;
      }

      return left.id.localeCompare(right.id);
    });
  let bridgeIndex = 0;
  for (let i = 0; i < baseBlobs.length; i += 1) {
    const leftBlob = baseBlobs[i]!;
    const leftRight = leftBlob.left + leftBlob.width;
    for (let j = i + 1; j < baseBlobs.length; j += 1) {
      const rightBlob = baseBlobs[j]!;
      if (Math.abs(leftBlob.centerY - rightBlob.centerY) > config.bridgeMaxVerticalDeltaPx) {
        continue;
      }

      const gap = rightBlob.left - leftRight;
      if (gap > config.bridgeMaxGapPx) {
        // Blobs are sorted by centerX/left, so later candidates can only increase this gap.
        break;
      }

      if (gap <= 0) {
        continue;
      }

      const bridgeWidth = gap + (config.bridgeOverlapPx * 2);
      const bridgeHeight = Math.max(
        config.minBlobSizePx,
        Math.min(leftBlob.height, rightBlob.height) * config.bridgeHeightRatio
      );
      const bridgeLeft = leftRight - config.bridgeOverlapPx;
      const bridgeCenterY = (leftBlob.centerY + rightBlob.centerY) / 2;

      group.blobs.push({
        id: `${group.id}-bridge-${bridgeIndex}`,
        left: bridgeLeft,
        top: bridgeCenterY,
        width: bridgeWidth,
        height: bridgeHeight,
        centerX: bridgeLeft + (bridgeWidth / 2),
        centerY: bridgeCenterY
      });
      bridgeIndex += 1;
    }
  }
}

function toRenderBlob(blob: GooComputedBlob): GooRenderBlob {
  return {
    id: blob.id,
    top: blob.top,
    left: blob.left,
    width: blob.width,
    height: blob.height,
    clipPath: blob.clipPath,
    borderRadius: blob.borderRadius
  };
}

function toClipInsetPath(top: number, right: number, bottom: number, left: number): string | undefined {
  if (top <= 0 && right <= 0 && bottom <= 0 && left <= 0) {
    return undefined;
  }

  return `inset(${top}px ${right}px ${bottom}px ${left}px)`;
}

function toClipAwareBorderRadius(top: number, right: number, bottom: number, left: number): string | undefined {
  if (top <= 0 && right <= 0 && bottom <= 0 && left <= 0) {
    return undefined;
  }

  const radiusToken = 'var(--goo-radius)';
  const topLeft = top > 0 || left > 0 ? '0px' : radiusToken;
  const topRight = top > 0 || right > 0 ? '0px' : radiusToken;
  const bottomRight = bottom > 0 || right > 0 ? '0px' : radiusToken;
  const bottomLeft = bottom > 0 || left > 0 ? '0px' : radiusToken;
  return `${topLeft} ${topRight} ${bottomRight} ${bottomLeft}`;
}
