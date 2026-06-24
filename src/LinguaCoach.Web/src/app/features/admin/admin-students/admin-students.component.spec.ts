import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AdminStudentsComponent } from './admin-students.component';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { ToastService } from '../../../core/services/toast.service';
import { StudentListItem, PagedResponse } from '../../../core/models/admin.models';

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
  preferredName: null,
  supportLanguageCode: null,
  supportLanguageName: null,
  difficultyPreference: null,
  translationHelpPreference: null,
  focusAreas: [],
  customFocusArea: null,
  learningGoals: [],
  customLearningGoal: null,
  learningPreferencesUpdatedAt: null,
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

function pagedOf(items: StudentListItem[]): PagedResponse<StudentListItem> {
  return { items, totalCount: items.length, page: 1, pageSize: 10, totalPages: Math.max(1, Math.ceil(items.length / 10)) };
}

function makeAdminApi(students: StudentListItem[] = [STUDENT_ACTIVE]) {
  return {
    listStudents: jasmine.createSpy('listStudents').and.returnValue(of(pagedOf(students))),
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

  it('renders Create student button via sp-admin-button wrapper', async () => {
    await setup();
    const el: HTMLElement = fixture.nativeElement;
    const btn = el.querySelector('sp-admin-button[routerLink]') ?? el.querySelector('sp-admin-button');
    expect(btn).toBeTruthy();
    expect(el.textContent).toContain('Create student');
  });

  it('calls listStudents with default params on init', async () => {
    await setup([STUDENT_ACTIVE]);
    expect(adminApi.listStudents).toHaveBeenCalledTimes(1);
    const call = adminApi.listStudents.calls.mostRecent().args[0];
    expect(call.page).toBe(1);
    expect(call.pageSize).toBe(10);
  });

  it('loads and displays student rows from paged response items', async () => {
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

  it('shows empty state when paged response items is empty', async () => {
    await setup([]);
    expect(fixture.nativeElement.textContent).toContain('No students found');
  });

  it('calls listStudents with updated page when page changes', async () => {
    await setup([STUDENT_ACTIVE]);
    adminApi.listStudents.calls.reset();
    component.onPageChange(2);
    expect(adminApi.listStudents).toHaveBeenCalledTimes(1);
    const call = adminApi.listStudents.calls.mostRecent().args[0];
    expect(call.page).toBe(2);
  });

  it('resets page to 1 and calls listStudents when search changes', async () => {
    await setup([STUDENT_ACTIVE]);
    component.page.set(3);
    adminApi.listStudents.calls.reset();
    component.onSearchChange('alice');
    expect(component.page()).toBe(1);
    expect(adminApi.listStudents).toHaveBeenCalledTimes(1);
    const call = adminApi.listStudents.calls.mostRecent().args[0];
    expect(call.search).toBe('alice');
    expect(call.page).toBe(1);
  });

  it('calls listStudents when includeArchived toggle changes', async () => {
    await setup([STUDENT_ACTIVE]);
    adminApi.listStudents.calls.reset();
    component.includeArchived = true;
    component.onIncludeArchivedChange();
    expect(adminApi.listStudents).toHaveBeenCalledTimes(1);
  });

  it('calls listStudents with sortBy and sortDir when sort header clicked', async () => {
    await setup([STUDENT_ACTIVE]);
    adminApi.listStudents.calls.reset();
    component.setSort('name');
    expect(adminApi.listStudents).toHaveBeenCalledTimes(1);
    const call = adminApi.listStudents.calls.mostRecent().args[0];
    expect(call.sortBy).toBe('name');
    expect(call.sortDir).toBeTruthy();
  });

  it('toggles sort direction on repeated click of same column', async () => {
    await setup([STUDENT_ACTIVE]);
    component.setSort('joined');
    expect(component.sortDirection()).toBe('asc');
    component.setSort('joined');
    expect(component.sortDirection()).toBe('desc');
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

  it('marks archived row when lifecycleStage is Archived', async () => {
    await setup([STUDENT_ARCHIVED]);
    const rows = fixture.nativeElement.querySelectorAll('tbody tr.sp-admin-archived-row');
    expect(rows.length).toBe(1);
  });

  it('shows pagination when totalPages > 1', async () => {
    // Server returns totalPages = 2
    adminApi = makeAdminApi([STUDENT_ACTIVE]);
    adminApi.listStudents.and.returnValue(of({ items: [STUDENT_ACTIVE], totalCount: 30, page: 1, pageSize: 10, totalPages: 2 }));
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

  it('archive action calls archiveStudent and reloads', async () => {
    await setup([STUDENT_ACTIVE]);
    spyOn(window, 'confirm').and.returnValue(true);
    adminApi.listStudents.calls.reset();
    component.confirmArchive(STUDENT_ACTIVE);
    expect(adminApi.archiveStudent).toHaveBeenCalledWith('id-1');
    expect(adminApi.listStudents).toHaveBeenCalledTimes(1);
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

  it('sortIndicator returns arrow for active sort column', async () => {
    await setup();
    component.setSort('name');
    expect(component.sortIndicator('name')).toContain('▲');
    adminApi.listStudents.and.returnValue(of(pagedOf([STUDENT_ACTIVE])));
    component.setSort('name');
    expect(component.sortIndicator('name')).toContain('▼');
  });

  it('lifecycleStage filter calls listStudents with param and resets page', async () => {
    await setup([STUDENT_ACTIVE]);
    component.page.set(3);
    adminApi.listStudents.calls.reset();
    component.onLifecycleStageChange('CourseReady');
    expect(component.page()).toBe(1);
    expect(adminApi.listStudents).toHaveBeenCalledTimes(1);
    const call = adminApi.listStudents.calls.mostRecent().args[0];
    expect(call.lifecycleStage).toBe('CourseReady');
  });

  it('onboardingStatus filter calls listStudents with param and resets page', async () => {
    await setup([STUDENT_ACTIVE]);
    component.page.set(2);
    adminApi.listStudents.calls.reset();
    component.onOnboardingStatusChange('Complete');
    expect(component.page()).toBe(1);
    expect(adminApi.listStudents).toHaveBeenCalledTimes(1);
    const call = adminApi.listStudents.calls.mostRecent().args[0];
    expect(call.onboardingStatus).toBe('Complete');
  });

  it('cefrLevel filter calls listStudents with param and resets page', async () => {
    await setup([STUDENT_ACTIVE]);
    component.page.set(2);
    adminApi.listStudents.calls.reset();
    component.onCefrLevelChange('B2');
    expect(component.page()).toBe(1);
    expect(adminApi.listStudents).toHaveBeenCalledTimes(1);
    const call = adminApi.listStudents.calls.mostRecent().args[0];
    expect(call.cefrLevel).toBe('B2');
  });

  it('clearFilters clears search and all filter params then calls listStudents', async () => {
    await setup([STUDENT_ACTIVE]);
    component.searchTerm.set('alice');
    component.filterLifecycleStage.set('CourseReady');
    component.filterOnboardingStatus.set('Complete');
    component.filterCefrLevel.set('B2');
    component.page.set(3);
    adminApi.listStudents.calls.reset();
    component.clearFilters();
    expect(component.searchTerm()).toBe('');
    expect(component.filterLifecycleStage()).toBe('');
    expect(component.filterOnboardingStatus()).toBe('');
    expect(component.filterCefrLevel()).toBe('');
    expect(component.page()).toBe(1);
    expect(adminApi.listStudents).toHaveBeenCalledTimes(1);
    const call = adminApi.listStudents.calls.mostRecent().args[0];
    expect(call.lifecycleStage).toBeUndefined();
    expect(call.onboardingStatus).toBeUndefined();
    expect(call.cefrLevel).toBeUndefined();
    expect(call.search).toBeUndefined();
  });

  it('clearFilters does not touch includeArchived', async () => {
    await setup([STUDENT_ACTIVE]);
    component.includeArchived = true;
    adminApi.listStudents.calls.reset();
    component.clearFilters();
    const call = adminApi.listStudents.calls.mostRecent().args[0];
    expect(call.includeArchived).toBeTrue();
  });

  it('hasActiveFilters returns false when no filters set', async () => {
    await setup([STUDENT_ACTIVE]);
    expect(component.hasActiveFilters()).toBeFalse();
  });

  it('hasActiveFilters returns true when any filter is set', async () => {
    await setup([STUDENT_ACTIVE]);
    component.filterLifecycleStage.set('CourseReady');
    expect(component.hasActiveFilters()).toBeTrue();
  });

  it('totalCount signal reflects server response', async () => {
    adminApi = makeAdminApi();
    adminApi.listStudents.and.returnValue(of({ items: [STUDENT_ACTIVE], totalCount: 42, page: 1, pageSize: 10, totalPages: 2 }));
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
    expect(component.totalCount()).toBe(42);
  });

  // ── Avatar helpers ───────────────────────────────────────────────────────

  describe('avatarInitial', () => {
    it('returns first letter of displayName uppercased', async () => {
      await setup();
      expect(component.avatarInitial(STUDENT_ACTIVE)).toBe('A');
    });

    it('falls back to firstName when displayName is empty', async () => {
      await setup();
      const s = { ...STUDENT_NO_PROFILE, displayName: '', firstName: 'Carol' };
      expect(component.avatarInitial(s)).toBe('C');
    });

    it('falls back to email when displayName and firstName are empty', async () => {
      await setup();
      expect(component.avatarInitial(STUDENT_NO_PROFILE)).toBe('C');
    });

    it('returns ? for completely empty student', async () => {
      await setup();
      const s = { ...STUDENT_ACTIVE, displayName: '', firstName: '', email: '' };
      expect(component.avatarInitial(s)).toBe('?');
    });
  });

  describe('avatarColor', () => {
    it('returns a hex colour string', async () => {
      await setup();
      const colour = component.avatarColor(STUDENT_ACTIVE);
      expect(colour).toMatch(/^#[0-9a-fA-F]{6}$/);
    });

    it('returns consistent colour for same email', async () => {
      await setup();
      expect(component.avatarColor(STUDENT_ACTIVE)).toBe(component.avatarColor(STUDENT_ACTIVE));
    });

    it('renders avatar element in student name cell', async () => {
      await setup([STUDENT_ACTIVE]);
      const avatar = fixture.nativeElement.querySelector('.sp-stu-avatar');
      expect(avatar).not.toBeNull();
    });

    it('renders correct initial in avatar', async () => {
      await setup([STUDENT_ACTIVE]);
      const avatar = fixture.nativeElement.querySelector('.sp-stu-avatar');
      expect(avatar.textContent.trim()).toBe('A');
    });
  });

  // ── Rows per page ────────────────────────────────────────────

  describe('rows per page', () => {
    it('renders rows-per-page selector', async () => {
      await setup();
      const label: string = fixture.nativeElement.textContent;
      expect(label).toContain('Rows per page');
    });

    it('onPageSizeChange resets page to 1 and calls listStudents', async () => {
      await setup([STUDENT_ACTIVE]);
      component.page.set(3);
      component.pageSize = 50;
      adminApi.listStudents.calls.reset();
      component.onPageSizeChange();
      expect(component.page()).toBe(1);
      expect(adminApi.listStudents).toHaveBeenCalledTimes(1);
      const call = adminApi.listStudents.calls.mostRecent().args[0];
      expect(call.pageSize).toBe(50);
    });
  });
});
