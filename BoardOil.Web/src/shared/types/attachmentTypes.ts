export type CardAttachment = {
  id: number;
  originalFileName: string;
  contentType: string;
  byteLength: number;
  createdAtUtc: string;
  createdByUserId: number | null;
  hasThumbnail: boolean;
};

export type CardAttachmentList = { items: CardAttachment[]; maxUploadByteLength: number };

export type BoardAttachmentInventoryItem = {
  id: number;
  originalFileName: string;
  contentType: string;
  byteLength: number;
  createdAtUtc: string;
  cardId: number;
  cardTitle: string;
  archived: boolean;
};

export type BoardAttachmentInventory = {
  items: BoardAttachmentInventoryItem[];
  totalCount: number;
  totalByteLength: number;
  matchingCount: number;
  offset: number;
  limit: number;
};

export type BoardAttachmentInventoryQuery = {
  offset: number;
  limit: number;
  sort: 'name' | 'date' | 'size';
  direction: 'asc' | 'desc';
  state: 'live' | 'archived' | 'both';
};

export const defaultBoardAttachmentInventoryQuery: BoardAttachmentInventoryQuery = {
  offset: 0, limit: 50, sort: 'date', direction: 'desc', state: 'both'
};

export type CardAttachmentImageCandidate = {
  cardId: number;
  attachmentId: number;
  originalFileName: string;
  hasThumbnail: boolean;
};
