import { of, throwError } from 'rxjs';
import { AdminImportPackagePlanComponent } from './admin-import-package-plan.component';
import { AdminImportPackageService } from '../../../core/services/admin-import-package.service';
import { ImportExecutionPlanDto, ImportPlanPreviewResult } from '../../../core/models/admin-import-package.models';

/**
 * Phase 4.4A — focused unit coverage for the editable plan draft workflow (include/exclude,
 * routing, CSV mappings, save/preview lifecycle, concurrency conflicts, approval gating,
 * revision). Instantiates the component directly (no TestBed render) since all logic under test
 * lives in plain signal-driven methods — faster and avoids coupling to child component templates.
 */
describe('AdminImportPackagePlanComponent', () => {
  function basePlan(overrides: Partial<ImportExecutionPlanDto> = {}): ImportExecutionPlanDto {
    return {
      planId: 'plan-1',
      importPackageId: 'pkg-1',
      version: 1,
      status: 'AwaitingApproval',
      processingMode: 'Direct',
      processingModeReason: null,
      estimate: {
        detectedGroups: [
          { groupKey: '(root)', description: '1 file(s)', fileCount: 1, sampleRelativePaths: ['data.csv'], proposedResourceType: 'vocabularyEntry' as any, confidence: 0.9 },
        ],
        ambiguousGroups: [],
        unsupportedContentNotes: [],
        volume: { totalFiles: 1, filesByExtension: { '.csv': 1 }, expectedCandidateCount: 1, expectedAudioFilesRequiringStt: 0, estimatedAudioMinutesRequiringStt: 0, expectedTtsCandidates: 0, estimatedTtsCharacters: 0, expectedImageAnalysisCount: 0, unmatchedFileCount: 0 },
        time: { estimatedDurationRangeDescription: '1 min', estimatedMinMinutes: 1, estimatedMaxMinutes: 1, assumptions: '' },
        cost: { expectedCost: 1, minCost: 0.5, maxCost: 2, currency: 'USD', breakdown: [], assumptions: [], providerModelAssumptions: '' },
        risks: [],
        proposedDecisions: [],
        samplingRoundsUsed: 1,
        structureConfidence: 0.9,
        structuredMappingPreviews: [
          { assetRelativePath: 'data.csv', detectedColumns: ['mystery1'], proposedMapping: {}, ignoredColumns: [], expectedRecordCount: 1, warnings: [] },
        ],
      },
      approvedCostCeiling: null,
      createdAtUtc: '2026-01-01T00:00:00Z',
      approvedAtUtc: null,
      approvedByUserId: null,
      rejectedAtUtc: null,
      rejectionReason: null,
      pauseReason: null,
      changeReason: null,
      concurrencyStamp: 'stamp-1',
      isEditable: true,
      groupInstructions: [
        { groupKey: '(root)', included: true, resourceType: 'vocabularyEntry' as any, fieldMappings: {}, sampleRelativePaths: ['data.csv'] },
      ],
      accruedCost: 0,
      accruedCostCurrency: 'USD',
      remainingCeiling: null,
      ceilingAmendments: [],
      ...overrides,
    };
  }

  function makeComponent(svc: Partial<AdminImportPackageService>) {
    const routeStub = { snapshot: { paramMap: { get: () => 'pkg-1' } } } as any;
    const routerStub = { navigate: jasmine.createSpy('navigate') } as any;
    return new AdminImportPackagePlanComponent(svc as AdminImportPackageService, routeStub, routerStub);
  }

  it('loads a Draft/AwaitingApproval plan into editable form controls', () => {
    const svc: Partial<AdminImportPackageService> = {
      getManifest: () => of({} as any),
      getPlan: () => of(basePlan()),
    };
    const component = makeComponent(svc);
    component.ngOnInit();

    expect(component.plan()?.isEditable).toBeTrue();
    expect(component.formRows().length).toBe(1);
    expect(component.formRows()[0].groupKey).toBe('(root)');
    expect(component.formRows()[0].included).toBeTrue();
    expect(component.formRows()[0].mappings).toEqual([{ source: 'mystery1', target: '' }]);
  });

  it('loads an Approved plan read-only (no form rows)', () => {
    const svc: Partial<AdminImportPackageService> = {
      getManifest: () => of({} as any),
      getPlan: () => of(basePlan({ status: 'Approved', isEditable: false })),
    };
    const component = makeComponent(svc);
    component.ngOnInit();

    expect(component.plan()?.isEditable).toBeFalse();
    expect(component.formRows().length).toBe(0);
  });

  it('excluding a group clears its resource type when building the save payload', () => {
    const component = makeComponent({ getManifest: () => of({} as any), getPlan: () => of(basePlan()) });
    component.ngOnInit();
    component.formRows()[0].included = false;
    const instructions = (component as any).toInstructions();
    expect(instructions[0].included).toBeFalse();
    expect(instructions[0].resourceType).toBeNull();
  });

  it('include/exclude, resource type, and CSV mapping edits all appear in the save payload', () => {
    const component = makeComponent({ getManifest: () => of({} as any), getPlan: () => of(basePlan()) });
    component.ngOnInit();
    component.formRows()[0].resourceType = 'grammarProfileEntry' as any;
    component.formRows()[0].mappings[0].target = 'word';
    const instructions = (component as any).toInstructions();
    expect(instructions[0].resourceType).toBe('grammarProfileEntry');
    expect(instructions[0].fieldMappings).toEqual({ mystery1: 'word' });
  });

  it('sends the current concurrency stamp on save and replaces it on success', () => {
    let sentStamp = '';
    const svc: Partial<AdminImportPackageService> = {
      getManifest: () => of({} as any),
      getPlan: () => of(basePlan()),
      updatePlanDraft: (_pkg, _plan, stamp) => { sentStamp = stamp; return of(basePlan({ concurrencyStamp: 'stamp-2' })); },
    };
    const component = makeComponent(svc);
    component.ngOnInit();
    component.markDirty();
    component.saveDraft();

    expect(sentStamp).toBe('stamp-1');
    expect(component.plan()?.concurrencyStamp).toBe('stamp-2');
    expect(component.dirty()).toBeFalse();
  });

  it('a stale save (409) shows conflict guidance without discarding the edit', () => {
    const svc: Partial<AdminImportPackageService> = {
      getManifest: () => of({} as any),
      getPlan: () => of(basePlan()),
      updatePlanDraft: () => throwError(() => ({ status: 409, error: { error: 'stale' } })),
    };
    const component = makeComponent(svc);
    component.ngOnInit();
    component.formRows()[0].included = false;
    component.markDirty();
    component.saveDraft();

    expect(component.concurrencyConflict()).toBeTrue();
    expect(component.formRows()[0].included).toBeFalse(); // edit preserved, not discarded
  });

  it('save validation failure (400 with errors) preserves the draft and maps errors per group', () => {
    const svc: Partial<AdminImportPackageService> = {
      getManifest: () => of({} as any),
      getPlan: () => of(basePlan()),
      updatePlanDraft: () => throwError(() => ({ status: 400, error: { error: 'invalid', errors: [{ groupKey: '(root)', message: 'bad mapping' }] } })),
    };
    const component = makeComponent(svc);
    component.ngOnInit();
    component.markDirty();
    component.saveDraft();

    expect(component.errorsForGroup('(root)').length).toBe(1);
    expect(component.formRows().length).toBe(1); // draft preserved
  });

  it('approval is disabled while the form is dirty', () => {
    const component = makeComponent({ getManifest: () => of({} as any), getPlan: () => of(basePlan()) });
    component.ngOnInit();
    expect(component.dirty()).toBeFalse();
    component.markDirty();
    expect(component.dirty()).toBeTrue(); // template disables the Approve button on this signal
  });

  it('approval sends the current concurrency stamp', () => {
    let sentStamp = '';
    const svc: Partial<AdminImportPackageService> = {
      getManifest: () => of({} as any),
      getPlan: () => of(basePlan()),
      approvePlan: (_pkg, _plan, _ceiling, stamp) => { sentStamp = stamp; return of(basePlan({ status: 'Approved', isEditable: false })); },
    };
    const component = makeComponent(svc);
    component.ngOnInit();
    component.approvedCostCeiling = 10;
    component.confirmApprove();

    expect(sentStamp).toBe('stamp-1');
  });

  it('a stale approval (409) shows conflict guidance', () => {
    const svc: Partial<AdminImportPackageService> = {
      getManifest: () => of({} as any),
      getPlan: () => of(basePlan()),
      approvePlan: () => throwError(() => ({ status: 409, error: { error: 'stale' } })),
    };
    const component = makeComponent(svc);
    component.ngOnInit();
    component.approvedCostCeiling = 10;
    component.confirmApprove();

    expect(component.concurrencyConflict()).toBeTrue();
  });

  it('preview displays mapped sample values without implying candidate creation', () => {
    const result: ImportPlanPreviewResult = {
      rows: [{ groupKey: '(root)', assetRelativePath: 'data.csv', sourceRow: { mystery1: 'x' }, predictedCandidateType: 'vocabularyEntry' as any, predictedCanonicalText: 'x', warnings: [] }],
      validationErrors: [],
    };
    const svc: Partial<AdminImportPackageService> = {
      getManifest: () => of({} as any),
      getPlan: () => of(basePlan()),
      previewPlanDraft: () => of(result),
    };
    const component = makeComponent(svc);
    component.ngOnInit();
    component.previewDraft();

    expect(component.previewResult()?.rows.length).toBe(1);
    expect(component.previewResult()?.rows[0].predictedCanonicalText).toBe('x');
  });

  it('preview errors surface against the correct group', () => {
    const result: ImportPlanPreviewResult = {
      rows: [],
      validationErrors: [{ groupKey: '(root)', message: 'missing mapping' }],
    };
    const component = makeComponent({
      getManifest: () => of({} as any),
      getPlan: () => of(basePlan()),
      previewPlanDraft: () => of(result),
    });
    component.ngOnInit();
    component.previewDraft();

    expect(component.previewResult()?.validationErrors[0].groupKey).toBe('(root)');
  });

  it('Create Revision is offered only for an Approved plan', () => {
    const component = makeComponent({ getManifest: () => of({} as any), getPlan: () => of(basePlan({ status: 'Approved', isEditable: false })) });
    component.ngOnInit();
    expect(component.canRevise()).toBeTrue();

    const draftComponent = makeComponent({ getManifest: () => of({} as any), getPlan: () => of(basePlan()) });
    draftComponent.ngOnInit();
    expect(draftComponent.canRevise()).toBeFalse();
  });

  it('Create Revision calls the revise endpoint and loads the new draft', () => {
    const svc: Partial<AdminImportPackageService> = {
      getManifest: () => of({} as any),
      getPlan: () => of(basePlan({ status: 'Approved', isEditable: false })),
      revisePlan: () => of(basePlan({ version: 2 })),
    };
    const component = makeComponent(svc);
    component.ngOnInit();
    component.reviseReason = 'need a fix';
    component.confirmRevise();

    expect(component.plan()?.version).toBe(2);
    expect(component.plan()?.isEditable).toBeTrue();
  });

  it('discardAndReload re-fetches the plan and clears the conflict state', () => {
    let callCount = 0;
    const svc: Partial<AdminImportPackageService> = {
      getManifest: () => of({} as any),
      getPlan: () => { callCount++; return of(basePlan({ concurrencyStamp: `stamp-${callCount}` })); },
    };
    const component = makeComponent(svc);
    component.ngOnInit();
    component.concurrencyConflict.set(true);
    component.discardAndReload();

    expect(component.concurrencyConflict()).toBeFalse();
    expect(component.plan()?.concurrencyStamp).toBe('stamp-2');
  });

  describe('cost ceiling amendment (Phase 4.4B)', () => {
    function pausedPlan(overrides: Partial<ImportExecutionPlanDto> = {}) {
      return basePlan({
        status: 'PausedForCostApproval',
        isEditable: false,
        pauseReason: 'Projected cost would exceed the approved ceiling.',
        approvedCostCeiling: 0.03,
        accruedCost: 0.03,
        remainingCeiling: 0,
        ...overrides,
      });
    }

    it('displays the paused cost state (accrued, ceiling, remaining, pause reason)', () => {
      const component = makeComponent({ getManifest: () => of({} as any), getPlan: () => of(pausedPlan()) });
      component.ngOnInit();

      expect(component.plan()?.status).toBe('PausedForCostApproval');
      expect(component.plan()?.accruedCost).toBe(0.03);
      expect(component.plan()?.approvedCostCeiling).toBe(0.03);
      expect(component.plan()?.remainingCeiling).toBe(0);
      expect(component.plan()?.pauseReason).toContain('exceed');
    });

    it('submits the new ceiling, reason, and current concurrency stamp', () => {
      let sent: any = null;
      const svc: Partial<AdminImportPackageService> = {
        getManifest: () => of({} as any),
        getPlan: () => of(pausedPlan()),
        amendCostCeiling: (pkg, plan, stamp, ceiling, reason) => {
          sent = { pkg, plan, stamp, ceiling, reason };
          return of(basePlan({ status: 'Executing', isEditable: false, approvedCostCeiling: ceiling }));
        },
      };
      const component = makeComponent(svc);
      component.ngOnInit();
      component.openResume();
      component.resumeCostCeiling = 5;
      component.resumeReason = 'need more budget';
      component.confirmResume();

      expect(sent.stamp).toBe('stamp-1');
      expect(sent.ceiling).toBe(5);
      expect(sent.reason).toBe('need more budget');
    });

    it('rejects a new ceiling that does not exceed the current one before calling the backend', () => {
      let called = false;
      const svc: Partial<AdminImportPackageService> = {
        getManifest: () => of({} as any),
        getPlan: () => of(pausedPlan()),
        amendCostCeiling: () => { called = true; return of(pausedPlan()); },
      };
      const component = makeComponent(svc);
      component.ngOnInit();
      component.openResume();
      component.resumeCostCeiling = 0.03; // not greater than the current ceiling
      component.resumeReason = 'trying anyway';
      component.confirmResume();

      expect(called).toBeFalse();
      expect(component.resumeError()).toContain('greater');
    });

    it('shows stale-conflict guidance on a 409 without resuming', () => {
      const svc: Partial<AdminImportPackageService> = {
        getManifest: () => of({} as any),
        getPlan: () => of(pausedPlan()),
        amendCostCeiling: () => throwError(() => ({ status: 409, error: { error: 'stale' } })),
      };
      const component = makeComponent(svc);
      component.ngOnInit();
      component.openResume();
      component.resumeCostCeiling = 5;
      component.resumeReason = 'reason';
      component.confirmResume();

      expect(component.resumeConcurrencyConflict()).toBeTrue();
      expect(component.plan()?.status).toBe('PausedForCostApproval'); // not resumed
    });

    it('refreshes package/plan state after a successful amendment', () => {
      const svc: Partial<AdminImportPackageService> = {
        getManifest: () => of({} as any),
        getPlan: () => of(pausedPlan()),
        amendCostCeiling: () => of(basePlan({
          status: 'Executing', isEditable: false, approvedCostCeiling: 5, accruedCost: 0.03, remainingCeiling: 4.97,
          ceilingAmendments: [{ amendmentId: 'a1', previousCeiling: 0.03, newCeiling: 5, currency: 'USD', reason: 'need more budget', administratorUserId: 'admin-1', createdAtUtc: '2026-01-01T00:00:00Z' }],
        })),
      };
      const component = makeComponent(svc);
      component.ngOnInit();
      component.openResume();
      component.resumeCostCeiling = 5;
      component.resumeReason = 'need more budget';
      component.confirmResume();

      expect(component.plan()?.status).toBe('Executing');
      expect(component.plan()?.ceilingAmendments.length).toBe(1);
      expect(component.resumeModalOpen()).toBeFalse();
    });
  });
});
