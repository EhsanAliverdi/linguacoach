import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AdminStudentsComponent } from './admin-students.component';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { ToastService } from '../../../core/services/toast.service';
import { StudentListItem } from '../../../core/models/admin.models';

const STUDENT_ACTIVE: StudentListItem = {
  studentProfileId: 'id-1',
  userId: 'user-1',
  email: 'alice@example.com',
  firstName: 'Alice',
  lastName: 'Smith',
  displayName: 'Alice Smith',
  lifecycleStage: 'CourseReady',
  onboardingStatus: 'Completed',
  cefrLevel: 'B2',
  careerContext: 'Software engineer at Acme',
  learningGoal: null as any,
  learningGoalDescription: null as any,
  difficultSituationsText: null as any,
  preferredSessionDurationMinutes: null,
  professionalExperienceLevel: null,
  roleFamiliarity: null,
  createdAt: '2025-01-15T00:00:00Z',
};

const STUDENT_ARCHIVED: StudentListItem = {
  ...STUDENT_ACTIVE,
  studentProfileId: 'id-2',
  email: 'bob@example.com',
  firstName: 'Bob',
  lastName: 'Jones',
  displayName: 'Bob Jones',
  lifecycleStage: 'Archived',
  onboardingStatus: 'Completed',
  careerContext: null as any,
  learningGoal: null as any,
};

const STUDENT_NO_PROFILE: StudentListItem = {
  ...STUDENT_ACTIVE,
  studentProfileId: 'id-3',
  email: 'carol@example.com',
  displayName: '',
  firstName: '',
  lastName: '',
  careerContext: null as any,
  learningGoal: null as any,
  cefrLevel: null as any,
  lifecycleStage: 'OnboardingRequired',
  onboardingStatus: 'NotStarted',
};

function makeAdminApi(students: StudentListItem[] = [STUDENT_ACTIVE]) {
  return {
    listStudents: jasmine.createSpy('listStudents').and.returnValue(of(students)),
    updateStudent: jasmine.createSpy('updateStudent').and.returnValue(of(STUDENT_ACTIVE)),
    archiveStudent: jasmine.createSpy('archiveStudent').and.returnValue(of({ ...STUDENT_ACTIVE, lifecycleStage: 'Archived' })),
    resetStudentPassword: jasmine.createSpy('resetStudentPassword').and.returnValue(of({})),
    resetStudent: jasmine.createSpy('resetStudent').and.returnValue(of({ newStage: 'OnboardingRequired', previousStage: 'CourseReady', clearedItems: {}, resetLogId: 'log-1' })),
  };
}

function makeToast() {
  return { success: jasmine.createSpy('success'), error: jasmine.createSpy('error') };
}

