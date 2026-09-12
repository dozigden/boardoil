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
