export const APP_LAYOUT_STANDARD = 'standard' as const;
export const APP_LAYOUT_BOARD_WITH_CONVEYOR = 'board-with-conveyor' as const;
export const APP_LAYOUT_ADMIN = 'admin' as const;

export type AppLayoutMode =
  | typeof APP_LAYOUT_STANDARD
  | typeof APP_LAYOUT_BOARD_WITH_CONVEYOR
  | typeof APP_LAYOUT_ADMIN;

export function resolveAppLayout(layout: unknown): AppLayoutMode {
  if (layout === APP_LAYOUT_BOARD_WITH_CONVEYOR) {
    return APP_LAYOUT_BOARD_WITH_CONVEYOR;
  }

  if (layout === APP_LAYOUT_ADMIN) {
    return APP_LAYOUT_ADMIN;
  }

  return APP_LAYOUT_STANDARD;
}
