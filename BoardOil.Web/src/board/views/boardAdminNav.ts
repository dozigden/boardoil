export type BoardAdminNavItem = {
  label: string;
  to: { name: string; params: { boardId: number } };
  activeRouteNames?: string[];
};

export function buildBoardAdminNavItems(boardId: number, currentUserRole: string | null | undefined): BoardAdminNavItem[] {
  const items: BoardAdminNavItem[] = [
    {
      label: 'Details',
      to: { name: 'board-details', params: { boardId } }
    },
    {
      label: 'Tags',
      to: { name: 'tags', params: { boardId } },
      activeRouteNames: ['tags', 'tags-new', 'tags-tag']
    }
  ];

  if (currentUserRole === 'Owner') {
    items.splice(1, 0, {
      label: 'Columns',
      to: { name: 'columns', params: { boardId } },
      activeRouteNames: ['columns', 'columns-column']
    });
    items.splice(2, 0, {
      label: 'Card Types',
      to: { name: 'card-types', params: { boardId } },
      activeRouteNames: ['card-types', 'card-types-new', 'card-types-card-type']
    });
    items.push({
      label: 'Members',
      to: { name: 'board-members', params: { boardId } }
    });
    items.push({
      label: 'Delete board',
      to: { name: 'board-delete', params: { boardId } }
    });
  }

  return items;
}
