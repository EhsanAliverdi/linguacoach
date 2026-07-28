import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { SpAdminNodeHierarchyModalComponent } from './sp-admin-node-hierarchy-modal.component';
import { AdminApiService } from '../../../../core/services/admin.api.service';
import { SkillGraphNodeDetail } from '../../../../core/models/admin.models';

const NODE: SkillGraphNodeDetail = {
  id: 'n1', key: 'grammar.to_be.a1', title: 'Verb to be', description: 'D',
  cefrLevel: 'A1', skill: 'grammar', subskill: null, difficultyBand: 1,
  reviewStatus: 'Approved', isActive: true, rejectionReason: null, createdAt: '2026-07-17T00:00:00Z',
  contextTags: [], focusTags: [], linkedModuleCount: 0,
  parentNodeId: null, childCount: 2,
  descriptionForAi: null, reviewedByUserId: null, approvedAtUtc: null, rejectedAtUtc: null,
  prerequisites: [], dependents: [], linkedModules: [],
  parent: null,
  children: [
    { id: 'c1', key: 'grammar.i_am.a1', title: 'I am', reviewStatus: 'PendingReview' },
    { id: 'c2', key: 'grammar.i_am_not.a1', title: 'I am not', reviewStatus: 'PendingReview' },
  ],
};

@Component({
  standalone: true,
  imports: [SpAdminNodeHierarchyModalComponent],
  template: `<sp-admin-node-hierarchy-modal [nodeId]="nodeId" (closed)="closedCount = closedCount + 1" (viewFullPage)="viewedId = $event" />`,
})
class HostComponent {
  nodeId: string | null = null;
  closedCount = 0;
  viewedId: string | null = null;
}

describe('SpAdminNodeHierarchyModalComponent', () => {
  let fixture: ComponentFixture<HostComponent>;
  let host: HostComponent;
  let api: { getSkillGraphNode: jasmine.Spy };

  async function setup(response = of(NODE)) {
    api = { getSkillGraphNode: jasmine.createSpy('getSkillGraphNode').and.returnValue(response) };
    await TestBed.configureTestingModule({
      imports: [HostComponent],
      providers: [{ provide: AdminApiService, useValue: api }],
    }).compileComponents();
    fixture = TestBed.createComponent(HostComponent);
    host = fixture.componentInstance;
  }

  it('does not fetch when nodeId is null', async () => {
    await setup();
    fixture.detectChanges();
    expect(api.getSkillGraphNode).not.toHaveBeenCalled();
  });

  it('fetches the node when nodeId is set', async () => {
    await setup();
    host.nodeId = 'n1';
    fixture.detectChanges();
    expect(api.getSkillGraphNode).toHaveBeenCalledWith('n1');
  });

  it('re-fetches when nodeId changes to a different node', async () => {
    await setup();
    host.nodeId = 'n1';
    fixture.detectChanges();
    host.nodeId = 'n2';
    fixture.detectChanges();
    expect(api.getSkillGraphNode).toHaveBeenCalledTimes(2);
    expect(api.getSkillGraphNode).toHaveBeenCalledWith('n2');
  });

  it('renders the node title, children, and their review statuses once loaded', async () => {
    await setup();
    host.nodeId = 'n1';
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Verb to be');
    expect(text).toContain('I am');
    expect(text).toContain('I am not');
    expect(text).toContain('2 leaf child node(s)');
  });

  it('shows an error message when the fetch fails', async () => {
    await setup(throwError(() => ({ error: { error: 'Not found' } })));
    host.nodeId = 'n1';
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Not found');
  });

  it('emits closed when the Close button is clicked', async () => {
    await setup();
    host.nodeId = 'n1';
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const buttons = Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[];
    const closeButton = buttons.find(b => b.textContent?.trim() === 'Close');
    closeButton!.click();

    expect(host.closedCount).toBe(1);
  });

  it('emits viewFullPage with the current nodeId when "View full page" is clicked', async () => {
    await setup();
    host.nodeId = 'n1';
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const buttons = Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[];
    const viewButton = buttons.find(b => b.textContent?.trim() === 'View full page');
    viewButton!.click();

    expect(host.viewedId).toBe('n1');
  });
});
