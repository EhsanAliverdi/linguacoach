import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { HttpEventType } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { AdminContentImportComponent } from './admin-content-import.component';
import { AdminResourceImportRunService, AdminResourceSourceService } from '../../../core/services/admin-resource-import.service';
import { AdminImportPackageService } from '../../../core/services/admin-import-package.service';
import { AdminResourceSourceDto } from '../../../core/models/admin-resource-import.models';
import { ImportPackageManifestSummaryDto } from '../../../core/models/admin-import-package.models';

const SOURCE: AdminResourceSourceDto = {
  sourceId: 'src-1', name: 'Test Source', licenseType: 'AdminUpload', sourceUrl: null,
  usageRestrictionNotes: null, languageCode: 'en', isImportApproved: true, allowsStudentDisplay: true,
  allowsCommercialUse: true, attributionText: null, sourceVersion: null, downloadUrl: null,
} as AdminResourceSourceDto;

const SUBMIT_RESULT: ImportPackageManifestSummaryDto = {
  importPackageId: 'pkg-1', status: 'awaitingSample', isAccepted: true, rejectionReason: null,
  compressedSizeBytes: 10, expandedSizeBytes: 10, entryCount: 1, folderGroups: [], distinctExtensions: ['.jsonl'],
  duplicateChecksumEntryCount: 0, unsupportedEntryCount: 0, suspiciousEntryCount: 0,
};

/**
 * Phase 4.2 — the unified Import submission page. Proves the acceptance criteria: submitting
 * pasted content creates a package (via the new submit() endpoint, never the removed old
 * content-imports/resource-import-runs endpoints) and always navigates to the plan page — never
 * directly to a candidate review page, and no candidate is ever created from this component.
 */
