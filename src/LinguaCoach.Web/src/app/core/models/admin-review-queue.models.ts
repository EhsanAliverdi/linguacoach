// Phase I2A — the legacy ActivityTemplate entity was removed; the review queue now covers
// PlacementItem only. See docs/reviews/2026-07-10-phase-i2a-practice-gym-legacy-deletion-review.md.
export const REVIEW_QUEUE_ENTITY_TYPES = ['PlacementItem'] as const;
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
