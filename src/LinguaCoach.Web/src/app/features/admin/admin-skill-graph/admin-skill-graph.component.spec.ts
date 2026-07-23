import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AdminSkillGraphComponent } from './admin-skill-graph.component';
import { AdminApiService } from '../../../core/services/admin.api.service';
import {
  SkillGraphTaxonomy,
  SkillGraphNodeListResponse,
  SkillGraphCoverageResponse,
  SkillGraphDraftResponse,
  SkillGraphBatchActionResponse,
  SkillGraphBatchRejectResponse,
  RejectReconnectGroup,
  AddSkillGraphPrerequisiteResponse,
  GraphChangeSuggestionsResponse,
  NearDuplicateSuggestionsResponse,
  NearDuplicateNodeSuggestion,
  ConfirmNearDuplicateResponse,
  MergeNodesResponse,
} from '../../../core/models/admin.models';

// Adaptive Curriculum Sprint 1 — admin skill-graph review page.
// See docs/architecture/adaptive-curriculum-skill-graph.md.

const TAXONOMY: SkillGraphTaxonomy = {
  cefrLevels: ['A1', 'A2', 'B1', 'B2', 'C1', 'C2'],
  skills: ['grammar', 'vocabulary'],
  subskillsBySkill: { grammar: ['grammar.tense_aspect'], vocabulary: ['vocabulary.receptive'] },
  contextTags: ['general_english', 'workplace'],
  focusTags: ['general_english', 'workplace'],
};

const NODES: SkillGraphNodeListResponse = {
  items: [
    {
      id: 'n1', key: 'grammar.present_simple.a1', title: 'Present simple', description: 'D',
      cefrLevel: 'A1', skill: 'grammar', subskill: null, difficultyBand: 1,
      reviewStatus: 'PendingReview', isActive: true, rejectionReason: null, createdAt: '2026-07-17T00:00:00Z',
      contextTags: [], focusTags: [], linkedModuleCount: 0,
    },
  ],
  totalCount: 1, totalPages: 1, page: 1, pageSize: 25,
};

const COVERAGE: SkillGraphCoverageResponse = {
  matrix: [
    { cefrLevel: 'A1', skill: 'grammar', approvedCount: 0, pendingCount: 1, hasGap: true },
    { cefrLevel: 'A1', skill: 'vocabulary', approvedCount: 3, pendingCount: 0, hasGap: false },
  ],
};

function makeApi(overrides: Partial<Record<string, unknown>> = {}) {
  return {
    getSkillGraphTaxonomy: jasmine.createSpy('getSkillGraphTaxonomy').and.returnValue(of(TAXONOMY)),
    getSkillGraphNodes: jasmine.createSpy('getSkillGraphNodes').and.returnValue(of(NODES)),
    getSkillGraphCoverage: jasmine.createSpy('getSkillGraphCoverage').and.returnValue(of(COVERAGE)),
    draftSkillGraph: jasmine.createSpy('draftSkillGraph').and.returnValue(
      of<SkillGraphDraftResponse>({ queued: true, createdCount: 3, droppedEdgeCount: 0, error: null })),
    batchApproveSkillGraphNodes: jasmine.createSpy('batchApproveSkillGraphNodes').and.returnValue(
      of<SkillGraphBatchActionResponse>({ requestedCount: 1, succeeded: 1, failed: 0, limitReached: false })),
    batchRejectSkillGraphNodes: jasmine.createSpy('batchRejectSkillGraphNodes').and.returnValue(
      of<SkillGraphBatchRejectResponse>({ requestedCount: 1, succeeded: 1, failed: 0, limitReached: false, edgesRemoved: 0, reconnectSuggestions: [] })),
    getSkillGraphContentCoverage: jasmine.createSpy('getSkillGraphContentCoverage').and.returnValue(
      of({ totalApprovedNodes: 0, nodesWithContent: 0, nodesWithoutContentCount: 0, nodes: [] })),
    getSkillGraphNodeIssuesSummary: jasmine.createSpy('getSkillGraphNodeIssuesSummary').and.returnValue(
      of({ totalItems: 0, itemsWithIssues: 0 })),
    // Editability audit (2026-07-23) — isolated-node connectivity metric, loaded on init.
    getIsolatedSkillGraphNodes: jasmine.createSpy('getIsolatedSkillGraphNodes').and.returnValue(
      of({ isolatedCount: 0, isolated: [] })),
    // Phase 6.3e — acceptReconnectSuggestion's own addSkillGraphPrerequisite call.
    addSkillGraphPrerequisite: jasmine.createSpy('addSkillGraphPrerequisite').and.returnValue(
      of<AddSkillGraphPrerequisiteResponse>({ added: true, suggestions: [] })),
    // Phase 6.3a/6.3c/6.3f — merged "Graph audit" (runGraphAudit calls both in parallel).
    getRedundantEdgeSuggestions: jasmine.createSpy('getRedundantEdgeSuggestions').and.returnValue(
      of<GraphChangeSuggestionsResponse>({ count: 0, suggestions: [] })),
    getNearDuplicateSuggestions: jasmine.createSpy('getNearDuplicateSuggestions').and.returnValue(
      of<NearDuplicateSuggestionsResponse>({ count: 0, suggestions: [] })),
    mergeSkillGraphNodes: jasmine.createSpy('mergeSkillGraphNodes').and.returnValue(
      of<MergeNodesResponse>({ keepNodeId: 'n1', mergeAwayNodeId: 'n2', repointedCount: 0, droppedCount: 0 })),
    confirmNearDuplicate: jasmine.createSpy('confirmNearDuplicate').and.returnValue(
      of<ConfirmNearDuplicateResponse>({ success: true, isLikelyDuplicate: false, reasoning: 'Different content.', error: null })),
    ...overrides,
  };
}

