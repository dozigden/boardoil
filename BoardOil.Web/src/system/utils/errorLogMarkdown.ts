import type { ErrorLogDetails } from '../../shared/types/errorLogTypes';

export function buildErrorLogMarkdown(
  errorLog: ErrorLogDetails,
  occurredLabel: string
): string {
  return [
    `# Error #${errorLog.id}`,
    '',
    `- **Occurred:** ${occurredLabel}`,
    `- **Source:** ${errorLog.source}`,
    `- **Area:** ${errorLog.area}`,
    `- **Actor:** ${formatNullableReference(errorLog.actorUserId)}`,
    `- **Trace:** ${errorLog.traceIdentifier ?? '-'}`,
    `- **Request:** ${formatErrorLogRequest(errorLog)}`,
    '',
    '## Exception',
    '',
    errorLog.exceptionType,
    '',
    errorLog.message,
    '',
    '## Stack Trace',
    '',
    '```text',
    formatErrorLogStackTrace(errorLog.stackTrace),
    '```',
    '',
    '## Context JSON',
    '',
    '```json',
    formatErrorLogContextJson(errorLog.contextJson),
    '```'
  ].join('\n');
}

export function formatErrorLogRequest(errorLog: ErrorLogDetails): string {
  if (!errorLog.requestMethod && !errorLog.requestPath) {
    return '-';
  }

  return `${errorLog.requestMethod ?? '?'} ${errorLog.requestPath ?? '-'}`;
}

export function formatErrorLogStackTrace(stackTrace: string | null): string {
  if (!stackTrace) {
    return '-';
  }

  return stackTrace.replace(/\\r\\n|\\n|\\r/g, '\n');
}

export function formatErrorLogContextJson(contextJson: string | null): string {
  if (!contextJson) {
    return '-';
  }

  try {
    return JSON.stringify(JSON.parse(contextJson), null, 2);
  } catch {
    return contextJson;
  }
}

function formatNullableReference(value: number | null): string {
  return value === null ? '-' : `#${value}`;
}
