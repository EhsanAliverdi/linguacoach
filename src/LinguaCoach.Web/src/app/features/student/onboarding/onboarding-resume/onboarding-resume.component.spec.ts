import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { OnboardingResumeComponent } from './onboarding-resume.component';
import { OnboardingService } from '../../../../core/services/onboarding.service';

describe('OnboardingResumeComponent', () => {
  let onboardingService: jasmine.SpyObj<OnboardingService>;
  let router: jasmine.SpyObj<Router>;

  beforeEach(() => {
    onboardingService = jasmine.createSpyObj('OnboardingService', ['getStatus']);
    router = jasmine.createSpyObj('Router', ['navigate']);

    TestBed.configureTestingModule({
      imports: [OnboardingResumeComponent],
      providers: [
        { provide: OnboardingService, useValue: onboardingService },
        { provide: Router, useValue: router },
      ],
    });
  });

  function create() {
    const fixture = TestBed.createComponent(OnboardingResumeComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('navigates to /dashboard when onboarding is complete', fakeAsync(() => {
    onboardingService.getStatus.and.returnValue(of({ currentStep: 'Skill', isComplete: true }));
    create();
    tick();
    expect(router.navigate).toHaveBeenCalledWith(['/dashboard']);
  }));

  it('navigates to /onboarding/v2 when onboarding is incomplete', fakeAsync(() => {
    onboardingService.getStatus.and.returnValue(of({ currentStep: 'None', isComplete: false }));
    create();
    tick();
    expect(router.navigate).toHaveBeenCalledWith(['/onboarding/v2']);
  }));

  it('falls back to /onboarding/v2 on error', fakeAsync(() => {
    onboardingService.getStatus.and.returnValue(throwError(() => new Error('network')));
    create();
    tick();
    expect(router.navigate).toHaveBeenCalledWith(['/onboarding/v2']);
  }));
});
