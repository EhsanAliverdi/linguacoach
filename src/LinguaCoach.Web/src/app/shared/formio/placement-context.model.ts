/**
 * Passed into Formio.createForm(host, schema, { placementContext }) so the vanilla
 * "speakingResponse" Form.io component (which has no Angular HttpClient/interceptor) can upload
 * its recording without knowing about auth tokens, base URLs, or assessment/item identity itself.
 * PlacementComponent supplies a single function that does all of that, reading the same token
 * source auth.interceptor.ts uses for ordinary HttpClient calls.
 */
export interface PlacementSpeakingUploadResult {
  storageKey: string;
  mimeType: string;
  durationSeconds: number | null;
}

export interface PlacementFormioContext {
  uploadSpeakingAudio: (blob: Blob, mimeType: string, durationSeconds: number) => Promise<PlacementSpeakingUploadResult>;
}
