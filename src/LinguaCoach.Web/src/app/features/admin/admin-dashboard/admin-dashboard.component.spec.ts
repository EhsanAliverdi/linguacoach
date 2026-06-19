import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AdminDashboardComponent } from './admin-dashboard.component';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { StudentListItem, AdminStats } from '../../../core/models/admin.models';

const STATS: AdminStats = {
  totalStudents: 5,
  onboardedStudents: 3,
  totalActivityAttempts: 120,
};

const STUDENT: StudentListItem = {
  studentProfileId: 'sp-1',
  userId: 'user-1',
  email: 'alice@example.com',
  firstName: 'Alice',
  lastName: 'Smith',
  displayName: 'Alice Smith',
  onboardingStatus: 'Completed',
  lifecycleStage: 'CourseReady',
  cefrLevel: 'B2',
  careerContext: null,
  learningGoal: null,
  learningGoalDescription: null as any,
  difficultSituationsText: null as any,
  preferredSessionDurationMinutes: null,
  professionalExperienceLevel: null,
  roleFamiliarity: null,
  createdAt: '2026-01-15T00:00:00Z',
};

const STUDENT_NO_CEFR: StudentListItem = {
  ...STUDENT,
  studentProfileId: 'sp-2',
  userId: 'user-2',
  email: 'bob@example.com',
  displayName: 'Bob Jones',
  cefrLevel: null,
  onboardingStatus: 'NotStarted',
};

function makeAdminApi(students: StudentListItem[] = [STUDENT], stats: AdminStats = STATS) {
  return {
    listStudents: jasmine.createSpy('listStudents').and.returnValue(of(students)),
    getStats: jasmine.createSpy('getStats').and.returnValue(of(stats)),
  };
}

describe('AdminDashboardComponent', () => {
  let fixture: ComponentFixture<AdminDashboardComponent>;
  let component: AdminDashboardComponent;
  let adminApi: ReturnType<typeof makeAdminApi>;

  async function setup(students: StudentListItem[] = [STUDENT], stats: AdminStats = STATS) {
    adminApi = makeAdminApi(students, stats);
    await TestBed.configureTestingModule({
      imports: [AdminDashboardComponent],
      providers: [
        provideRouter([]),
        { provide: AdminApiService, useValue: adminApi },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminDashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('renders the dashboard', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('Dashboard');
  });

  it('calls listStudents and getStats on init', async () => {
    await setup();
    expect(adminApi.listStudents).toHaveBeenCalledTimes(1);
    expect(adminApi.getStats).toHaveBeenCalledTimes(1);
  });

  it('renders total students stat card from stats', async () => {
    await setup([], STATS);
    expect(fixture.nativeElement.textContent).toContain('Total students');
    expect(fixture.nativeElement.textContent).toContain('5');
  });

  it('renders onboarded stat card from stats', async () => {
    await setup([], STATS);
    expect(fixture.nativeElement.textContent).toContain('Onboarded');
    expect(fixture.nativeElement.textContent).toContain('3');
  });

  it('renders activities tracked stat card', async () => {
    await setup([], STATS);
    expect(fixture.nativeElement.textContent).toContain('Activities tracked');
    expect(fixture.nativeElement.textContent).toContain('120');
  });

  it('renders quick action cards', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('Add student');
    expect(fixture.nativeElement.textContent).toContain('Manage students');
    expect(fixture.nativeElement.textContent).toContain('AI Config');
    expect(fixture.nativeElement.textContent).toContain('Prompts');
  });

  it('renders recent students table', async () => {
    await setup([STUDENT]);
    expect(fixture.nativeElement.textContent).toContain('alice@example.com');
  });

  it('renders onboarding badge using shared label', async () => {
    await setup([STUDENT]);
    expect(fixture.nativeElement.textContent).toContain('Completed');
  });

  it('renders not-started badge for unboarded student', async () => {
    await setup([STUDENT_NO_CEFR]);
    expect(fixture.nativeElement.textContent).toContain('Not started');
  });

  it('renders CEFR badge when level present', async () => {
    await setup([STUDENT]);
    expect(fixture.nativeElement.textContent).toContain('B2');
  });

  it('renders em-dash when CEFR absent', async () => {
    await setup([STUDENT_NO_CEFR]);
    expect(fixture.nativeElement.textContent).toContain('—');
  });

  it('shows empty state when no students', async () => {
    await setup([]);
    expect(fixture.nativeElement.textContent).toContain('No students yet');
  });

  it('renders AI System section', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('AI System');
    expect(fixture.nativeElement.textContent).toContain('Active');
  });

  it('renders only up to 5 students in preview', async () => {
    const many = Array.from({ length: 8 }, (_, i) => ({
      ...STUDENT,
      userId: `user-${i}`,
      studentProfileId: `sp-${i}`,
      email: `student${i}@example.com`,
    }));
    await setup(many);
    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(5);
  });
});
