import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SpLessonStudentPreviewComponent } from './sp-lesson-student-preview.component';
import { LessonDto } from '../../../../core/models/admin-lesson.models';

const LESSON: LessonDto = {
  id: 'lesson-1', title: 'Present Perfect', body: '<p><strong>Used</strong> for past actions.</p>',
  examplesJson: '["<p>I have <em>visited</em> Paris.</p>"]', commonMistakesJson: '["<p>Confusing with simple past</p>"]',
  usageNotes: '<p>Common in <strong>spoken</strong> English.</p>', cefrLevel: 'B1', skill: 'grammar', subskill: 'Tenses',
  contextTagsJson: '[]', focusTagsJson: '[]', difficultyBand: 3, estimatedMinutes: 5,
  sourceMode: 'Manual', generationProvider: null, generationModel: null, reviewStatus: 'PendingReview',
  createdByUserId: null, reviewedByUserId: null, approvedAtUtc: null, rejectedAtUtc: null, rejectionReason: null,
  reviewNotes: null, createdAt: '2026-07-01T00:00:00Z', updatedAtUtc: '2026-07-01T00:00:00Z', links: [],
  isArchived: false,
};

describe('SpLessonStudentPreviewComponent', () => {
  let fixture: ComponentFixture<SpLessonStudentPreviewComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [SpLessonStudentPreviewComponent] }).compileComponents();
    fixture = TestBed.createComponent(SpLessonStudentPreviewComponent);
    fixture.componentInstance.lesson = LESSON;
    fixture.detectChanges();
  });

  it('renders Body as actual HTML, not escaped tags', () => {
    const html = fixture.nativeElement.innerHTML as string;
    expect(html).toContain('<strong>Used</strong>');
    expect(html).not.toContain('&lt;strong&gt;');
  });

  it('renders each Example as HTML', () => {
    const html = fixture.nativeElement.innerHTML as string;
    expect(html).toContain('<em>visited</em>');
  });

  it('renders Usage Notes and Common Mistakes as HTML', () => {
    const html = fixture.nativeElement.innerHTML as string;
    expect(html).toContain('<strong>spoken</strong>');
    expect(html).toContain('Confusing with simple past');
  });
});
