import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { Router } from '@angular/router';
import { PlacementCardsComponent } from './placement-cards.component';
import { PlacementService } from '../../../../core/services/placement.service';
import { AdaptivePlacementSummary, PlacementSkillStatus } from '../../../../core/models/placement.models';

function makeSummary(partial: Partial<AdaptivePlacementSummary> = {}): AdaptivePlacementSummary {
  return {
    assessmentId: 'assess-1',
    studentProfileId: 'profile-1',
    status: 'InProgress',
    startedAtUtc: '2026-01-01T00:00:00Z',
    completedAtUtc: null as any,
    expiredAtUtc: null as any,
    overallCefrLevel: null as any,
    overallConfidence: 0,
    isProvisional: false,
    resultSummary: null as any,
    source: 'adaptive',
    skillResults: [],
    learningPlanRegenerated: false,
    learningPlanRegenerationWarning: null as any,
    itemCount: 0,
    hasPlacement: true,
    ...partial,
  };
}

const partialSkills: PlacementSkillStatus[] = [
  { skill: 'grammar', label: 'Grammar', percentComplete: 40, completed: false, evidenceCount: 2 },
  { skill: 'listening', label: 'Listening', percentComplete: 100, completed: true, evidenceCount: 4 },
];

describe('PlacementCardsComponent', () => {
  function setup(overrides: Partial<PlacementService> = {}) {
    const svc: Partial<PlacementService> = {
      getAdaptiveCurrent: () => of({ hasPlacement: false } as any),
      getSkillStatus: jasmine.createSpy('getSkillStatus').and.returnValue(of(partialSkills)),
      completeAdaptive: jasmine.createSpy('completeAdaptive'),
      ...overrides,
    };

    TestBed.configureTestingModule({
      imports: [PlacementCardsComponent],
      providers: [
        { provide: PlacementService, useValue: svc },
        { provide: Router, useValue: { navigate: jasmine.createSpy('navigate') } },
      ],
    });

    const fixture = TestBed.createComponent(PlacementCardsComponent);
    return { fixture, router: TestBed.inject(Router) as any };
  }

  it('shows cards with per-skill percent/completed state', () => {
    const { fixture } = setup();
    fixture.detectChanges();
    expect(fixture.componentInstance.state()).toBe('cards');
    expect(fixture.componentInstance.skills()).toEqual(partialSkills);
    expect(fixture.componentInstance.allCompleted()).toBeFalse();
  });

  it('shows the done screen directly when the assessment is already Completed', () => {
    const completed = makeSummary({ status: 'Completed', overallCefrLevel: 'B1' });
    const { fixture } = setup({ getAdaptiveCurrent: () => of(completed) });
    fixture.detectChanges();
    expect(fixture.componentInstance.state()).toBe('done');
    expect(fixture.componentInstance.result()?.overallCefrLevel).toBe('B1');
  });

  it('openCard() navigates to /placement/:skill for an incomplete card', () => {
    const { fixture, router } = setup();
    fixture.detectChanges();
    fixture.componentInstance.openCard(partialSkills[0]);
    expect(router.navigate).toHaveBeenCalledWith(['/placement', 'grammar']);
  });

  it('openCard() does nothing for a completed card', () => {
    const { fixture, router } = setup();
    fixture.detectChanges();
    fixture.componentInstance.openCard(partialSkills[1]);
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('allCompleted() is true only when every skill is completed', () => {
    const allDone: PlacementSkillStatus[] = partialSkills.map(s => ({ ...s, completed: true, percentComplete: 100 }));
    const { fixture } = setup({ getSkillStatus: () => of(allDone) });
    fixture.detectChanges();
    expect(fixture.componentInstance.allCompleted()).toBeTrue();
  });

  it('finish() calls completeAdaptive and shows the done screen', () => {
    const allDone: PlacementSkillStatus[] = partialSkills.map(s => ({ ...s, completed: true, percentComplete: 100 }));
    const completeAdaptive = jasmine.createSpy('completeAdaptive').and.returnValue(of(makeSummary({ status: 'Completed', overallCefrLevel: 'B2' })));
    const { fixture } = setup({
      getAdaptiveCurrent: () => of(makeSummary()),
      getSkillStatus: () => of(allDone),
      completeAdaptive,
    });
    fixture.detectChanges();
    fixture.componentInstance.finish();

    expect(completeAdaptive).toHaveBeenCalledWith('assess-1');
    expect(fixture.componentInstance.state()).toBe('done');
    expect(fixture.componentInstance.result()?.overallCefrLevel).toBe('B2');
  });

  it('shows an error state when loading skills fails', () => {
    const { fixture } = setup({ getSkillStatus: () => throwError(() => new Error('network')) });
    fixture.detectChanges();
    expect(fixture.componentInstance.state()).toBe('error');
  });

  it('continueToDashboard() navigates to /dashboard', () => {
    const { fixture, router } = setup();
    fixture.detectChanges();
    fixture.componentInstance.continueToDashboard();
    expect(router.navigate).toHaveBeenCalledWith(['/dashboard']);
  });
});
