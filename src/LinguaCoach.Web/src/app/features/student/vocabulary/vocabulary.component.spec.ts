import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { VocabularyComponent } from './vocabulary.component';
import { VocabularyService } from '../../../core/services/vocabulary.service';
import { StudentVocabularyItem } from '../../../core/models/vocabulary.models';

const makeItem = (overrides: Partial<StudentVocabularyItem> = {}): StudentVocabularyItem => ({
  id: 'item-1',
  term: 'could you please',
  suggestedPhrase: 'Could you please send the file?',
  meaningOrExplanation: 'A polite way to make a workplace request.',
  exampleSentence: 'Could you please confirm by tomorrow?',
  category: 'polite_request',
  status: 'New',
  source: 'AiExtractedFromWritingAttempt',
  seenCount: 1,
  lastSeenAtUtc: null,
  nextReviewAtUtc: null,
  createdAt: '2026-06-07T10:00:00Z',
  sourceActivityTitle: 'Follow-up email',
  sourceModuleTitle: 'Workplace Emails',
  ...overrides,
});

describe('VocabularyComponent', () => {
  let vocabService: jasmine.SpyObj<VocabularyService>;

  beforeEach(() => {
    vocabService = jasmine.createSpyObj('VocabularyService', ['getVocabulary', 'updateStatus']);

    TestBed.configureTestingModule({
      imports: [VocabularyComponent],
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: { get: () => null } } } },
        { provide: VocabularyService, useValue: vocabService },
      ],
    });
  });

  function create() {
    const fixture = TestBed.createComponent(VocabularyComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('shows empty state when no entries', fakeAsync(() => {
    vocabService.getVocabulary.and.returnValue(of([]));
    const fixture = create();
    tick();
    fixture.detectChanges();

    const html: string = fixture.nativeElement.textContent;
    expect(html).toContain('Your vocabulary list will grow as you complete writing activities.');
    expect(html).toContain('Start practising');
  }));

  it('shows summary cards with counts', fakeAsync(() => {
    vocabService.getVocabulary.and.returnValue(of([
      makeItem({ status: 'New' }),
      makeItem({ id: 'item-2', term: 'follow up', status: 'Practising' }),
      makeItem({ id: 'item-3', term: 'kind regards', status: 'Mastered' }),
    ]));
    const fixture = create();
    tick();
    fixture.detectChanges();

    const html: string = fixture.nativeElement.textContent;
    expect(html).toContain('New');
    expect(html).toContain('Practising');
    expect(html).toContain('Mastered');
    expect(html).toContain('Total saved');
  }));

  it('renders vocabulary card with term, explanation, and buttons', fakeAsync(() => {
    vocabService.getVocabulary.and.returnValue(of([makeItem()]));
    const fixture = create();
    tick();
    fixture.detectChanges();

    const html: string = fixture.nativeElement.textContent;
    expect(html).toContain('could you please');
    expect(html).toContain('Could you please send the file?');
    expect(html).toContain('A polite way to make a workplace request.');
    expect(html).toContain('Practise');
    expect(html).toContain('Mastered');
    expect(html).toContain('Ignore');
  }));

  it('filters items by status when filter selected', fakeAsync(() => {
    vocabService.getVocabulary.and.returnValue(of([
      makeItem({ status: 'New', term: 'could you please' }),
      makeItem({ id: 'item-2', status: 'Mastered', term: 'kind regards' }),
    ]));
    const fixture = create();
    tick();
    fixture.detectChanges();

    const component = fixture.componentInstance;
    component.setFilter('Mastered');
    fixture.detectChanges();

    const html: string = fixture.nativeElement.textContent;
    expect(html).toContain('kind regards');
    expect(html).not.toContain('could you please');
  }));

  it('calls updateStatus when Mastered button clicked', fakeAsync(() => {
    vocabService.getVocabulary.and.returnValue(of([makeItem()]));
    vocabService.updateStatus.and.returnValue(of(undefined as any));
    const fixture = create();
    tick();
    fixture.detectChanges();

    const component = fixture.componentInstance;
    component.updateStatus(component.allItems()[0], 'Mastered');
    tick();

    expect(vocabService.updateStatus).toHaveBeenCalledWith('item-1', 'Mastered');
  }));

  it('shows friendly error when API fails', fakeAsync(() => {
    vocabService.getVocabulary.and.returnValue(throwError(() => ({ error: { error: 'Server error.' } })));
    const fixture = create();
    tick();
    fixture.detectChanges();

    const html: string = fixture.nativeElement.textContent;
    expect(html).toContain('Could not load vocabulary');
    expect(html).toContain('Server error.');
    expect(html).toContain('Try again');
  }));

  it('does not display raw JSON', fakeAsync(() => {
    vocabService.getVocabulary.and.returnValue(of([makeItem()]));
    const fixture = create();
    tick();
    fixture.detectChanges();

    const html: string = fixture.nativeElement.textContent;
    expect(html).not.toContain('"StudentProfileId"');
    expect(html).not.toContain('"term":');
    expect(html).not.toContain('{"');
  }));

  it('category label is human-readable (no underscores)', fakeAsync(() => {
    vocabService.getVocabulary.and.returnValue(of([makeItem({ category: 'polite_request' })]));
    const fixture = create();
    tick();
    fixture.detectChanges();

    const html: string = fixture.nativeElement.textContent;
    expect(html).toContain('Polite request');
    expect(html).not.toContain('polite_request');
  }));
});


