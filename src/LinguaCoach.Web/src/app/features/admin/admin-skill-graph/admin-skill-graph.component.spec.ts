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
  SkillGraphBatchRejectConfirmationRequired,
  RejectReconnectGroup,
  AddSkillGraphPrerequisiteResponse,
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
    // Phase 6.3e — acceptReconnectSuggestion's own addSkillGraphPrerequisite call.
    addSkillGraphPrerequisite: jasmine.createSpy('addSkillGraphPrerequisite').and.returnValue(
      of<AddSkillGraphPrerequisiteResponse>({ added: true, suggestions: [] })),
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
    expect(api.batchRejectSkillGraphNodes).toHaveBeenCalledWith(['n1'], 'Too broad.', false);
    expect(component.batchStatus()).toContain('Rejected 1 of 1');
  });

  // Skill Graph pipeline audit (2026-07-24, Bug #1) — bulk-reject confirmation gate.
  describe('batchReject confirmation gate', () => {
    const IMPACT: SkillGraphBatchRejectConfirmationRequired = {
      requiresConfirmation: true,
      impactedApprovedCount: 1,
      impactedTotalLinkedModules: 2,
      impactedNodes: [{ id: 'n1', title: 'Present simple', linkedModuleCount: 2 }],
    };

    it('opens the confirmation modal instead of mutating when the API reports requiresConfirmation', async () => {
      await setup({ batchRejectSkillGraphNodes: jasmine.createSpy().and.returnValue(of(IMPACT)) });
      component.selectedIds.set(new Set(['n1']));
      component.rejectReason = 'Too broad.';
      component.batchReject();

      expect(component.pendingRejectConfirmation()).toEqual(IMPACT);
      expect(component.batchStatus()).toBe('');
      // Selection/reason are preserved so the admin can adjust and retry.
      expect(component.hasSelection()).toBeTrue();
    });

    it('confirmBatchReject resubmits with confirm:true and completes the reject', async () => {
      const spy = jasmine.createSpy().and.returnValues(
        of(IMPACT),
        of<SkillGraphBatchRejectResponse>({ requestedCount: 1, succeeded: 1, failed: 0, limitReached: false, edgesRemoved: 0, reconnectSuggestions: [] }),
      );
      await setup({ batchRejectSkillGraphNodes: spy });
      component.selectedIds.set(new Set(['n1']));
      component.rejectReason = 'Too broad.';
      component.batchReject();
      expect(component.pendingRejectConfirmation()).toBeTruthy();

      component.confirmBatchReject();
      expect(spy).toHaveBeenCalledWith(['n1'], 'Too broad.', true);
      expect(component.pendingRejectConfirmation()).toBeNull();
      expect(component.batchStatus()).toContain('Rejected 1 of 1');
      expect(component.hasSelection()).toBeFalse();
    });

    it('cancelBatchReject closes the modal without calling the API again and keeps the selection', async () => {
      await setup({ batchRejectSkillGraphNodes: jasmine.createSpy().and.returnValue(of(IMPACT)) });
      component.selectedIds.set(new Set(['n1']));
      component.rejectReason = 'Too broad.';
      component.batchReject();
      expect(component.pendingRejectConfirmation()).toBeTruthy();

      component.cancelBatchReject();
      expect(component.pendingRejectConfirmation()).toBeNull();
      expect(api.batchRejectSkillGraphNodes).toHaveBeenCalledTimes(1);
      expect(component.hasSelection()).toBeTrue();
    });
  });

  it('selectedApprovedCount reflects Approved nodes among the current selection', async () => {
    await setup({
      getSkillGraphNodes: jasmine.createSpy().and.returnValue(of<SkillGraphNodeListResponse>({
        items: [
          { ...NODES.items[0], id: 'n1', reviewStatus: 'PendingReview' },
          { ...NODES.items[0], id: 'n2', reviewStatus: 'Approved' },
        ],
        totalCount: 2, totalPages: 1, page: 1, pageSize: 25,
      })),
    });
    component.selectedIds.set(new Set(['n1', 'n2']));
    expect(component.selectedApprovedCount()).toBe(1);
  });

  // User correction (2026-07-24) — the tag-issues banner, isolated-nodes banner, and the merged
  // "Graph audit" card all moved to their own page; this list page now just links there.
  it('goToAuditPage navigates to the audit route', async () => {
    await setup();
    const router = TestBed.inject(Router);
    const navSpy = spyOn(router, 'navigateByUrl');
    component.goToAuditPage();
    expect(navSpy).toHaveBeenCalledWith('/admin/skill-graph/audit');
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

});
