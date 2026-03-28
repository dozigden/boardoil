export const APP_LAYOUT_PAGE = 'page' as const;
export const APP_LAYOUT_BOARD = 'board' as const;

export type AppLayoutMode = typeof APP_LAYOUT_PAGE | typeof APP_LAYOUT_BOARD;

export function resolveAppLayout(layout: unknown): AppLayoutMode {
  return layout === APP_LAYOUT_BOARD ? APP_LAYOUT_BOARD : APP_LAYOUT_PAGE;
}
