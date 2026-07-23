import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AdminSkillGraphAuditComponent } from './admin-skill-graph-audit.component';
import { AdminApiService } from '../../../../core/services/admin.api.service';
import {
  GraphChangeSuggestionsResponse,
  NearDuplicateSuggestionsResponse,
  NearDuplicateNodeSuggestion,
  ConfirmNearDuplicateResponse,
  MergeNodesResponse,
  SkillGraphIsolatedNodesResponse,
} from '../../../../core/models/admin.models';

// Skill Graph rebuild — dedicated audit page (2026-07-24 user correction): the tag-issues banner,
// isolated-nodes banner, and the merged "Graph audit" (redundant edges + near-duplicate nodes)
// card moved here from the main Skill Graph list page.

function makeApi(overrides: Partial<Record<string, unknown>> = {}) {
  return {
    getSkillGraphNodeIssuesSummary: jasmine.createSpy('getSkillGraphNodeIssuesSummary').and.returnValue(
      of({ totalItems: 0, itemsWithIssues: 0 })),
    listSkillGraphNodesWithIssues: jasmine.createSpy('listSkillGraphNodesWithIssues').and.returnValue(of([])),
    repairSkillGraphNode: jasmine.createSpy('repairSkillGraphNode').and.returnValue(of({})),
    getIsolatedSkillGraphNodes: jasmine.createSpy('getIsolatedSkillGraphNodes').and.returnValue(
      of<SkillGraphIsolatedNodesResponse>({ isolatedCount: 0, isolated: [] })),
    getRedundantEdgeSuggestions: jasmine.createSpy('getRedundantEdgeSuggestions').and.returnValue(
      of<GraphChangeSuggestionsResponse>({ count: 0, suggestions: [] })),
    getNearDuplicateSuggestions: jasmine.createSpy('getNearDuplicateSuggestions').and.returnValue(
      of<NearDuplicateSuggestionsResponse>({ count: 0, suggestions: [] })),
    mergeSkillGraphNodes: jasmine.createSpy('mergeSkillGraphNodes').and.returnValue(
      of<MergeNodesResponse>({ keepNodeId: 'n1', mergeAwayNodeId: 'n2', repointedCount: 0, droppedCount: 0 })),
    confirmNearDuplicate: jasmine.createSpy('confirmNearDuplicate').and.returnValue(
      of<ConfirmNearDuplicateResponse>({ success: true, isLikelyDuplicate: false, reasoning: 'Different content.', error: null })),
    removeSkillGraphPrerequisite: jasmine.createSpy('removeSkillGraphPrerequisite').and.returnValue(of({ removed: true })),
    ...overrides,
  };
}

describe('AdminSkillGraphAuditComponent', () => {
  let fixture: ComponentFixture<AdminSkillGraphAuditComponent>;
  let component: AdminSkillGraphAuditComponent;
  let api: ReturnType<typeof makeApi>;

  async function setup(overrides: Partial<Record<string, unknown>> = {}) {
    api = makeApi(overrides);
    await TestBed.configureTestingModule({
      imports: [AdminSkillGraphAuditComponent],
      providers: [provideRouter([]), provideHttpClient(), { provide: AdminApiService, useValue: api }],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminSkillGraphAuditComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('renders the audit page and loads tag-issue summary + isolated nodes on init', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('Skill Graph Audit');
    expect(api.getSkillGraphNodeIssuesSummary).toHaveBeenCalledTimes(1);
    expect(api.listSkillGraphNodesWithIssues).toHaveBeenCalledTimes(1);
    expect(api.getIsolatedSkillGraphNodes).toHaveBeenCalledTimes(1);
  });

  it('back() navigates via Location.back()', async () => {
    await setup();
    const spy = spyOn((component as any).location, 'back');
    component.back();
    expect(spy).toHaveBeenCalled();
  });

  it('editIsolatedNode navigates to the node Edit route', async () => {
    await setup();
    const router = TestBed.inject(Router);
    const navSpy = spyOn(router, 'navigateByUrl');
    component.editIsolatedNode({ id: 'n1', key: 'k', title: 'T', cefrLevel: 'A1', skill: 'grammar', reviewStatus: 'PendingReview' });
    expect(navSpy).toHaveBeenCalledWith('/admin/skill-graph/nodes/n1/edit');
  });

  it('fixOneNodeWithAi repairs a single node and reloads the summary/list', async () => {
    await setup();
    component.fixOneNodeWithAi({ id: 'n1', title: 'T' });
    expect(api.repairSkillGraphNode).toHaveBeenCalledWith('n1');
    expect(api.getSkillGraphNodeIssuesSummary).toHaveBeenCalledTimes(2);
    expect(api.listSkillGraphNodesWithIssues).toHaveBeenCalledTimes(2);
  });

  it('missingTagsSearch filters the with-issues list client-side', async () => {
    await setup({
      listSkillGraphNodesWithIssues: jasmine.createSpy('listSkillGraphNodesWithIssues').and.returnValue(
        of([{ id: 'n1', title: 'Present simple' }, { id: 'n2', title: 'Past continuous' }])),
    });
    component.missingTagsSearch.set('present');
    expect(component.filteredNodesWithIssues().length).toBe(1);
    expect(component.filteredNodesWithIssues()[0].title).toBe('Present simple');
  });

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

  it('dismissNearDuplicateSuggestion clears AI results, expanded state, and any pending merge for that pair', async () => {
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

  it('isDuplicateRowExpanded is true once toggled, and toggling again collapses it', async () => {
    const dup: NearDuplicateNodeSuggestion = {
      nodeAId: 'n1', nodeATitle: 'A', nodeADescription: 'Desc A',
      nodeBId: 'n2', nodeBTitle: 'B', nodeBDescription: 'Desc B',
      cefrLevel: 'A1', skill: 'grammar', similarity: 0.9,
    };
    await setup();
    expect(component.isDuplicateRowExpanded(dup)).toBeFalse();

    component.toggleDuplicateExpanded(dup);
    expect(component.isDuplicateRowExpanded(dup)).toBeTrue();

    component.toggleDuplicateExpanded(dup);
    expect(component.isDuplicateRowExpanded(dup)).toBeFalse();
  });

  it('isDuplicateRowExpanded stays true while an AI result or pending merge exists, even without a manual toggle', async () => {
    const dup: NearDuplicateNodeSuggestion = {
      nodeAId: 'n1', nodeATitle: 'A', nodeADescription: 'Desc A',
      nodeBId: 'n2', nodeBTitle: 'B', nodeBDescription: 'Desc B',
      cefrLevel: 'A1', skill: 'grammar', similarity: 0.9,
    };
    await setup();
    component.nearDuplicateSuggestions.set([dup]);
    component.confirmDuplicateWithAi(dup);
    expect(component.isDuplicateRowExpanded(dup)).toBeTrue();
  });
});
