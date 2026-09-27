import type { SystemJobsRealtime } from './systemJobsRealtime';

export function createSystemJobsContext(
  realtime: SystemJobsRealtime,
  disposeJobs: () => void,
  reportConnectionError: (error: unknown) => void
) {
  let active = false;
  let version = 0;
  let transition = Promise.resolve();

  function setActive(nextActive: boolean) {
    if (active === nextActive) return transition;
    active = nextActive;
    const requestVersion = ++version;
    if (!nextActive) {
      // Block callbacks synchronously, even if a pending connect is still starting.
      realtime.suspend();
      disposeJobs();
    }
    transition = transition.then(async () => {
      if (requestVersion !== version) return;
      if (nextActive) await realtime.connect();
      else await realtime.disconnect();
    }).catch(error => {
      if (active) reportConnectionError(error);
    });
    return transition;
  }

  return { setActive };
}
