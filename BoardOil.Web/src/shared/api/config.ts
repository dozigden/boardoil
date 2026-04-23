const configuredApiBase = (import.meta.env.VITE_API_BASE as string | undefined)?.trim() ?? '';

export const apiBase = configuredApiBase ? configuredApiBase.replace(/\/+$/, '') : window.location.origin;

export const boardHubUrl = `${apiBase}/hubs/board`;

export function buildApiUrl(path: string) {
  return `${apiBase}${path}`;
}
