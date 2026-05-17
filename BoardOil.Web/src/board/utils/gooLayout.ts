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

export type GooBlob = {
  id: string;
  top: number;
  left: number;
  width: number;
  height: number;
  centerX: number;
  centerY: number;
};

export type GooRenderGroup = {
  id: string;
  colour: string;
  blobs: GooBlob[];
};

export type GooConfig = {
  widthAdjustPx: number;
  heightAdjustPx: number;
  verticalOffsetPx: number;
  blurStdDeviation: number;
  alphaMultiplier: number;
  alphaOffset: number;
  bridgeMaxGapPx: number;
  bridgeMaxVerticalDeltaPx: number;
  bridgeOverlapPx: number;
  minBlobSizePx: number;
};

export function buildGooGroups(items: GooItem[], boardRect: RectLike, config: GooConfig): GooRenderGroup[] {
  const groups = new Map<string, GooRenderGroup>();
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
    .map(group => ({ ...group, blobs: group.blobs.sort((left, right) => left.top - right.top) }))
    .filter(group => group.blobs.length > 0);
}

function getOrCreateGroup(groups: Map<string, GooRenderGroup>, groupKey: string, colour: string): GooRenderGroup {
  const existing = groups.get(groupKey);
  if (existing) {
    return existing;
  }

  const created: GooRenderGroup = {
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
): GooBlob | null {
  const desiredLeft = rect.left - boardRect.left + ((rect.width - blobWidth) / 2);
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

  const left = Math.max(desiredLeft, clipLeft);
  const top = Math.max(desiredTop, clipTop);
  const right = Math.min(desiredRight, clipRight);
  const bottom = Math.min(desiredBottom, clipBottom);
  const width = right - left;
  const height = bottom - top;
  if (width < config.minBlobSizePx || height < config.minBlobSizePx) {
    return null;
  }

  return createBlob(id, left, top, width, height);
}

function createBlob(id: string, left: number, top: number, width: number, height: number): GooBlob {
  return {
    id,
    left,
    top: top + (height / 2),
    width,
    height,
    centerX: left + (width / 2),
    centerY: top + (height / 2)
  };
}

function appendBridgeBlobs(group: GooRenderGroup, config: GooConfig) {
  const baseBlobs = [...group.blobs];
  let bridgeIndex = 0;
  for (let i = 0; i < baseBlobs.length; i += 1) {
    const leftBlob = baseBlobs[i]!;
    for (let j = i + 1; j < baseBlobs.length; j += 1) {
      const rightBlob = baseBlobs[j]!;
      if (Math.abs(leftBlob.centerY - rightBlob.centerY) > config.bridgeMaxVerticalDeltaPx) {
        continue;
      }

      const first = leftBlob.centerX <= rightBlob.centerX ? leftBlob : rightBlob;
      const second = first === leftBlob ? rightBlob : leftBlob;
      const firstRight = first.left + first.width;
      const gap = second.left - firstRight;
      if (gap <= 0 || gap > config.bridgeMaxGapPx) {
        continue;
      }

      const bridgeWidth = gap + (config.bridgeOverlapPx * 2);
      const bridgeHeight = Math.max(
        config.minBlobSizePx,
        Math.min(first.height, second.height) * 0.52
      );
      const bridgeLeft = firstRight - config.bridgeOverlapPx;
      const bridgeCenterY = (first.centerY + second.centerY) / 2;

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

