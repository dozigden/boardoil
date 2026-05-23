export type TagStyleName = 'solid' | 'gradient' | 'auto' | 'presets';
export type SlickStyleName = 'solid' | 'presets';
export type BoardMemberRole = 'Owner' | 'Contributor' | string;

export type TagPresentation = {
  styleName: TagStyleName;
  stylePropertiesJson: string;
  emoji: string | null;
};

export type CardTag = TagPresentation & {
  id: number;
  name: string;
};

export type CardComment = {
  id: number;
  cardId: number;
  authorUserId: number | null;
  text: string;
  createdAtUtc: string;
  authorDisplayName?: string | null;
  authorImageRelativePath?: string | null;
};

export type Card = {
  id: number;
  boardColumnId: number;
  cardTypeId: number;
  slickId?: number | null;
  slickName?: string | null;
  cardTypeName: string;
  cardTypeEmoji: string | null;
  assignedUserId?: number | null;
  assignedUserName?: string | null;
  assignedUserImageRelativePath?: string | null;
  title: string;
  description: string;
  sortKey: string;
  tags: CardTag[];
  tagNames: string[];
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type CardEditModel = {
  title: string;
  description: string;
  tagNames: string[];
  cardTypeId: number | null;
  boardColumnId: number;
  assignedUserId: number | null;
  slickName: string | null;
};

export type ArchivedCardListItem = {
  id: number;
  boardId: number;
  originalCardId: number;
  title: string;
  tagNames: string[];
  archivedAtUtc: string;
};

export type ArchivedCard = ArchivedCardListItem & {
  card: Card;
};

export type ArchivedCardList = {
  items: ArchivedCardListItem[];
  offset: number;
  limit: number;
  totalCount: number;
};

export type ArchiveCardsSummary = {
  boardId: number;
  requestedCount: number;
  archivedCount: number;
};

export type DeleteCardsSummary = {
  boardId: number;
  requestedCount: number;
  deletedCount: number;
};

export type Tag = TagPresentation & {
  id: number;
  name: string;
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type Slick = {
  id: number;
  name: string;
  styleName: SlickStyleName;
  stylePropertiesJson: string;
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type CardType = {
  id: number;
  name: string;
  styleName: TagStyleName;
  stylePropertiesJson: string;
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
  description: string;
  slickCohesionModeEnabled: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
  currentUserRole?: BoardMemberRole | null;
  columns: BoardColumn[];
};

export type BoardSummary = {
  id: number;
  name: string;
  description: string;
  slickCohesionModeEnabled: boolean;
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
  displayName: string;
  profileImageRelativePath?: string | null;
  role: BoardMemberRole;
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type ApiEnvelope<T> = {
  success: boolean;
  data: T | null;
  statusCode: number;
  message?: string;
  validationErrors?: Record<string, string[]>;
};
