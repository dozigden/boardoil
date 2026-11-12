export type GooViewportClip = { top: number; bottom: number };
export const boardGooClips = new WeakMap<HTMLElement, GooViewportClip>();
export const columnGooBottomExtensions = new WeakMap<HTMLElement, number>();

export function columnReachesViewport(height: number, availableHeight: number) {
  return height >= availableHeight - 1;
}
