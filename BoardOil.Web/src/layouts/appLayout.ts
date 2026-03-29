export const APP_LAYOUT_PAGE = 'page' as const;
export const APP_LAYOUT_BOARD = 'board' as const;
export const APP_LAYOUT_ADMIN = 'admin' as const;

export type AppLayoutMode = typeof APP_LAYOUT_PAGE | typeof APP_LAYOUT_BOARD | typeof APP_LAYOUT_ADMIN;

export function resolveAppLayout(layout: unknown): AppLayoutMode {
  if (layout === APP_LAYOUT_BOARD) {
    return APP_LAYOUT_BOARD;
  }

  if (layout === APP_LAYOUT_ADMIN) {
    return APP_LAYOUT_ADMIN;
  }

  return APP_LAYOUT_PAGE;
}
