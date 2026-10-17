export type CardIdsByColumn = Record<number, number[]>;

// Compare the order of cards present in both snapshots. Creation/removal alone
// must not turn into a movement effect for every card whose index changed.
export function findMovedColumns(before: CardIdsByColumn, after: CardIdsByColumn) {
  const beforeIds = new Set(Object.values(before).flat());
  const afterIds = new Set(Object.values(after).flat());
  const columns = new Set([...Object.keys(before), ...Object.keys(after)].map(Number));
  const movedColumns = new Set<number>();
  for (const columnId of columns) {
    const oldOrder = (before[columnId] ?? []).filter(id => afterIds.has(id));
    const newOrder = (after[columnId] ?? []).filter(id => beforeIds.has(id));
    if (oldOrder.length !== newOrder.length || oldOrder.some((id, index) => id !== newOrder[index])) {
      movedColumns.add(columnId);
    }
  }
  return movedColumns;
}
