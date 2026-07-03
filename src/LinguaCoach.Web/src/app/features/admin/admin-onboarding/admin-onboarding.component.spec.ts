import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AdminOnboardingComponent } from './admin-onboarding.component';
import { AdminOnboardingService } from '../../../core/services/admin-onboarding.service';
import {
  AdminOnboardingFlowSummary,
  AdminOnboardingFlowDto,
  AdminOnboardingStepDto,
} from '../../../core/models/admin-onboarding.models';

const STEP_A: AdminOnboardingStepDto = {
  stepKey: 'welcome',
  title: 'Welcome',
  description: null,
  stepType: 'Welcome',
  requirementType: 'SystemRequired',
  answerMapping: 'None',
  stepOrder: 1,
  isEnabled: true,
  options: null,
};

const STEP_B: AdminOnboardingStepDto = {
  stepKey: 'preferred_name',
  title: 'Your Name',
  description: 'Tell us your name',
  stepType: 'PreferredName',
  requirementType: 'AdminConfigured',
  answerMapping: 'PreferredName',
  stepOrder: 2,
  isEnabled: false,
  options: null,
};

const ACTIVE_FLOW: AdminOnboardingFlowDto = {
  flowId: 'flow-1',
  name: 'Default Flow',
  version: 1,
  isActive: true,
  steps: [STEP_A, STEP_B],
};

const FLOW_SUMMARY_ACTIVE: AdminOnboardingFlowSummary = {
  flowId: 'flow-1',
  name: 'Default Flow',
  version: 1,
  isActive: true,
  totalSteps: 2,
  requiredSteps: 1,
  createdAt: '2026-01-01T00:00:00Z',
};

const FLOW_SUMMARY_INACTIVE: AdminOnboardingFlowSummary = {
  flowId: 'flow-2',
  name: 'Draft Flow',
  version: 2,
  isActive: false,
  totalSteps: 0,
  requiredSteps: 0,
  createdAt: '2026-02-01T00:00:00Z',
};

function makeService(
  flows: AdminOnboardingFlowSummary[] = [FLOW_SUMMARY_ACTIVE],
  activeFlow: AdminOnboardingFlowDto | null = ACTIVE_FLOW,
) {
  return {
    listFlows: jasmine.createSpy('listFlows').and.returnValue(of(flows)),
    getActiveFlow: jasmine.createSpy('getActiveFlow').and.returnValue(
      activeFlow ? of(activeFlow) : throwError(() => ({ error: { error: 'Not found' } })),
    ),
    addStep: jasmine.createSpy('addStep').and.returnValue(of(STEP_A)),
    updateStep: jasmine.createSpy('updateStep').and.returnValue(of(STEP_A)),
    removeStep: jasmine.createSpy('removeStep').and.returnValue(of(void 0)),
    activateFlow: jasmine.createSpy('activateFlow').and.returnValue(of(void 0)),
  };
}

