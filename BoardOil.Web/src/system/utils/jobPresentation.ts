const labels: Record<string, string> = {
  'oauth.client-registration.cleanup': 'OAuth client registration cleanup',
  'error-log.purge': 'Error log purge',
  'oauth-token-audit.purge': 'OAuth token audit purge',
  'history.purge': 'History purge'
};

export function jobTypeLabel(type: string) { return labels[type] ?? type; }
export function formatJobDate(value: unknown): string {
  if (typeof value !== 'string' || !value) return '-';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}
export function statusClass(status: string): string {
  if (status === 'completed') return 'job-status--completed';
  if (status === 'failed') return 'job-status--failed';
  if (status === 'running') return 'job-status--running';
  return '';
}
export function formatDiagnosticJson(value: string | null): string {
  if (!value) return '-';
  try { return JSON.stringify(JSON.parse(value), null, 2); } catch { return value; }
}