describe('AdminContentImportComponent', () => {
  let fixture: ComponentFixture<AdminContentImportComponent>;
  let component: AdminContentImportComponent;
  let packageSvc: jasmine.SpyObj<AdminImportPackageService>;
  let router: Router;

  function setup(submitResult = SUBMIT_RESULT) {
    packageSvc = jasmine.createSpyObj('AdminImportPackageService', [
      'submit', 'requestUpload', 'putToStorage', 'confirmUpload', 'generatePlan',
      'createUploadSession', 'uploadSessionPart', 'getUploadSessionStatus', 'completeUploadSession', 'abortUploadSession',
    ]);
    packageSvc.submit.and.returnValue(of(submitResult));

    const sourceSvc = jasmine.createSpyObj<AdminResourceSourceService>('AdminResourceSourceService', ['list', 'add', 'approve']);
    sourceSvc.list.and.returnValue(of({ items: [SOURCE], totalCount: 1, overallTotalCount: 1, approvedCount: 1 }));

    const runSvc = jasmine.createSpyObj<AdminResourceImportRunService>('AdminResourceImportRunService', ['list']);
    runSvc.list.and.returnValue(of({ items: [], totalCount: 0, overallTotalCount: 0 }));

    TestBed.configureTestingModule({
      imports: [AdminContentImportComponent],
      providers: [
        { provide: AdminImportPackageService, useValue: packageSvc },
        { provide: AdminResourceSourceService, useValue: sourceSvc },
        { provide: AdminResourceImportRunService, useValue: runSvc },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: convertToParamMap({}) } },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AdminContentImportComponent);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));
    fixture.detectChanges();
  }

  it('cannot submit with no source and no content', () => {
    setup();
    component.selectedSourceId = '';
    component.pastedText = '';
    expect(component.canSubmit()).toBeFalse();
  });

  it('can submit once a source and pasted text are present', () => {
    setup();
    component.selectedSourceId = 'src-1';
    component.pastedText = 'hello world';
    expect(component.canSubmit()).toBeTrue();
  });

  it('submitting pasted text calls the new package submit endpoint, not any old import endpoint', () => {
    setup();
    component.selectedSourceId = 'src-1';
    component.pastedText = 'hello\nworld';

    component.submit();

    expect(packageSvc.submit).toHaveBeenCalledWith('src-1', 'hello\nworld', [], undefined);
  });

  it('a successful submission navigates straight to the plan review page, never to a candidate review page', () => {
    setup();
    component.selectedSourceId = 'src-1';
    component.pastedText = 'hello world';

    component.submit();

    expect(router.navigate).toHaveBeenCalledWith(['/admin/content/import/packages', 'pkg-1', 'plan']);
  });

  it('a rejected submission surfaces the rejection reason and does not navigate', () => {
    setup({ ...SUBMIT_RESULT, isAccepted: false, rejectionReason: 'No usable content.' });
    component.selectedSourceId = 'src-1';
    component.pastedText = 'hello world';

    component.submit();

    expect(component.submitError()).toBe('No usable content.');
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('a failed submission surfaces the server error and does not navigate', () => {
    setup();
    packageSvc.submit.and.returnValue(throwError(() => ({ error: { error: 'Boom' } })));
    component.selectedSourceId = 'src-1';
    component.pastedText = 'hello world';

    component.submit();

    expect(component.submitError()).toBe('Boom');
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('exposes no method that creates candidates or publishes directly from this page', () => {
    setup();
    // Acceptance criterion: the old immediate "import as candidates" / mapping-review-modal
    // workflow no longer exists on this component at all.
    expect((component as any).submitPaste).toBeUndefined();
    expect((component as any).submitFile).toBeUndefined();
    expect((component as any).confirmMapping).toBeUndefined();
    expect((component as any).uploadPackage).toBeUndefined();
  });

  // ── Phase 4.7 (2026-07-17 reliable large uploads) — the resumable, chunked-upload ZIP flow. ──
  describe('ZIP upload (resumable, chunked)', () => {
    function zipFile(name: string, bytes: number): File {
      return new File([new Uint8Array(bytes)], name, { type: 'application/zip' });
    }

    function fakePutEvents(partNumber: number, sizeBytes: number) {
      return of(
        { type: HttpEventType.UploadProgress, loaded: sizeBytes, total: sizeBytes } as any,
        { type: HttpEventType.Response, body: { partNumber, sizeBytes, sha256Checksum: null, uploadedAtUtc: new Date().toISOString() } } as any,
      );
    }

    beforeEach(() => sessionStorage.clear());

    it('uploads every part, completes the session, generates a plan, and navigates to it', fakeAsync(() => {
      setup();
      component.selectedSourceId = 'src-1';
      const file = zipFile('words.zip', 30);

      packageSvc.createUploadSession.and.returnValue(of({ sessionId: 'sess-1', partSizeBytes: 10, totalPartsExpected: 3, expiresAtUtc: new Date().toISOString() }));
      packageSvc.uploadSessionPart.and.callFake((sessionId: string, partNumber: number) => fakePutEvents(partNumber, 10));
      packageSvc.completeUploadSession.and.returnValue(of(SUBMIT_RESULT));
      packageSvc.generatePlan.and.returnValue(of({} as any));

      component.onFilesSelected(file);
      component.submit();
      tick();

      expect(packageSvc.createUploadSession).toHaveBeenCalledWith('src-1', 'words.zip', 30, null, undefined);
      expect(packageSvc.uploadSessionPart).toHaveBeenCalledTimes(3);
      expect(packageSvc.completeUploadSession).toHaveBeenCalledWith('sess-1');
      expect(router.navigate).toHaveBeenCalledWith(['/admin/content/import/packages', 'pkg-1', 'plan']);
      expect(component.activeUploadSessionId()).toBeNull();
    }));

    it('resumes an existing session for the same source/file/size instead of restarting', fakeAsync(() => {
      setup();
      component.selectedSourceId = 'src-1';
      const file = zipFile('words.zip', 30);
      sessionStorage.setItem(`import-upload-session:src-1:words.zip:30`, 'sess-existing');

      packageSvc.getUploadSessionStatus.and.returnValue(of({
        sessionId: 'sess-existing', status: 'InProgress', originalFileName: 'words.zip',
        declaredTotalSizeBytes: 30, partSizeBytes: 10, totalPartsExpected: 3,
        uploadedParts: [{ partNumber: 1, sizeBytes: 10, sha256Checksum: null, uploadedAtUtc: new Date().toISOString() }],
        importPackageId: null, expiresAtUtc: new Date().toISOString(),
      }));
      packageSvc.uploadSessionPart.and.callFake((sessionId: string, partNumber: number) => fakePutEvents(partNumber, 10));
      packageSvc.completeUploadSession.and.returnValue(of(SUBMIT_RESULT));
      packageSvc.generatePlan.and.returnValue(of({} as any));

      component.onFilesSelected(file);
      component.submit();
      tick();

      expect(packageSvc.createUploadSession).not.toHaveBeenCalled();
      // Only parts 2 and 3 should be (re-)uploaded — part 1 was already present.
      expect(packageSvc.uploadSessionPart).toHaveBeenCalledTimes(2);
      expect(packageSvc.completeUploadSession).toHaveBeenCalledWith('sess-existing');
    }));

    it('cancelling an in-progress upload aborts the session and clears resume state', () => {
      setup();
      component.selectedSourceId = 'src-1';
      const file = zipFile('words.zip', 30);
      component.onFilesSelected(file);

      packageSvc.abortUploadSession.and.returnValue(of({}));
      (component as any).activeUploadSessionId.set('sess-1');
      sessionStorage.setItem('import-upload-session:src-1:words.zip:30', 'sess-1');

      component.cancelUpload();

      expect(packageSvc.abortUploadSession).toHaveBeenCalledWith('sess-1');
      expect(component.activeUploadSessionId()).toBeNull();
      expect(sessionStorage.getItem('import-upload-session:src-1:words.zip:30')).toBeNull();
    });
  });
});
