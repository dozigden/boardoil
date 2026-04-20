export const APP_LAYOUT_PAGE = 'page' as const;
export const APP_LAYOUT_BOARD = 'board' as const;
export const APP_LAYOUT_ADMIN = 'admin' as const;
export const APP_LAYOUT_FULL_HEIGHT = 'full-height' as const;

export type AppLayoutMode =
  | typeof APP_LAYOUT_PAGE
  | typeof APP_LAYOUT_BOARD
  | typeof APP_LAYOUT_ADMIN
  | typeof APP_LAYOUT_FULL_HEIGHT;

export function resolveAppLayout(layout: unknown): AppLayoutMode {
  if (layout === APP_LAYOUT_BOARD) {
    return APP_LAYOUT_BOARD;
  }

  if (layout === APP_LAYOUT_ADMIN) {
    return APP_LAYOUT_ADMIN;
  }

  if (layout === APP_LAYOUT_FULL_HEIGHT) {
    return APP_LAYOUT_FULL_HEIGHT;
  }

  return APP_LAYOUT_PAGE;
}
