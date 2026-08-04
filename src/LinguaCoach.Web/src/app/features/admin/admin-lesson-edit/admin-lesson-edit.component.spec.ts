import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { AdminLessonEditComponent } from './admin-lesson-edit.component';
import { AdminLessonService } from '../../../core/services/admin-lesson.service';
import { LessonDto, LessonMediaUploadResult, UpdateLessonRequestBody } from '../../../core/models/admin-lesson.models';

const LESSON: LessonDto = {
  id: 'lesson-1', title: 'Present Perfect', body: '<p>Used for past actions.</p>',
  examplesJson: '["<p>I have visited Paris.</p>"]', commonMistakesJson: '["<p>Confusing with simple past</p>"]',
  usageNotes: '<p>Common in spoken English.</p>', cefrLevel: 'B1', skill: 'Grammar', subskill: 'Tenses',
  contextTagsJson: '["travel"]', focusTagsJson: '["past-experience"]', difficultyBand: 3, estimatedMinutes: 5,
  sourceMode: 'Manual', generationProvider: null, generationModel: null, reviewStatus: 'PendingReview',
  createdByUserId: null, reviewedByUserId: null, approvedAtUtc: null, rejectedAtUtc: null, rejectionReason: null,
  reviewNotes: null, createdAt: '2026-07-01T00:00:00Z', updatedAtUtc: '2026-07-01T00:00:00Z', links: [],
  isArchived: false,
};

function makeLessonService(overrides: Partial<Record<string, unknown>> = {}) {
  return {
    get: jasmine.createSpy('get').and.returnValue(of(LESSON)),
    update: jasmine.createSpy('update').and.returnValue(of({ ...LESSON, id: 'lesson-1' })),
    uploadMedia: jasmine.createSpy('uploadMedia').and.returnValue(
      of<LessonMediaUploadResult>({ storageKey: 'lesson-media/a/1.png', url: '/api/lesson-media/lesson-media/a/1.png', mimeType: 'image/png' })),
    ...overrides,
  };
}

describe('AdminLessonEditComponent', () => {
  let fixture: ComponentFixture<AdminLessonEditComponent>;
  let component: AdminLessonEditComponent;
  let lessonSvc: ReturnType<typeof makeLessonService>;
  let router: Router;

  async function setup(overrides: Partial<Record<string, unknown>> = {}) {
    lessonSvc = makeLessonService(overrides);
    await TestBed.configureTestingModule({
      imports: [AdminLessonEditComponent],
      providers: [
        { provide: AdminLessonService, useValue: lessonSvc },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: 'lesson-1' }) } },
        },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminLessonEditComponent);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('loads the lesson and parses examples/commonMistakes into arrays of HTML strings', async () => {
    await setup();
    expect(component.title).toBe('Present Perfect');
    expect(component.body).toBe('<p>Used for past actions.</p>');
    expect(component.examples).toEqual(['<p>I have visited Paris.</p>']);
    expect(component.commonMistakes).toEqual(['<p>Confusing with simple past</p>']);
  });

  it('addExample/removeExample mutate the examples array', async () => {
    await setup();
    component.addExample();
    expect(component.examples.length).toBe(2);
    component.removeExample(0);
    expect(component.examples).toEqual(['']);
  });

  it('save requires a non-empty title and body (HTML tags alone do not count as content)', async () => {
    await setup();
    component.title = 'Present Perfect';
    component.body = '<p></p>';

    component.save();

    expect(component.error()).toContain('required');
    expect(lessonSvc.update).not.toHaveBeenCalled();
  });

  it('save sends the rich-text HTML as-is and drops blank example/mistake rows', async () => {
    await setup();
    const navigateSpy = spyOn(router, 'navigateByUrl');
    component.examples = ['<p>Real example</p>', '<p></p>', ''];
    component.commonMistakes = ['   ', '<p>Real mistake</p>'];

    component.save();

    const body = lessonSvc.update.calls.mostRecent().args[1] as UpdateLessonRequestBody;
    expect(body.body).toBe('<p>Used for past actions.</p>');
    expect(body.examples).toEqual(['<p>Real example</p>']);
    expect(body.commonMistakes).toEqual(['<p>Real mistake</p>']);
    expect(navigateSpy).toHaveBeenCalledWith('/admin/lesson-library/lesson-1');
  });

  it('openStudentPreview/closeStudentPreview toggle the preview modal', async () => {
    await setup();
    expect(component.studentPreviewOpen()).toBeFalse();
    component.openStudentPreview();
    expect(component.studentPreviewOpen()).toBeTrue();
    component.closeStudentPreview();
    expect(component.studentPreviewOpen()).toBeFalse();
  });

  it('previewLesson reflects live unsaved edits, not just the last-loaded item', async () => {
    await setup();
    component.title = 'Edited title (unsaved)';
    component.body = '<p>Edited body</p>';
    component.examples = ['<p>New example</p>'];

    const preview = component.previewLesson!;

    expect(preview.title).toBe('Edited title (unsaved)');
    expect(preview.body).toBe('<p>Edited body</p>');
    expect(JSON.parse(preview.examplesJson)).toEqual(['<p>New example</p>']);
    expect(preview.id).toBe('lesson-1');
  });
});