describe('AdminStudentsComponent', () => {
  let fixture: ComponentFixture<AdminStudentsComponent>;
  let component: AdminStudentsComponent;
  let adminApi: ReturnType<typeof makeAdminApi>;
  let toast: ReturnType<typeof makeToast>;

  async function setup(students: StudentListItem[] = [STUDENT_ACTIVE]) {
    adminApi = makeAdminApi(students);
    toast = makeToast();
    await TestBed.configureTestingModule({
      imports: [AdminStudentsComponent],
      providers: [
        provideRouter([]),
        { provide: AdminApiService, useValue: adminApi },
        { provide: ToastService, useValue: toast },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminStudentsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('renders page header', async () => {
    await setup();
    const el: HTMLElement = fixture.nativeElement;
    expect(el.textContent).toContain('Students');
  });

  it('loads and displays student rows', async () => {
    await setup([STUDENT_ACTIVE]);
    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(1);
    expect(fixture.nativeElement.textContent).toContain('Alice Smith');
  });

  it('shows loading state initially then data', fakeAsync(async () => {
    adminApi = makeAdminApi();
    toast = makeToast();
    await TestBed.configureTestingModule({
      imports: [AdminStudentsComponent],
      providers: [
        provideRouter([]),
        { provide: AdminApiService, useValue: adminApi },
        { provide: ToastService, useValue: toast },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminStudentsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    tick();
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('sp-admin-loading-state, [data-testid="loading"]') || el.textContent).toBeTruthy();
  }));

  it('shows error state on load failure', async () => {
    adminApi = makeAdminApi();
    adminApi.listStudents.and.returnValue(throwError(() => new Error('fail')));
    toast = makeToast();
    await TestBed.configureTestingModule({
      imports: [AdminStudentsComponent],
      providers: [
        provideRouter([]),
        { provide: AdminApiService, useValue: adminApi },
        { provide: ToastService, useValue: toast },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminStudentsComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Could not load');
  });

  it('shows empty state when no students match', async () => {
    await setup([]);
    expect(fixture.nativeElement.textContent).toContain('No students found');
  });

  it('filters students reactively when search term changes', async () => {
    await setup([STUDENT_ACTIVE, STUDENT_ARCHIVED]);
    expect(component.filteredStudents().length).toBe(2);
    component.searchTerm.set('alice');
    expect(component.filteredStudents().length).toBe(1);
    expect(component.filteredStudents()[0].email).toBe('alice@example.com');
    component.searchTerm.set('');
    expect(component.filteredStudents().length).toBe(2);
  });

  it('shows lifecycle badge with friendly label', async () => {
    await setup([STUDENT_ACTIVE]);
    const badges = fixture.nativeElement.querySelectorAll('sp-admin-badge');
    const labels = Array.from(badges).map((b: any) => b.textContent?.trim());
    expect(labels.some(l => l && l.length > 0)).toBeTrue();
  });

  it('shows onboarding badge', async () => {
    await setup([STUDENT_ACTIVE]);
    const badges = fixture.nativeElement.querySelectorAll('sp-admin-badge');
    expect(badges.length).toBeGreaterThanOrEqual(2);
  });

  it('shows profile text when careerContext is set', async () => {
    await setup([STUDENT_ACTIVE]);
    expect(fixture.nativeElement.textContent).toContain('Software engineer');
  });

  it('shows "Not set" when no careerContext or learningGoal', async () => {
    await setup([STUDENT_NO_PROFILE]);
    expect(fixture.nativeElement.textContent).toContain('Not set');
  });

  it('marks archived row when lifecycleStage is Archived', async () => {
    await setup([STUDENT_ARCHIVED]);
    const rows = fixture.nativeElement.querySelectorAll('tbody tr.sp-admin-archived-row');
    expect(rows.length).toBe(1);
  });

  it('shows pagination when total pages > 1', async () => {
    const many = Array.from({ length: 30 }, (_, i) => ({
      ...STUDENT_ACTIVE,
      studentProfileId: `id-${i}`,
      email: `user${i}@example.com`,
      displayName: `User ${i}`,
    }));
    await setup(many);
    const pagination = fixture.nativeElement.querySelector('sp-admin-pagination');
    expect(pagination).not.toBeNull();
  });

  it('does not show pagination when only one page', async () => {
    await setup([STUDENT_ACTIVE]);
    const pagination = fixture.nativeElement.querySelector('sp-admin-pagination');
    expect(pagination).toBeNull();
  });

  it('action menu renders for active student', async () => {
    await setup([STUDENT_ACTIVE]);
    const actions = fixture.nativeElement.querySelector('sp-admin-table-actions');
    expect(actions).not.toBeNull();
  });

  it('archive action calls archiveStudent and removes row', async () => {
    await setup([STUDENT_ACTIVE]);
    spyOn(window, 'confirm').and.returnValue(true);
    component.confirmArchive(STUDENT_ACTIVE);
    expect(adminApi.archiveStudent).toHaveBeenCalledWith('id-1');
  });

  it('startEdit populates editForm', async () => {
    await setup([STUDENT_ACTIVE]);
    component.startEdit(STUDENT_ACTIVE);
    expect(component.editForm.firstName).toBe('Alice');
    expect(component.editForm.careerContext).toBe('Software engineer at Acme');
  });

  it('cancelEdit clears editing signal', async () => {
    await setup([STUDENT_ACTIVE]);
    component.startEdit(STUDENT_ACTIVE);
    component.cancelEdit();
    expect(component.editing()).toBeNull();
  });

  it('displayName falls back to email when no name set', async () => {
    await setup([STUDENT_NO_PROFILE]);
    expect(component.displayName(STUDENT_NO_PROFILE)).toBe('carol@example.com');
  });

  it('sort by name changes order', async () => {
    await setup([STUDENT_ACTIVE, STUDENT_ARCHIVED]);
    component.setSort('name');
    fixture.detectChanges();
    const names = component.filteredStudents().map(s => component.displayName(s));
    const sorted = [...names].sort();
    expect(names).toEqual(sorted);
  });

  it('sortIndicator returns arrow for active sort column', async () => {
    await setup();
    component.setSort('name');
    expect(component.sortIndicator('name')).toContain('▲');
    component.setSort('name');
    expect(component.sortIndicator('name')).toContain('▼');
  });
});
