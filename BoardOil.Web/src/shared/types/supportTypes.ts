export type SupportInfo = {
  database: DatabaseInfo;
};

export type DatabaseInfo = {
  provider: string;
  version: string;
  journalMode: string;
  sizeBytes: number;
};
