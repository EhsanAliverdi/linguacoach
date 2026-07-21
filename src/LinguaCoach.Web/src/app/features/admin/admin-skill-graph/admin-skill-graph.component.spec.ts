import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AdminSkillGraphComponent } from './admin-skill-graph.component';
import { AdminApiService } from '../../../core/services/admin.api.service';
import {
  SkillGraphTaxonomy,
  SkillGraphNodeListResponse,
  SkillGraphCoverageResponse,
  SkillGraphDraftResponse,
  SkillGraphBatchActionResponse,
} from '../../../core/models/admin.models';

// Adaptive Curriculum Sprint 1 — admin skill-graph review page.
// See docs/architecture/adaptive-curriculum-skill-graph.md.

const TAXONOMY: SkillGraphTaxonomy = {
  cefrLevels: ['A1', 'A2', 'B1', 'B2', 'C1', 'C2'],
  skills: ['grammar', 'vocabulary'],
  subskillsBySkill: { grammar: ['grammar.tense_aspect'], vocabulary: ['vocabulary.receptive'] },
};

const NODES: SkillGraphNodeListResponse = {
  items: [
    {
      id: 'n1', key: 'grammar.present_simple.a1', title: 'Present simple', description: 'D',
      cefrLevel: 'A1', skill: 'grammar', subskill: null, difficultyBand: 1,
      reviewStatus: 'PendingReview', isActive: true, rejectionReason: null, createdAt: '2026-07-17T00:00:00Z',
      contextTags: [], focusTags: [],
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
      of<SkillGraphBatchActionResponse>({ requestedCount: 1, succeeded: 1, failed: 0, limitReached: false })),
    getSkillGraphContentCoverage: jasmine.createSpy('getSkillGraphContentCoverage').and.returnValue(
      of({ totalApprovedNodes: 0, nodesWithContent: 0, nodesWithoutContentCount: 0, nodes: [] })),
    getSkillGraphNodeIssuesSummary: jasmine.createSpy('getSkillGraphNodeIssuesSummary').and.returnValue(
      of({ totalItems: 0, itemsWithIssues: 0 })),
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
      providers: [provideRouter([]), { provide: AdminApiService, useValue: api }],
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

  it('toggleSelected tracks selected node ids', async () => {
    await setup();
    expect(component.hasSelection()).toBeFalse();
    component.toggleSelected('n1', true);
    expect(component.hasSelection()).toBeTrue();
    expect(component.isSelected('n1')).toBeTrue();
    component.toggleSelected('n1', false);
    expect(component.hasSelection()).toBeFalse();
  });

  it('batchApprove calls the API with selected ids and clears selection', async () => {
    await setup();
    component.toggleSelected('n1', true);
    component.batchApprove();
    expect(api.batchApproveSkillGraphNodes).toHaveBeenCalledWith(['n1']);
    expect(component.hasSelection()).toBeFalse();
    expect(component.batchStatus()).toContain('Approved 1 of 1');
  });

  it('batchReject requires a reason', async () => {
    await setup();
    component.toggleSelected('n1', true);
    component.rejectReason = '';
    component.batchReject();
    expect(component.batchError()).toBeTruthy();
    expect(api.batchRejectSkillGraphNodes).not.toHaveBeenCalled();
  });

  it('batchReject calls the API when a reason is set', async () => {
    await setup();
    component.toggleSelected('n1', true);
    component.rejectReason = 'Too broad.';
    component.batchReject();
    expect(api.batchRejectSkillGraphNodes).toHaveBeenCalledWith(['n1'], 'Too broad.');
    expect(component.batchStatus()).toContain('Rejected 1 of 1');
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
});
