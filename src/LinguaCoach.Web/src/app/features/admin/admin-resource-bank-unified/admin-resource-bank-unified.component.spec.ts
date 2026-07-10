import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AdminResourceBankUnifiedComponent } from './admin-resource-bank-unified.component';
import {
  AdminUnifiedResourceBankService,
} from '../../../core/services/admin-resource-import.service';
import { AdminLessonService } from '../../../core/services/admin-lesson.service';
import { AdminExerciseService } from '../../../core/services/admin-exercise.service';
import { AdminModuleService } from '../../../core/services/admin-module.service';
import { UnifiedResourceBankListResult } from '../../../core/models/admin-resource-import.models';

const EMPTY_RESULT: UnifiedResourceBankListResult = { items: [], totalCount: 0 };

describe('AdminResourceBankUnifiedComponent', () => {
  let fixture: ComponentFixture<AdminResourceBankUnifiedComponent>;
  let component: AdminResourceBankUnifiedComponent;
  let bankSvc: { list: jasmine.Spy };

  async function setup(typeParam: string | null = null) {
    bankSvc = { list: jasmine.createSpy('list').and.returnValue(of(EMPTY_RESULT)) };
    await TestBed.configureTestingModule({
      imports: [AdminResourceBankUnifiedComponent],
      providers: [
        provideRouter([]),
        { provide: AdminUnifiedResourceBankService, useValue: bankSvc },
        { provide: AdminLessonService, useValue: {} },
        { provide: AdminExerciseService, useValue: {} },
        { provide: AdminModuleService, useValue: {} },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { queryParamMap: convertToParamMap(typeParam ? { type: typeParam } : {}) },
          },
        },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminResourceBankUnifiedComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  // Phase H9A — redirects from the removed typed bank routes land here with ?type=<value>;
  // this pre-seeds the filter so the redirect actually narrows the list instead of just landing
  // on the unfiltered unified page.
  it('pre-seeds the type filter from a valid ?type= query param', async () => {
    await setup('vocabulary');
    expect(component.typeFilter()).toBe('vocabulary');
    expect(bankSvc.list).toHaveBeenCalledWith(1, 20, 'vocabulary', undefined, undefined, undefined, undefined, undefined, undefined, undefined);
  });

  it('ignores an unrecognized ?type= query param and falls back to "all"', async () => {
    await setup('not-a-real-type');
    expect(component.typeFilter()).toBe('all');
  });

  it('defaults to "all" when no ?type= query param is present', async () => {
    await setup();
    expect(component.typeFilter()).toBe('all');
  });
});