describe('AdminOnboardingComponent', () => {
  let fixture: ComponentFixture<AdminOnboardingComponent>;
  let component: AdminOnboardingComponent;
  let svc: ReturnType<typeof makeService>;

  async function setup(
    flows: AdminOnboardingFlowSummary[] = [FLOW_SUMMARY_ACTIVE],
    activeFlow: AdminOnboardingFlowDto | null = ACTIVE_FLOW,
  ) {
    svc = makeService(flows, activeFlow);
    await TestBed.configureTestingModule({
      imports: [AdminOnboardingComponent],
      providers: [{ provide: AdminOnboardingService, useValue: svc }],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminOnboardingComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('renders the page heading', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('Onboarding');
  });

  it('loading signal starts true before data arrives', () => {
    svc = makeService();
    TestBed.configureTestingModule({
      imports: [AdminOnboardingComponent],
      providers: [{ provide: AdminOnboardingService, useValue: svc }],
    });
    fixture = TestBed.createComponent(AdminOnboardingComponent);
    component = fixture.componentInstance;
    expect(component.loading()).toBeTrue();
  });

  it('calls listFlows on init', async () => {
    await setup();
    expect(svc.listFlows).toHaveBeenCalledTimes(1);
  });

  it('calls getActiveFlow when active flow exists in list', async () => {
    await setup();
    expect(svc.getActiveFlow).toHaveBeenCalledTimes(1);
  });

  it('does not call getActiveFlow when no active flow in list', async () => {
    await setup([FLOW_SUMMARY_INACTIVE], null);
    expect(svc.getActiveFlow).not.toHaveBeenCalled();
  });

  it('populates flows signal after load', async () => {
    await setup([FLOW_SUMMARY_ACTIVE, FLOW_SUMMARY_INACTIVE]);
    expect(component.flows().length).toBe(2);
  });

  it('populates activeFlow signal when active flow exists', async () => {
    await setup();
    expect(component.activeFlow()).not.toBeNull();
    expect(component.activeFlow()!.flowId).toBe('flow-1');
  });

  it('loading is false after data arrives', async () => {
    await setup();
    expect(component.loading()).toBeFalse();
  });

  it('shows error state when listFlows fails', async () => {
    svc = makeService();
    svc.listFlows.and.returnValue(throwError(() => ({ error: { error: 'Server error' } })));
    await TestBed.configureTestingModule({
      imports: [AdminOnboardingComponent],
      providers: [{ provide: AdminOnboardingService, useValue: svc }],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminOnboardingComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(component.error()).toBeTruthy();
  });

  // ── KPI computed signals ──────────────────────────────────────────────────

  it('totalSteps computed reflects active flow step count', async () => {
    await setup();
    expect(component.totalSteps()).toBe(2);
  });

  it('enabledSteps computed counts only enabled steps', async () => {
    await setup();
    expect(component.enabledSteps()).toBe(1);
  });

  it('totalSteps is 0 when no active flow', async () => {
    await setup([FLOW_SUMMARY_INACTIVE], null);
    expect(component.totalSteps()).toBe(0);
  });

  it('activeFlowSummary returns active flow from list', async () => {
    await setup([FLOW_SUMMARY_ACTIVE, FLOW_SUMMARY_INACTIVE]);
    expect(component.activeFlowSummary()?.flowId).toBe('flow-1');
  });

  it('activeFlowSummary returns null when no active flow in list', async () => {
    await setup([FLOW_SUMMARY_INACTIVE], null);
    expect(component.activeFlowSummary()).toBeNull();
  });

  // ── KPI cards ──────────────────────────────────────────────────────────────

  it('renders KPI cards', async () => {
    await setup();
    const cards = (fixture.nativeElement as HTMLElement).querySelectorAll('sp-admin-kpi-card');
    expect(cards.length).toBeGreaterThanOrEqual(2);
  });

  // ── Step table ──────────────────────────────────────────────────────────────

  it('renders step table when active flow has steps', async () => {
    await setup();
    const table = (fixture.nativeElement as HTMLElement).querySelector('sp-admin-table');
    expect(table).toBeTruthy();
  });

  it('renders step titles in page content', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('Welcome');
  });

  // ── Slide-over ──────────────────────────────────────────────────────────────

  it('slideOverOpen is false initially', async () => {
    await setup();
    expect(component.slideOverOpen()).toBeFalse();
  });

  it('openAddStep opens slide-over with null editingStep', async () => {
    await setup();
    component.openAddStep();
    expect(component.slideOverOpen()).toBeTrue();
    expect(component.editingStep()).toBeNull();
  });

  it('openAddStep resets stepForm to defaults', async () => {
    await setup();
    component.openAddStep();
    expect(component.stepForm.stepKey).toBe('');
    expect(component.stepForm.isEnabled).toBeTrue();
    expect(component.stepForm.stepType).toBe('Welcome');
  });

  it('openEditStep opens slide-over with selected step', async () => {
    await setup();
    component.openEditStep(STEP_B);
    expect(component.slideOverOpen()).toBeTrue();
    expect(component.editingStep()).toBe(STEP_B);
  });

  it('openEditStep populates stepForm from selected step', async () => {
    await setup();
    component.openEditStep(STEP_B);
    expect(component.stepForm.stepKey).toBe('preferred_name');
    expect(component.stepForm.title).toBe('Your Name');
    expect(component.stepForm.isEnabled).toBeFalse();
  });

  it('closeSlideOver closes slide-over and clears editingStep', async () => {
    await setup();
    component.openEditStep(STEP_A);
    component.closeSlideOver();
    expect(component.slideOverOpen()).toBeFalse();
    expect(component.editingStep()).toBeNull();
  });

  // ── Save step ──────────────────────────────────────────────────────────────

  it('saveStep calls addStep when editingStep is null', fakeAsync(async () => {
    await setup();
    component.openAddStep();
    component.stepForm.stepKey = 'new-step';
    component.stepForm.title = 'New Step';
    component.saveStep();
    tick();
    expect(svc.addStep).toHaveBeenCalledWith('flow-1', jasmine.objectContaining(component.stepForm));
  }));

  it('saveStep calls updateStep when editingStep is set', fakeAsync(async () => {
    await setup();
    component.openEditStep(STEP_B);
    component.saveStep();
    tick();
    expect(svc.updateStep).toHaveBeenCalledWith('flow-1', 'preferred_name', jasmine.objectContaining(component.stepForm));
  }));

  it('saveStep closes slide-over on success', fakeAsync(async () => {
    await setup();
    component.openAddStep();
    component.saveStep();
    tick();
    expect(component.slideOverOpen()).toBeFalse();
  }));

  it('saveStep sets actionSuccess on success', fakeAsync(async () => {
    await setup();
    component.openAddStep();
    component.saveStep();
    tick();
    expect(component.actionSuccess()).toBe('Step added.');
  }));

  it('saveStep sets actionError on failure', fakeAsync(async () => {
    await setup();
    svc.addStep.and.returnValue(throwError(() => ({ error: { error: 'Validation failed' } })));
    component.openAddStep();
    component.saveStep();
    tick();
    expect(component.actionError()).toContain('Validation failed');
  }));

  it('saveStep does nothing when no activeFlow', async () => {
    await setup([FLOW_SUMMARY_INACTIVE], null);
    component.openAddStep();
    component.saveStep();
    expect(svc.addStep).not.toHaveBeenCalled();
  });

  // ── Remove step ────────────────────────────────────────────────────────────

  it('removeStep calls service with correct flow and step key', fakeAsync(async () => {
    await setup();
    component.removeStep(STEP_A);
    tick();
    expect(svc.removeStep).toHaveBeenCalledWith('flow-1', 'welcome');
  }));

  it('removeStep sets actionSuccess on success', fakeAsync(async () => {
    await setup();
    component.removeStep(STEP_A);
    tick();
    expect(component.actionSuccess()).toBe('Step removed.');
  }));

  // ── Activate flow ──────────────────────────────────────────────────────────

  it('activateFlow calls service with flowId', fakeAsync(async () => {
    await setup([FLOW_SUMMARY_INACTIVE], null);
    component.activateFlow('flow-2');
    tick();
    expect(svc.activateFlow).toHaveBeenCalledWith('flow-2');
  }));

  it('activateFlow sets actionSuccess on success', fakeAsync(async () => {
    await setup([FLOW_SUMMARY_INACTIVE], null);
    component.activateFlow('flow-2');
    tick();
    expect(component.actionSuccess()).toBe('Flow activated.');
  }));

  it('activateFlow sets actionError on failure', fakeAsync(async () => {
    await setup();
    svc.activateFlow.and.returnValue(throwError(() => ({ error: { error: 'Cannot activate' } })));
    component.activateFlow('flow-1');
    tick();
    expect(component.actionError()).toContain('Cannot activate');
  }));

  // ── stepTone helper ────────────────────────────────────────────────────────

  it('stepTone returns success for enabled step', async () => {
    await setup();
    expect(component.stepTone(STEP_A)).toBe('success');
  });

  it('stepTone returns neutral for disabled step', async () => {
    await setup();
    expect(component.stepTone(STEP_B)).toBe('neutral');
  });

  // ── Empty state ────────────────────────────────────────────────────────────

  it('activeFlow is null when no active flow in list', async () => {
    await setup([FLOW_SUMMARY_INACTIVE], null);
    expect(component.activeFlow()).toBeNull();
  });
});