describe('AdminSkillGraphComponent', () => {
  let fixture: ComponentFixture<AdminSkillGraphComponent>;
  let component: AdminSkillGraphComponent;
  let api: ReturnType<typeof makeApi>;

  async function setup(overrides: Partial<Record<string, unknown>> = {}) {
    api = makeApi(overrides);
    await TestBed.configureTestingModule({
      imports: [AdminSkillGraphComponent],
      providers: [provideRouter([]), provideHttpClient(), { provide: AdminApiService, useValue: api }],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminSkillGraphComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('renders the skill graph page', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('Skill Graph');
  });

  it('loads taxonomy, coverage, and nodes on init', async () => {
    await setup();
    expect(api.getSkillGraphTaxonomy).toHaveBeenCalledTimes(1);
    expect(api.getSkillGraphCoverage).toHaveBeenCalledTimes(1);
    expect(api.getSkillGraphNodes).toHaveBeenCalledTimes(1);
  });

  it('populates coverage gaps from the matrix', async () => {
    await setup();
    expect(component.coverageGaps().length).toBe(1);
    expect(component.coverageGaps()[0].skill).toBe('grammar');
  });

  it('shows a coverage-gap warning banner', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('have zero approved nodes');
  });

  it('runDraft requires both cefrLevel and skill', async () => {
    await setup();
    component.draftCefrLevel = '';
    component.draftSkill = '';
    component.runDraft();
    expect(component.draftError()).toBeTruthy();
    expect(api.draftSkillGraph).not.toHaveBeenCalled();
  });

  it('runDraft calls the API and reports the result', async () => {
    await setup();
    component.draftCefrLevel = 'A1';
    component.draftSkill = 'grammar';
    component.runDraft();
    expect(api.draftSkillGraph).toHaveBeenCalledWith('A1', 'grammar');
    expect(component.draftStatus()).toContain('Drafted 3 node(s)');
  });

  it('shows the draft error message when drafting fails', async () => {
    await setup({
      draftSkillGraph: jasmine.createSpy('draftSkillGraph').and.returnValue(
        of<SkillGraphDraftResponse>({ queued: false, createdCount: 0, error: 'AI provider unavailable' })),
    });
    component.draftCefrLevel = 'A1';
    component.draftSkill = 'grammar';
    component.runDraft();
    expect(component.draftError()).toBe('AI provider unavailable');
  });

  it('selectedIds tracks selected node ids', async () => {
    await setup();
    expect(component.hasSelection()).toBeFalse();
    component.selectedIds.set(new Set(['n1']));
    expect(component.hasSelection()).toBeTrue();
    expect(component.selectedIds().has('n1')).toBeTrue();
    component.selectedIds.set(new Set());
    expect(component.hasSelection()).toBeFalse();
  });

  it('batchApprove calls the API with selected ids and clears selection', async () => {
    await setup();
    component.selectedIds.set(new Set(['n1']));
    component.batchApprove();
    expect(api.batchApproveSkillGraphNodes).toHaveBeenCalledWith(['n1']);
    expect(component.hasSelection()).toBeFalse();
    expect(component.batchStatus()).toContain('Approved 1 of 1');
  });

  it('batchReject requires a reason', async () => {
    await setup();
    component.selectedIds.set(new Set(['n1']));
    component.rejectReason = '';
    component.batchReject();
    expect(component.batchError()).toBeTruthy();
    expect(api.batchRejectSkillGraphNodes).not.toHaveBeenCalled();
  });

  it('batchReject calls the API when a reason is set', async () => {
    await setup();
    component.selectedIds.set(new Set(['n1']));
    component.rejectReason = 'Too broad.';
    component.batchReject();
    expect(api.batchRejectSkillGraphNodes).toHaveBeenCalledWith(['n1'], 'Too broad.');
    expect(component.batchStatus()).toContain('Rejected 1 of 1');
  });

  // ── Editability audit (2026-07-23) — create modal, isolated-node metric, node detail ─────

  it('shows an isolated-node warning banner when nodes lack edges', async () => {
    await setup({
      getIsolatedSkillGraphNodes: jasmine.createSpy('getIsolatedSkillGraphNodes').and.returnValue(
        of({ isolatedCount: 1, isolated: [{ id: 'n1', key: 'grammar.present_simple.a1', title: 'Present simple', cefrLevel: 'A1', skill: 'grammar', reviewStatus: 'PendingReview' }] })),
    });
    expect(fixture.nativeElement.textContent).toContain('no prerequisite edges at all');
  });

  // User correction (2026-07-23) — Create moved from a slide-over to its own routed page
  // (admin-skill-graph-node-create.component.ts covers the create form itself); this page's own
  // responsibility is just navigating there.
  it('createNode navigates to the node create route', async () => {
    await setup();
    const router = TestBed.inject(Router);
    const navSpy = spyOn(router, 'navigateByUrl');
    component.createNode();
    expect(navSpy).toHaveBeenCalledWith('/admin/skill-graph/nodes/create');
  });

  // User correction (2026-07-23) — View moved from a slide-over to its own routed page;
  // clicking a Nodes row now navigates instead of loading node detail in place.
  it('viewNode navigates to the node view route', async () => {
    await setup();
    const router = TestBed.inject(Router);
    const navSpy = spyOn(router, 'navigateByUrl');
    component.viewNode(NODES.items[0]);
    expect(navSpy).toHaveBeenCalledWith('/admin/skill-graph/nodes/n1');
  });

  it('shows an error state when coverage fails to load', async () => {
    await setup({
      getSkillGraphCoverage: jasmine.createSpy('getSkillGraphCoverage').and.returnValue(throwError(() => new Error('fail'))),
    });
    expect(component.coverageError()).toBeTruthy();
  });

  it('shows an error state when nodes fail to load', async () => {
    await setup({
      getSkillGraphNodes: jasmine.createSpy('getSkillGraphNodes').and.returnValue(throwError(() => new Error('fail'))),
    });
    expect(component.nodesError()).toBeTruthy();
  });

  // Phase 6.3e — AddPrerequisite's inline redundant-edge check (6.3a scenario 1/3) was previously
  // discarded by acceptReconnectSuggestion; it's now appended to the same "Graph audit" list.
  it('acceptReconnectSuggestion surfaces an inline redundant-edge suggestion into the Graph audit list', async () => {
    const group: RejectReconnectGroup = {
      rejectedNodeId: 'b1', rejectedNodeTitle: 'B',
      orphanedPredecessors: [{ id: 'a1', title: 'A' }],
      orphanedDependents: [{ id: 'c1', title: 'C' }],
      suggestedReconnects: [{ nodeId: 'c1', nodeTitle: 'C', prerequisiteNodeId: 'a1', prerequisiteNodeTitle: 'A' }],
    };
    const inlineSuggestion = {
      type: 'RedundantEdge', description: 'now redundant',
      proposedEdgesToAdd: [],
      proposedEdgesToRemove: [{ nodeId: 'x1', nodeTitle: 'X', prerequisiteNodeId: 'y1', prerequisiteNodeTitle: 'Y' }],
    };
    await setup({
      addSkillGraphPrerequisite: jasmine.createSpy('addSkillGraphPrerequisite').and.returnValue(
        of<AddSkillGraphPrerequisiteResponse>({ added: true, suggestions: [inlineSuggestion] })),
    });
    component.reconnectSuggestionGroups.set([group]);

    component.acceptReconnectSuggestion(0, 0);

    expect(api.addSkillGraphPrerequisite).toHaveBeenCalledWith('c1', 'a1');
    expect(component.redundantEdgeSuggestions()).toEqual([inlineSuggestion]);
  });

  it('acceptReconnectSuggestion does not touch the Graph audit list when there are no inline suggestions', async () => {
    const group: RejectReconnectGroup = {
      rejectedNodeId: 'b1', rejectedNodeTitle: 'B',
      orphanedPredecessors: [{ id: 'a1', title: 'A' }],
      orphanedDependents: [{ id: 'c1', title: 'C' }],
      suggestedReconnects: [{ nodeId: 'c1', nodeTitle: 'C', prerequisiteNodeId: 'a1', prerequisiteNodeTitle: 'A' }],
    };
    await setup();
    component.reconnectSuggestionGroups.set([group]);

    component.acceptReconnectSuggestion(0, 0);

    expect(component.redundantEdgeSuggestions()).toEqual([]);
  });

  // Phase 6.3a/6.3c merged into one "Graph audit" (2026-07-24 user correction: "Graph audit and
  // Near-duplicate nodes are literally the same thing... Graph audit should return both").

  it('runGraphAudit populates both redundant-edge and near-duplicate suggestions from one click', async () => {
    const dup: NearDuplicateNodeSuggestion = {
      nodeAId: 'n1', nodeATitle: 'A', nodeADescription: 'Desc A',
      nodeBId: 'n2', nodeBTitle: 'B', nodeBDescription: 'Desc B',
      cefrLevel: 'A1', skill: 'grammar', similarity: 0.9,
    };
    await setup({
      getNearDuplicateSuggestions: jasmine.createSpy('getNearDuplicateSuggestions').and.returnValue(
        of<NearDuplicateSuggestionsResponse>({ count: 1, suggestions: [dup] })),
    });

    component.runGraphAudit();

    expect(api.getRedundantEdgeSuggestions).toHaveBeenCalledTimes(1);
    expect(api.getNearDuplicateSuggestions).toHaveBeenCalledTimes(1);
    expect(component.auditRun()).toBeTrue();
    expect(component.nearDuplicateSuggestions()).toEqual([dup]);
  });

  it('runGraphAudit reports an error when either call fails', async () => {
    await setup({
      getNearDuplicateSuggestions: jasmine.createSpy('getNearDuplicateSuggestions').and.returnValue(
        throwError(() => ({ error: { error: 'boom' } }))),
    });

    component.runGraphAudit();

    expect(component.auditError()).toBe('boom');
  });

  // Phase 6.3f — merge requires an explicit confirm step; nothing is called on "Keep A/B" alone.

  it('stageMerge does not call the API until confirmMerge is invoked', async () => {
    const dup: NearDuplicateNodeSuggestion = {
      nodeAId: 'n1', nodeATitle: 'A', nodeADescription: 'Desc A',
      nodeBId: 'n2', nodeBTitle: 'B', nodeBDescription: 'Desc B',
      cefrLevel: 'A1', skill: 'grammar', similarity: 0.9,
    };
    await setup();
    component.nearDuplicateSuggestions.set([dup]);

    component.stageMerge(dup, 'A');
    expect(api.mergeSkillGraphNodes).not.toHaveBeenCalled();
    expect(component.isPendingMergeFor(dup)).toBeTrue();

    component.confirmMerge();
    expect(api.mergeSkillGraphNodes).toHaveBeenCalledWith('n1', 'n2');
    expect(component.nearDuplicateSuggestions()).toEqual([]);
  });

  it('cancelMerge clears the pending confirmation without calling the API', async () => {
    const dup: NearDuplicateNodeSuggestion = {
      nodeAId: 'n1', nodeATitle: 'A', nodeADescription: 'Desc A',
      nodeBId: 'n2', nodeBTitle: 'B', nodeBDescription: 'Desc B',
      cefrLevel: 'A1', skill: 'grammar', similarity: 0.9,
    };
    await setup();
    component.nearDuplicateSuggestions.set([dup]);
    component.stageMerge(dup, 'B');

    component.cancelMerge();

    expect(api.mergeSkillGraphNodes).not.toHaveBeenCalled();
    expect(component.isPendingMergeFor(dup)).toBeFalse();
    expect(component.nearDuplicateSuggestions()).toEqual([dup]);
  });

  it('confirmDuplicateWithAi stores the AI verdict keyed to the pair, not array index', async () => {
    const dup: NearDuplicateNodeSuggestion = {
      nodeAId: 'n1', nodeATitle: 'A', nodeADescription: 'Desc A',
      nodeBId: 'n2', nodeBTitle: 'B', nodeBDescription: 'Desc B',
      cefrLevel: 'A1', skill: 'grammar', similarity: 0.9,
    };
    await setup();
    component.nearDuplicateSuggestions.set([dup]);

    component.confirmDuplicateWithAi(dup);

    expect(api.confirmNearDuplicate).toHaveBeenCalledWith('n1', 'n2');
    expect(component.duplicateAiResult(dup)?.isLikelyDuplicate).toBeFalse();
  });

  it('dismissNearDuplicateSuggestion clears AI results and any pending merge for that pair', async () => {
    const dup: NearDuplicateNodeSuggestion = {
      nodeAId: 'n1', nodeATitle: 'A', nodeADescription: 'Desc A',
      nodeBId: 'n2', nodeBTitle: 'B', nodeBDescription: 'Desc B',
      cefrLevel: 'A1', skill: 'grammar', similarity: 0.9,
    };
    await setup();
    component.nearDuplicateSuggestions.set([dup]);
    component.confirmDuplicateWithAi(dup);
    component.stageMerge(dup, 'A');

    component.dismissNearDuplicateSuggestion(dup);

    expect(component.nearDuplicateSuggestions()).toEqual([]);
    expect(component.duplicateAiResult(dup)).toBeUndefined();
    expect(component.isPendingMergeFor(dup)).toBeFalse();
  });
});
