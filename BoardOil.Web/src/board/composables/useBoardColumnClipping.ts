import { onBeforeUnmount, onMounted, watch, type Ref } from 'vue';
import { boardGooClips, columnGooBottomExtensions, columnReachesViewport } from '../utils/gooViewport';

export function useBoardColumnClipping(columns: Ref<unknown>, layoutVersion: Ref<number>, layoutChanged: () => void) {
  let board: HTMLElement | null = null;
  let observer: ResizeObserver | null = null;
  const observed = new Set<Element>();
  const gutters = new WeakMap<HTMLElement, { left: number; right: number }>();

  function refresh() {
    if (!board?.isConnected) return;
    const boardRect = board.getBoundingClientRect();
    const originY = boardRect.top + board.clientTop;
    const nextObserved = new Set<Element>([board]);
    const rows = [...board.querySelectorAll<HTMLElement>('.column-stack')].map(stack => {
      const column = stack.querySelector<HTMLElement>('.column')!;
      const scroller = column.querySelector<HTMLElement>('.column-content')!;
      const tail = stack.querySelector<HTMLElement>('.column-tail-drop-zone')!;
      const tailStyle = getComputedStyle(tail);
      let tailMinimum = Number.parseFloat(tailStyle.minHeight) || 0;
      if (tailStyle.boxSizing !== 'border-box') {
        tailMinimum += (Number.parseFloat(tailStyle.borderTopWidth) || 0) + (Number.parseFloat(tailStyle.borderBottomWidth) || 0);
      }
      const gap = Number.parseFloat(getComputedStyle(stack).rowGap) || 0;
      const full = columnReachesViewport(column.getBoundingClientRect().height, stack.clientHeight - gap - tailMinimum);
      if (!column.classList.contains('column--short')) {
        gutters.set(scroller, { left: scroller.clientLeft, right: scroller.offsetWidth - scroller.clientWidth - scroller.clientLeft });
      }
      const gutter = gutters.get(scroller)!;
      const rect = scroller.getBoundingClientRect();
      nextObserved.add(stack); nextObserved.add(column); nextObserved.add(scroller);
      return { column, scroller, full, gutter, top: rect.top - originY, bottom: rect.bottom - originY };
    });
    for (const element of observed) if (!nextObserved.has(element)) { observer?.unobserve(element); observed.delete(element); }
    for (const element of nextObserved) if (!observed.has(element)) { observer?.observe(element); observed.add(element); }
    const top = Math.max(0, Math.min(board.clientHeight, ...rows.map(row => row.top)));
    const bottom = Math.min(board.clientHeight, ...rows.filter(row => row.full).map(row => row.bottom));
    const previous = boardGooClips.get(board);
    let changed = !previous || previous.top !== top || previous.bottom !== bottom;
    boardGooClips.set(board, { top, bottom });
    board.style.setProperty('--board-goo-clip-top', `${top}px`);
    board.style.setProperty('--board-goo-clip-bottom', `${board.clientHeight - bottom}px`);
    for (const row of rows) {
      const short = !row.full;
      if (row.column.classList.contains('column--short') !== short) changed = true;
      row.column.style.setProperty('--column-scroll-gutter-left', `${row.gutter.left}px`);
      row.column.style.setProperty('--column-scroll-gutter-right', `${row.gutter.right}px`);
      row.column.classList.toggle('column--short', short);
      const extension = short ? Math.max(0, bottom - row.bottom) : 0;
      if (columnGooBottomExtensions.get(row.scroller) !== extension) changed = true;
      columnGooBottomExtensions.set(row.scroller, extension);
    }
    if (changed) layoutChanged();
  }

  function setBoardRef(element: unknown) {
    observer?.disconnect(); observed.clear();
    board = element instanceof HTMLElement ? element : null;
    refresh();
  }
  watch([columns, layoutVersion], refresh, { flush: 'post' });
  onMounted(() => { observer = new ResizeObserver(refresh); observed.clear(); refresh(); });
  onBeforeUnmount(() => { observer?.disconnect(); observed.clear(); board = null; });
  return { setBoardRef };
}
