export const REVIEW_QUEUE_ENTITY_TYPES = ['ActivityTemplate', 'PlacementItem'] as const;
export type ReviewQueueEntityType = (typeof REVIEW_QUEUE_ENTITY_TYPES)[number];

export interface AdminReviewQueueItemDto {
  entityType: ReviewQueueEntityType;
  entityId: string;
  displayKey: string;
  skill: string;
  cefrLevel: string;
  reviewStatus: string;
  createdAt: string;
}

export interface AdminReviewQueueResult {
  items: AdminReviewQueueItemDto[];
  totalCount: number;
  pendingCount: number;
}
