import type { BoardRealtime, BoardRealtimeHandlers } from '../board/realtime/boardRealtime';

export function createDemoRealtime(_handlers: BoardRealtimeHandlers): BoardRealtime {
  return {
    async connect() {
      // The static preview intentionally has no network-backed collaboration.
    },
    async disconnect() {
      // Nothing to tear down in the browser-local preview.
    }
  };
}
