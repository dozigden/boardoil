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

export type CardAttachmentImageCandidate = {
  cardId: number;
  attachmentId: number;
  originalFileName: string;
  hasThumbnail: boolean;
};
