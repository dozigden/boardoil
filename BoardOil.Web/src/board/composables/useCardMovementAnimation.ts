import { onBeforeUnmount, onMounted, shallowReactive } from 'vue';
import type { BoardAnimationHandle, BoardAnimationRunner } from '../utils/boardAnimationRunner';

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
  columns: Map<number, { element: HTMLElement; height: number }>;
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
    board?.removeEventListener('scroll', finishAll, true);
    board = element instanceof HTMLElement ? element : null;
    board?.addEventListener('scroll', finishAll, { passive: true, capture: true });
  }

  function setLayerRef(element: unknown) {
    layer = element instanceof HTMLElement ? element : null;
  }

  function capture(columnIds: Set<number>) {
    const snapshot: CardMovementSnapshot = { cards: new Map(), columns: new Map() };
    if (!board || runner.reducedMotion) {
      return snapshot;
    }
    for (const column of board.querySelectorAll<HTMLElement>('[data-column-id]')) {
      const columnId = Number(column.dataset.columnId);
      if (!columnIds.has(columnId)) {
        continue;
      }
      const columnElement = column.parentElement!;
      snapshot.columns.set(columnId, { element: columnElement, height: columnElement.getBoundingClientRect().height });
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

  function play(snapshot: CardMovementSnapshot, targets: MovementTargets) {
    if (!board || !layer || runner.reducedMotion) {
      return;
    }
    const boardRect = board.getBoundingClientRect();
    const movingColumns = new Set<number>();
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
          position: 'absolute', margin: '0', visibility: 'visible', overflow: 'clip',
          left: `${target.left - boardRect.left + board.scrollLeft}px`,
          top: `${target.top - boardRect.top + board.scrollTop}px`,
          width: `${target.width}px`, height: `${target.height}px`
        });
        layer.append(floating);
        card.style.visibility = 'hidden';
        keyframes = [
          { transform: `translate(${dx}px, ${dy}px)`, width: `${previous.rect.width}px`, height: `${previous.rect.height}px` },
          { transform: 'translate(0, 0)', width: `${target.width}px`, height: `${target.height}px` }
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
    board?.removeEventListener('scroll', finishAll, true);
    finishAll();
  });

  return { floatingCards, setBoardRef, setLayerRef, capture, measureTargets, play, finishAll, prune };
}
