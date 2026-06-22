import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { Router } from '@angular/router';
import { PlacementComponent } from './placement.component';
import { PlacementService } from '../../../core/services/placement.service';
import {
  PlacementStatus, PlacementCurrentSection, PlacementResult,
} from '../../../core/models/placement.models';

function status(partial: Partial<PlacementStatus>): PlacementStatus {
  return {
    status: 'NotStarted',
    currentSectionKey: 'self_check',
    currentSectionOrder: 1,
    totalSections: 6,
    lifecycleStage: 'PlacementRequired',
    isCompleted: false,
    ...partial,
  };
}

const selfCheckSection: PlacementCurrentSection = {
  status: 'InProgress',
  currentSectionOrder: 1,
  totalSections: 6,
  isCompleted: false,
  section: {
    key: 'self_check',
    order: 1,
    title: 'Quick self-check',
    instructions: 'Tell us how confident you feel.',
    sectionType: 'self_check',
    scored: false,
    questions: [
      { key: 'confidence_email', prompt: 'Confidence writing email?', type: 'rating' },
    ],
    passage: null, audioScript: null, writingPrompt: null, speakingPrompt: null,
  },
};

const result: PlacementResult = {
  estimatedOverallLevel: 'B1',
  skillLevels: [{ skill: 'Grammar accuracy', level: 'B1' }],
  strengths: ['vocabulary'],
  weaknesses: ['formal tone'],
  recommendedStartingCourse: 'Workplace English B1',
  recommendedSessionDuration: 15,
  placementNotes: 'Solid B1.',
  isCompleted: true,
};

describe('PlacementComponent', () => {
  function setup(svc: Partial<PlacementService>) {
    TestBed.configureTestingModule({
      imports: [PlacementComponent],
      providers: [
        { provide: PlacementService, useValue: svc },
        { provide: Router, useValue: { navigate: jasmine.createSpy('navigate') } },
      ],
    });
    return TestBed.createComponent(PlacementComponent);
  }

  it('shows intro when placement not started', () => {
    const fixture = setup({ getStatus: () => of(status({ status: 'NotStarted' })) } as any);
    fixture.detectChanges();
    expect(fixture.componentInstance.state()).toBe('intro');
  });

  it('loads the current section when in progress', () => {
    const fixture = setup({
      getStatus: () => of(status({ status: 'InProgress', currentSectionOrder: 1 })),
      getCurrent: () => of(selfCheckSection),
    } as any);
    fixture.detectChanges();
    expect(fixture.componentInstance.state()).toBe('section');
    expect(fixture.componentInstance.section()?.key).toBe('self_check');
  });

  it('shows the result when placement is completed', () => {
    const fixture = setup({
      getStatus: () => of(status({ status: 'Completed', isCompleted: true, lifecycleStage: 'CourseReady' })),
      getResult: () => of(result),
    } as any);
    fixture.detectChanges();
    expect(fixture.componentInstance.state()).toBe('result');
    expect(fixture.componentInstance.result()?.estimatedOverallLevel).toBe('B1');
  });

  it('begin() starts placement and loads the first section', () => {
    const getCurrent = jasmine.createSpy('getCurrent').and.returnValue(of(selfCheckSection));
    const fixture = setup({
      getStatus: () => of(status({ status: 'NotStarted' })),
      start: () => of(status({ status: 'InProgress', currentSectionOrder: 1 })),
      getCurrent,
    } as any);
    fixture.detectChanges();
    fixture.componentInstance.begin();
    expect(getCurrent).toHaveBeenCalled();
    expect(fixture.componentInstance.state()).toBe('section');
  });
});


