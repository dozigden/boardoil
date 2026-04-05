export type Card = {
  id: number;
  boardColumnId: number;
  cardTypeId: number;
  cardTypeName: string;
  cardTypeEmoji: string | null;
  title: string;
  description: string;
  sortKey: string;
  tagNames: string[];
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type TagStyleName = 'solid' | 'gradient';
export type BoardMemberRole = 'Owner' | 'Contributor' | string;

export type Tag = {
  id: number;
  name: string;
  styleName: TagStyleName;
  stylePropertiesJson: string;
  emoji: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type CardType = {
  id: number;
  name: string;
  emoji: string | null;
  isSystem: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type Column = {
  id: number;
  title: string;
  sortKey: string;
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type BoardColumn = Column & {
  cards: Card[];
};

export type Board = {
  id: number;
  name: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  currentUserRole?: BoardMemberRole | null;
  columns: BoardColumn[];
};

export type BoardSummary = {
  id: number;
  name: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  currentUserRole?: BoardMemberRole | null;
};

export type SystemBoardSummary = {
  id: number;
  name: string;
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type BoardMember = {
  userId: number;
  userName: string;
  role: BoardMemberRole;
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type ApiEnvelope<T> = {
  success: boolean;
  data: T | null;
  statusCode: number;
  message?: string;
};
