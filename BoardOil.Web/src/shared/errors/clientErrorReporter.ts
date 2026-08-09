import type { App, ComponentPublicInstance } from 'vue';
import type { Router } from 'vue-router';
import { getFrontendBuildInfo } from '../api/versionApi';
import {
  createClientErrorsApi,
  type ClientErrorReportRequest,
  type ClientErrorsApi
} from '../api/clientErrorsApi';

type ErrorCategory = 'general' | 'realtime';

type RouteContext = {
  routeName: string | null;
  routePath: string | null;
};

type NormalisedError = {
  exceptionType: string;
  message: string;
  stackTrace: string | null;
};

type ThrottleBucket = {
  fingerprintWindowMs: number;
  totalWindowMs: number;
  maxReports: number;
  sentAtByFingerprint: Map<string, number>;
  sentAt: number[];
};

type ClientErrorReporterOptions = {
  api?: ClientErrorsApi;
  now?: () => number;
  routeProvider?: () => RouteContext;
  viewportProvider?: () => { width: number; height: number } | null;
  userAgentProvider?: () => string | null;
  buildInfoProvider?: () => string | null;
};

const GeneralFingerprintWindowMs = 60_000;
const GeneralTotalWindowMs = 60_000;
const GeneralMaxReports = 10;
const RealtimeFingerprintWindowMs = 5 * 60_000;
const RealtimeTotalWindowMs = 5 * 60_000;
const RealtimeMaxReports = 3;
const MaxStringLength = 2048;
const MaxPhaseLength = 64;
const MaxRouteNameLength = 256;
const MaxContextKeyLength = 64;
const MaxContextKeys = 20;
const MaxContextDepth = 2;

export function createClientErrorReporter(options: ClientErrorReporterOptions = {}) {
  const api = options.api ?? createClientErrorsApi();
  const now = options.now ?? (() => Date.now());
  let routeProvider = options.routeProvider ?? (() => ({ routeName: null, routePath: null }));
  const viewportProvider = options.viewportProvider ?? defaultViewport;
  const userAgentProvider = options.userAgentProvider ?? defaultUserAgent;
  const buildInfoProvider = options.buildInfoProvider ?? defaultBuildInfo;
  const buckets: Record<ErrorCategory, ThrottleBucket> = {
    general: {
      fingerprintWindowMs: GeneralFingerprintWindowMs,
      totalWindowMs: GeneralTotalWindowMs,
      maxReports: GeneralMaxReports,
      sentAtByFingerprint: new Map<string, number>(),
      sentAt: []
    },
    realtime: {
      fingerprintWindowMs: RealtimeFingerprintWindowMs,
      totalWindowMs: RealtimeTotalWindowMs,
      maxReports: RealtimeMaxReports,
      sentAtByFingerprint: new Map<string, number>(),
      sentAt: []
    }
  };

  function setRouteProvider(nextRouteProvider: () => RouteContext) {
    routeProvider = nextRouteProvider;
  }

  async function reportError(
    error: unknown,
    phase: string,
    context: Record<string, unknown> | null = null,
    category: ErrorCategory = 'general'
  ): Promise<boolean> {
    try {
      return await sendReport(normaliseError(error), phase, context, category);
    } catch {
      return false;
    }
  }

  async function reportRealtimeDiagnostic(
    phase: string,
    error: unknown,
    context: Record<string, unknown> | null = null
  ): Promise<boolean> {
    try {
      return await sendReport(normaliseError(error, phase), phase, context, 'realtime');
    } catch {
      return false;
    }
  }

  async function sendReport(
    error: NormalisedError,
    phase: string,
    context: Record<string, unknown> | null,
    category: ErrorCategory
  ): Promise<boolean> {
    const route = safeCall(routeProvider, { routeName: null, routePath: null });
    const safePhase = truncate(phase, MaxPhaseLength) ?? 'unknown';
    const routePath = truncate(route.routePath);
    const fingerprint = buildFingerprint(error, safePhase, routePath);
    if (!allowReport(category, fingerprint)) {
      return false;
    }

    const request: ClientErrorReportRequest = {
      message: truncate(error.message) ?? 'Unknown frontend error',
      exceptionType: truncate(error.exceptionType),
      stackTrace: truncate(error.stackTrace),
      phase: safePhase,
      routeName: truncate(route.routeName, MaxRouteNameLength),
      routePath,
      frontendVersion: truncate(safeCall(buildInfoProvider, null)),
      viewport: safeCall(viewportProvider, null),
      userAgent: truncate(safeCall(userAgentProvider, null)),
      context: safeCall(() => sanitiseContext(context), null)
    };

    try {
      await api.reportClientError(request);
      return true;
    } catch {
      return false;
    }
  }

  function allowReport(category: ErrorCategory, fingerprint: string): boolean {
    const bucket = buckets[category];
    const sentAtUtc = now();
    for (const [key, sentAt] of bucket.sentAtByFingerprint) {
      if (sentAtUtc - sentAt >= bucket.fingerprintWindowMs) {
        bucket.sentAtByFingerprint.delete(key);
      }
    }

    const lastSentAt = bucket.sentAtByFingerprint.get(fingerprint);
    if (lastSentAt !== undefined && sentAtUtc - lastSentAt < bucket.fingerprintWindowMs) {
      return false;
    }

    bucket.sentAt = bucket.sentAt.filter(value => sentAtUtc - value < bucket.totalWindowMs);
    if (bucket.sentAt.length >= bucket.maxReports) {
      return false;
    }

    bucket.sentAtByFingerprint.set(fingerprint, sentAtUtc);
    bucket.sentAt.push(sentAtUtc);
    return true;
  }

  return {
    reportError,
    reportRealtimeDiagnostic,
    setRouteProvider
  };
}

