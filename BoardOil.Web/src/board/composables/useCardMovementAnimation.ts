import { onBeforeUnmount, onMounted, shallowReactive } from 'vue';
import type { BoardAnimationHandle, BoardAnimationRunner } from '../utils/boardAnimationRunner';
import type { CardLayoutEffect } from './useCardLayoutAnimation';

type CardPosition = {
  rect: DOMRect;
  clip: DOMRect | null;
  columnId: number;
  floating: boolean;
  sortKey: string;
  foreground: boolean;
};
export type CardMovementSnapshot = {
  cards: Map<number, CardPosition>;
  columns: Map<number, {
    element: HTMLElement;
    height: number;
    content: HTMLElement;
    scrollTop: number;
    scrollLeft: number;
    focusedElement: Element | null;
  }>;
};
type MovementTargets = {
  cards: Map<number, { rect: DOMRect; clip: DOMRect; columnId: number }>;
  columns: Map<number, number>;
};
type MovingCard = {
  animation: BoardAnimationHandle;
  element: HTMLElement;
  floating: HTMLElement | null;
  columnId: number;
  foreground: boolean;
};

export function useCardMovementAnimation(runner: BoardAnimationRunner) {
  const floatingCards = shallowReactive(new Map<number, HTMLElement>());
  const active = new Map<number, MovingCard>();
  const columnAnimations = new Map<number, BoardAnimationHandle>();
  const scrollPositions = new WeakMap<HTMLElement, { top: number; left: number }>();
  let board: HTMLElement | null = null;
  let layer: HTMLElement | null = null;

  function settle(cardId: number) {
    active.get(cardId)?.animation.cancel();
  }

  function finishAll() {
    if (active.size === 0 && columnAnimations.size === 0) {
      return;
    }
    for (const cardId of active.keys()) {
      settle(cardId);
    }
    for (const animation of columnAnimations.values()) {
      animation.finish();
    }
    columnAnimations.clear();
  }

  function setBoardRef(element: unknown) {
    board?.removeEventListener('scroll', handleScroll, true);
    board = element instanceof HTMLElement ? element : null;
    board?.addEventListener('scroll', handleScroll, { passive: true, capture: true });
  }

  function rememberScrollPosition(element: HTMLElement) {
    const position = { top: element.scrollTop, left: element.scrollLeft };
    scrollPositions.set(element, position);
    return position;
  }

  function handleScroll(event: Event) {
    if (!(event.target instanceof HTMLElement)) { return; }
    const previous = scrollPositions.get(event.target);
    const current = rememberScrollPosition(event.target);
    // Endpoint measurement may queue a scroll event even though its offset has
    // already been restored. Only a changed offset should settle movement.
    if (previous && previous.top === current.top && previous.left === current.left) { return; }
    finishAll();
  }

  function setLayerRef(element: unknown) {
    layer = element instanceof HTMLElement ? element : null;
  }

  function capture(columnIds: Set<number>) {
    const snapshot: CardMovementSnapshot = { cards: new Map(), columns: new Map() };
    if (!board || runner.reducedMotion) {
      return snapshot;
    }
    rememberScrollPosition(board);
    for (const column of board.querySelectorAll<HTMLElement>('[data-column-id]')) {
      const columnId = Number(column.dataset.columnId);
      if (!columnIds.has(columnId)) {
        continue;
      }
      rememberScrollPosition(column);
      const columnElement = column.parentElement!;
      snapshot.columns.set(columnId, {
        element: columnElement, height: columnElement.getBoundingClientRect().height,
        content: column, scrollTop: column.scrollTop, scrollLeft: column.scrollLeft,
        focusedElement: document.activeElement
      });
      for (const card of column.querySelectorAll<HTMLElement>(':scope > .card[data-card-id]')) {
        const id = Number(card.dataset.cardId);
        const moving = active.get(id);
        snapshot.cards.set(id, {
          rect: (moving?.floating ?? card).getBoundingClientRect(),
          clip: moving?.floating ? null : column.getBoundingClientRect(),
          columnId, floating: Boolean(moving?.floating),
          sortKey: card.dataset.cardSortKey!, foreground: moving?.foreground ?? false
        });
      }
    }
    // Preserve the visible positions before cancelling any previous effect.
    for (const [id, moving] of active) {
      if (columnIds.has(moving.columnId) || snapshot.cards.has(id)) {
        settle(id);
      }
    }
    for (const columnId of columnIds) {
      const animation = columnAnimations.get(columnId);
      if (animation) {
        animation.cancel();
      }
    }
    return snapshot;
  }

  function measureTargets(snapshot: CardMovementSnapshot): MovementTargets {
    const targets: MovementTargets = { cards: new Map(), columns: new Map() };
    if (!board) {
      return targets;
    }
    for (const card of board.querySelectorAll<HTMLElement>('.column-content > .card[data-card-id]')) {
      const id = Number(card.dataset.cardId);
      if (snapshot.cards.has(id)) {
        const column = card.parentElement!;
        targets.cards.set(id, { rect: card.getBoundingClientRect(), clip: column.getBoundingClientRect(), columnId: Number(column.dataset.columnId) });
      }
    }
    for (const [id, { element }] of snapshot.columns) {
      if (element.isConnected) {
        targets.columns.set(id, element.getBoundingClientRect().height);
      }
    }
    return targets;
  }

  function restoreScroll(snapshot: CardMovementSnapshot) {
    // Vue's boundary-class update can itself invoke scroll anchoring before
    // endpoint measurement. Use the offset captured before that render.
    for (const { content, scrollTop, scrollLeft, focusedElement } of snapshot.columns.values()) {
      // Opening an editor intentionally focuses and reveals a new input after
      // render. Preserve that reveal rather than restoring the old viewport.
      const currentFocus = document.activeElement;
      if (currentFocus !== focusedElement && content.contains(currentFocus)) { continue; }
      content.scrollTop = scrollTop;
      content.scrollLeft = scrollLeft;
    }
  }

  function play(snapshot: CardMovementSnapshot, targets: MovementTargets, layout: {
    resizingColumns: Set<number>;
    effects: CardLayoutEffect[];
  }) {
    if (!board || !layer || runner.reducedMotion) {
      return;
    }
    const boardRect = board.getBoundingClientRect();
    const movingColumns = new Set(layout.resizingColumns);
    const boxEffects = new Map(layout.effects.map(effect => [effect.element, effect]));
    for (const card of board.querySelectorAll<HTMLElement>('.column-content > .card[data-card-id]')) {
      const id = Number(card.dataset.cardId);
      const previous = snapshot.cards.get(id);
      if (!previous) {
        continue;
      }
      const destination = targets.cards.get(id);
      if (!destination) {
        continue;
      }
      const columnId = destination.columnId;
      const useFloatingCard = columnId !== previous.columnId || previous.floating;
      // Real cards also move with growing/collapsing creation effects. Their
      // offsets start against that initial flow; floating copies land at the
      // measured final layout, independently of the changing flow below them.
      const target = useFloatingCard ? destination.rect : card.getBoundingClientRect();
      const dx = previous.rect.left - target.left;
      const dy = previous.rect.top - target.top;
      if (Math.abs(dx) < 0.5 && Math.abs(dy) < 0.5) {
        continue;
      }
      let floating: HTMLElement | null = null;
      let keyframes: Keyframe[];
      const visibility = card.style.visibility;
      // A changed sort key identifies the moved card, rather than neighbours
      // whose positions shifted to make room. Preserve this when interrupted.
      const foreground = previous.foreground || previous.sortKey !== card.dataset.cardSortKey;
      // A second move in this column may displace a card still travelling in
      // from another column. Keep that card floating until it reaches home.
      if (useFloatingCard) {
        const targetClip = destination.clip;
        // An offscreen endpoint has no visible travel path. Keep it clipped in
        // its column rather than letting a floating copy cross the headers.
        const sourceClipped = previous.clip
          && (previous.rect.top < previous.clip.top || previous.rect.bottom > previous.clip.bottom);
        if (sourceClipped
          || target.top < targetClip.top || target.bottom > targetClip.bottom) {
          continue;
        }
        floating = card.cloneNode(true) as HTMLElement;
        floating.dataset.movingCardId = String(id);
        floating.removeAttribute('data-card-id');
        floating.classList.remove('card--dragging', 'card--multi-dragging');
        Object.assign(floating.style, {
          position: 'absolute', margin: '0', visibility: 'visible', overflow: 'clip', minHeight: '0',
          left: `${target.left - boardRect.left + board.scrollLeft}px`,
          top: `${target.top - boardRect.top + board.scrollTop}px`,
          width: `${target.width}px`, height: `${target.height}px`
        });
        layer.append(floating);
        card.style.visibility = 'hidden';
        // Copies need the same padding, border and opacity endpoints as the
        // real card; height alone would stop shrinking at its padding/borders.
        const boxEffect = boxEffects.get(card);
        keyframes = [
          { ...floatingBoxStyles(boxEffect?.from), transform: `translate(${dx}px, ${dy}px)`, width: `${previous.rect.width}px`, height: `${previous.rect.height}px` },
          { ...floatingBoxStyles(boxEffect?.to), transform: 'translate(0, 0)', width: `${target.width}px`, height: `${target.height}px` }
        ];
        floatingCards.set(id, floating);
      } else {
        const zIndex = foreground ? '2' : getComputedStyle(card).zIndex;
        keyframes = [
          { transform: `translate(${dx}px, ${dy}px)`, zIndex },
          { transform: 'translate(0, 0)', zIndex }
        ];
      }
      const cleanup = () => {
        card.style.visibility = visibility;
        floating?.remove();
        active.delete(id);
        floatingCards.delete(id);
      };
      const animation = runner.animate(floating ?? card, keyframes, { onFinish: cleanup, onCancel: cleanup });
      if (animation.active) {
        active.set(id, { animation, element: card, floating, columnId, foreground });
        movingColumns.add(previous.columnId);
        movingColumns.add(columnId);
      }
    }
    // Columns size to their content. Preserve their old clipping area while
    // neighbours close/open gaps, rather than clipping them at the final size.
    for (const [columnId, previous] of snapshot.columns) {
      const height = targets.columns.get(columnId);
      if (!previous.element.isConnected || height === undefined || !movingColumns.has(columnId)) {
        continue;
      }
      if (Math.abs(height - previous.height) < 0.5) {
        continue;
      }
      const cleanup = () => { columnAnimations.delete(columnId); };
      const animation = runner.animate(previous.element, [
        { height: `${previous.height}px` }, { height: `${height}px` }
      ], { onFinish: cleanup, onCancel: cleanup });
      if (animation.active) {
        columnAnimations.set(columnId, animation);
      }
    }
  }

  function prune() {
    for (const moving of active.values()) {
      if (!moving.element.isConnected) {
        finishAll();
        return;
      }
    }
  }

  onMounted(() => {
    window.addEventListener('resize', finishAll);
  });
  onBeforeUnmount(() => {
    window.removeEventListener('resize', finishAll);
    board?.removeEventListener('scroll', handleScroll, true);
    finishAll();
  });

  return { floatingCards, setBoardRef, setLayerRef, capture, restoreScroll, measureTargets, play, finishAll, prune };
}

function floatingBoxStyles(styles: CardLayoutEffect['from'] | undefined) {
  const result = { ...styles };
  // Margins belong to column flow. A copy's absolute coordinates already
  // include that spacing, so applying those margins again would offset it.
  delete result.marginTop;
  delete result.marginBottom;
  return result;
}
