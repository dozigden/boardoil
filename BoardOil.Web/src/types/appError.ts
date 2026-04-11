export type AppErrorKind = 'network' | 'http' | 'api' | 'parse' | 'unexpected';

export type AppError = {
  kind: AppErrorKind;
  message: string;
  statusCode?: number;
  validationErrors?: Record<string, string[]>;
};
