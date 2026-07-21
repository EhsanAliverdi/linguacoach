import { of, throwError } from 'rxjs';
import { AdminImportRunCandidatesComponent } from './admin-import-run-candidates.component';
import {
  AdminResourceCandidateService,
  AdminResourceImportRunService,
} from '../../../core/services/admin-resource-import.service';
import { AdminResourceCandidateDto } from '../../../core/models/admin-resource-import.models';

/**
 * Phase 4.5 — focused unit coverage for the type-aware Candidate Review editor: correct editor
 * fields per candidate type, required-field/save-payload shape, approval gating on content
 * validity, server validation-error display, and read-only published state. Instantiates the
 * component directly (no TestBed render), matching AdminImportPackagePlanComponent's spec
 * convention — the logic under test lives in plain signal-driven methods.
 */
describe('AdminImportRunCandidatesComponent', () => {
  function baseCandidate(overrides: Partial<AdminResourceCandidateDto> = {}): AdminResourceCandidateDto {
    return {
      candidateId: 'cand-1',
      resourceRawRecordId: 'raw-1',
      resourceImportRunId: 'run-1',
      cefrResourceSourceId: 'source-1',
      candidateType: 'VocabularyEntry',
      canonicalText: 'hello',
      normalizedJson: '{"word":"hello"}',
      languageCode: 'en',
      cefrLevel: 'A1',
      cefrConfidence: null,
      primarySkill: null,
      subskill: null,
      difficultyBand: null,
      contextTagsJson: '[]',
      focusTagsJson: '[]',
      grammarTagsJson: null,
      vocabularyTagsJson: null,
      pronunciationTagsJson: null,
      activitySuitabilityTagsJson: null,
      safetyTagsJson: null,
      licenseTagsJson: null,
      qualityScore: null,
      contentFingerprint: 'fp-1',
      aiAnalysisJson: null,
      validationStatus: 'Passed',
      reviewStatus: 'PendingReview',
      rejectReason: null,
      adminNotes: null,
      createdAt: '2026-01-01T00:00:00Z',
      updatedAtUtc: '2026-01-01T00:00:00Z',
      isPublished: false,
      publishedAtUtc: null,
      publishedEntityType: null,
      publishedEntityId: null,
      publishedByUserId: null,
      canAttemptPublish: true,
      publishBlockReason: null,
      typedContentJson: '{"word":"hello","definition":"a greeting"}',
      contentValidationErrors: [],
      ...overrides,
    };
  }

  function makeComponent(svc: Partial<AdminResourceCandidateService> = {}) {
    const routeStub = { snapshot: { paramMap: { get: () => 'run-1' } } } as any;
    const routerStub = { navigate: jasmine.createSpy('navigate') } as any;
    const runSvcStub = { get: () => of({}) } as any as AdminResourceImportRunService;
    const withDefaults: Partial<AdminResourceCandidateService> = {
      list: () => of({ items: [], totalCount: 0, overallTotalCount: 0 }),
      summary: () => of({
        totalCount: 0, publishedCount: 0, passedCount: 0, needsReviewCount: 0, blockedCount: 0,
        publishableCount: 0, rejectedCount: 0, skippedCount: 0, pendingReviewCount: 0,
        stuckApprovedUnpublishableCount: 0,
      }),
      ...svc,
    };
    return new AdminImportRunCandidatesComponent(runSvcStub, withDefaults as AdminResourceCandidateService, routerStub, routeStub);
  }

  // ── Correct editor rendered per candidate type ──────────────────────────────

  it('hasTypedSchema is true for the six supported types and false for ActivityTemplateCandidate/Unknown', () => {
    const component = makeComponent();
    for (const type of ['VocabularyEntry', 'GrammarProfileEntry', 'ReadingPassage', 'ListeningPassage', 'SpeakingPrompt', 'WritingPrompt']) {
      expect(component.hasTypedSchema(type)).toBeTrue();
    }
    expect(component.hasTypedSchema('ActivityTemplateCandidate')).toBeFalse();
    expect(component.hasTypedSchema('Unknown')).toBeFalse();
  });

  it('openEdit parses typedContentJson into the typed draft for a Vocabulary candidate', () => {
    const component = makeComponent();
    const item = baseCandidate({
      typedContentJson: '{"word":"hello","definition":"a greeting","partOfSpeech":"interjection","examples":["Hi!","Hello there."]}',
    });

    component.openEdit(item);

    expect(component.editTargetType()).toBe('VocabularyEntry');
    expect(component.editDraft.typed.word).toBe('hello');
    expect(component.editDraft.typed.definition).toBe('a greeting');
    expect(component.editDraft.typed.partOfSpeech).toBe('interjection');
    expect(component.editDraft.typed.examples).toBe('Hi!, Hello there.');
  });

  it('openEdit falls back to the raw NormalizedJson textarea for an untyped candidate type', () => {
    const component = makeComponent();
    const item = baseCandidate({ candidateType: 'ActivityTemplateCandidate', typedContentJson: null, normalizedJson: '{"formIo":"{}"}' });

    component.openEdit(item);

    expect(component.hasTypedSchema(component.editTargetType())).toBeFalse();
    expect(component.editDraft.normalizedJson).toContain('formIo');
  });

  // ── Required-field validation / save payload shape ──────────────────────────

  it('confirmEdit sends a typedContentJson payload built from the typed draft, not raw NormalizedJson', () => {
    let capturedBody: any = null;
    const component = makeComponent({
      updateContent: (_id: string, body: any) => { capturedBody = body; return of(baseCandidate()); },
    });
    const item = baseCandidate();
    component.openEdit(item);
    component.editDraft.typed.word = 'greeting';
    component.editDraft.typed.definition = 'a friendly hello';

    component.confirmEdit();

    expect(capturedBody).not.toBeNull();
    expect(capturedBody.normalizedJson).toBeNull();
    const typed = JSON.parse(capturedBody.typedContentJson);
    expect(typed.word).toBe('greeting');
    expect(typed.definition).toBe('a friendly hello');
  });

  it('confirmEdit omits empty optional fields from the typed payload rather than sending blank strings', () => {
    let capturedBody: any = null;
    const component = makeComponent({
      updateContent: (_id: string, body: any) => { capturedBody = body; return of(baseCandidate()); },
    });
    component.openEdit(baseCandidate());
    component.editDraft.typed.partOfSpeech = '';
    component.editDraft.typed.examples = '';

    component.confirmEdit();

    const typed = JSON.parse(capturedBody.typedContentJson);
    expect('partOfSpeech' in typed).toBeFalse();
    expect('examples' in typed).toBeFalse();
  });

  // ── Approval disabled when invalid ───────────────────────────────────────────

  it('rowActions omits Approve & Publish when the candidate is publish-attemptable but has content errors', () => {
    const component = makeComponent();
    const item = baseCandidate({
      canAttemptPublish: true,
      contentValidationErrors: [{ fieldName: 'word', message: 'Word is required.' }],
    });

    const actions = component.rowActions(item);

    expect(actions.some(a => a.id === 'approve-and-publish')).toBeFalse();
  });

  it('rowActions includes Approve & Publish when publish-attemptable and content is valid', () => {
    const component = makeComponent();
    const item = baseCandidate({ canAttemptPublish: true, contentValidationErrors: [] });

    const actions = component.rowActions(item);

    expect(actions.some(a => a.id === 'approve-and-publish')).toBeTrue();
  });

  it('hasContentErrors reflects the DTO content validation errors', () => {
    const component = makeComponent();
    expect(component.hasContentErrors(baseCandidate({ contentValidationErrors: [] }))).toBeFalse();
    expect(component.hasContentErrors(baseCandidate({
      contentValidationErrors: [{ fieldName: 'word', message: 'Word is required.' }],
    }))).toBeTrue();
  });

  // ── Server validation errors ─────────────────────────────────────────────────

  it('confirmEdit surfaces structured server field errors from a 400 response without closing the modal', () => {
    const component = makeComponent({
      updateContent: () => throwError(() => ({
        error: { error: 'Candidate content failed typed schema validation', fieldErrors: [{ fieldName: 'word', message: 'Word is required.' }] },
      })),
    });
    component.openEdit(baseCandidate());

    component.confirmEdit();

    expect(component.editModalOpen()).toBeTrue();
    expect(component.editError()).toContain('failed typed schema validation');
    expect(component.fieldError('word')).toBe('Word is required.');
  });

  it('fieldError returns null when there is no error for that field', () => {
    const component = makeComponent();
    component.editFieldErrors.set([{ fieldName: 'word', message: 'Word is required.' }]);

    expect(component.fieldError('definition')).toBeNull();
  });

  // ── Read-only published state ────────────────────────────────────────────────

  it('rowActions omits Edit/Reject/Skip/Approve for a published candidate', () => {
    const component = makeComponent();
    const item = baseCandidate({ isPublished: true, publishedEntityType: 'CefrVocabularyEntry' });

    const actions = component.rowActions(item);

    expect(actions.some(a => a.id === 'edit')).toBeFalse();
    expect(actions.some(a => a.id === 'approve-and-publish')).toBeFalse();
    expect(actions.some(a => a.id === 'reject')).toBeFalse();
    expect(actions.some(a => a.id === 'skip')).toBeFalse();
  });
});