export type ClientErrorReporter = ReturnType<typeof createClientErrorReporter>;

export const clientErrorReporter = createClientErrorReporter();

export function installFrontendErrorReporting(
  app: App,
  router: Router,
  reporter: ClientErrorReporter = clientErrorReporter
) {
  reporter.setRouteProvider(() => {
    const route = router.currentRoute.value;
    return {
      routeName: typeof route.name === 'string' ? route.name : null,
      routePath: route.fullPath
    };
  });

  app.config.errorHandler = (error, instance, info) => {
    void reporter.reportError(error, 'vue', {
      vueInfo: info,
      componentName: getComponentName(instance)
    });
  };

  window.addEventListener('error', event => {
    void reporter.reportError(event.error ?? event.message, 'window-error', {
      fileName: event.filename || null,
      lineNumber: event.lineno || null,
      columnNumber: event.colno || null
    });
  });

  window.addEventListener('unhandledrejection', event => {
    void reporter.reportError(event.reason, 'unhandled-rejection');
  });
}

function normaliseError(value: unknown, fallbackMessage = 'Unknown frontend error'): NormalisedError {
  if (value instanceof Error) {
    return {
      exceptionType: value.name || 'Error',
      message: value.message || fallbackMessage,
      stackTrace: value.stack ?? null
    };
  }

  if (typeof value === 'string') {
    return {
      exceptionType: 'StringError',
      message: value || fallbackMessage,
      stackTrace: null
    };
  }

  if (hasErrorShape(value)) {
    return {
      exceptionType: normaliseThrownText(value.name) ?? 'ErrorLike',
      message: normaliseThrownText(value.message) ?? fallbackMessage,
      stackTrace: typeof value.stack === 'string' ? value.stack : null
    };
  }

  return {
    exceptionType: 'NonError',
    message: fallbackMessage,
    stackTrace: null
  };
}

function hasErrorShape(value: unknown): value is { name?: unknown; message?: unknown; stack?: unknown } {
  return typeof value === 'object'
    && value !== null
    && ('message' in value || 'name' in value || 'stack' in value);
}

function normaliseThrownText(value: unknown): string | null {
  if (typeof value === 'string') {
    return value || null;
  }

  if (typeof value === 'number' || typeof value === 'boolean' || typeof value === 'bigint') {
    return String(value);
  }

  return null;
}

function buildFingerprint(error: NormalisedError, phase: string, routePath: string | null): string {
  return [
    phase,
    error.exceptionType,
    error.message,
    firstStackLine(error.stackTrace),
    routePath ?? ''
  ].join('|');
}

function firstStackLine(stackTrace: string | null): string {
  return stackTrace?.split(/\r?\n/).map(line => line.trim()).find(Boolean) ?? '';
}

function sanitiseContext(context: Record<string, unknown> | null): Record<string, unknown> | null {
  if (!context) {
    return null;
  }

  const sanitised = sanitiseObject(context, 0);
  return Object.keys(sanitised).length === 0 ? null : sanitised;
}

function sanitiseObject(value: Record<string, unknown>, depth: number): Record<string, unknown> {
  if (depth >= MaxContextDepth) {
    return {};
  }

  const sanitised: Record<string, unknown> = {};
  for (const [key, entry] of Object.entries(value)) {
    if (Object.keys(sanitised).length >= MaxContextKeys) {
      break;
    }

    if (isSensitiveContextKey(key)) {
      continue;
    }

    const sanitisedEntry = sanitiseValue(entry, depth + 1);
    if (sanitisedEntry !== undefined) {
      sanitised[truncate(key, MaxContextKeyLength) ?? 'unknown'] = sanitisedEntry;
    }
  }

  return sanitised;
}

function sanitiseValue(value: unknown, depth: number): unknown {
  if (value === null) {
    return null;
  }

  if (typeof value === 'string') {
    return truncate(value);
  }

  if (typeof value === 'number' || typeof value === 'boolean') {
    return value;
  }

  if (Array.isArray(value)) {
    return value
      .slice(0, MaxContextKeys)
      .map(entry => sanitiseValue(entry, depth + 1))
      .filter(entry => entry !== undefined);
  }

  if (typeof value === 'object' && value !== null && depth < MaxContextDepth) {
    return sanitiseObject(value as Record<string, unknown>, depth);
  }

  return undefined;
}

function isSensitiveContextKey(key: string): boolean {
  const normalised = key.toLowerCase();
  return normalised.includes('token')
    || normalised.includes('credential')
    || normalised.includes('secret')
    || normalised.includes('password')
    || normalised.includes('authorization')
    || normalised.includes('cookie')
    || normalised.includes('content');
}

function truncate(value: string | null | undefined, maxLength = MaxStringLength): string | null {
  if (!value) {
    return null;
  }

  return value.length <= maxLength ? value : value.slice(0, maxLength);
}

function defaultViewport() {
  if (typeof window === 'undefined') {
    return null;
  }

  return {
    width: window.innerWidth,
    height: window.innerHeight
  };
}

function defaultUserAgent() {
  return typeof navigator === 'undefined' ? null : navigator.userAgent;
}

function defaultBuildInfo() {
  try {
    const buildInfo = getFrontendBuildInfo();
    return `${buildInfo.version} (${buildInfo.channel}/${buildInfo.build}) ${buildInfo.commit}`;
  } catch {
    return null;
  }
}

function safeCall<T>(callback: () => T, fallback: T): T {
  try {
    return callback();
  } catch {
    return fallback;
  }
}

function getComponentName(instance: ComponentPublicInstance | null): string | null {
  try {
    return instance?.$options.name ?? null;
  } catch {
    return null;
  }
}
