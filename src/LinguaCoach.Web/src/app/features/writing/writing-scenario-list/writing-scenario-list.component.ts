import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { WritingService } from '../../../core/services/writing.service';
import { WritingScenarioDto } from '../../../core/models/writing.models';

type PageState = 'loading' | 'loaded' | 'error';

@Component({
  selector: 'app-writing-scenario-list',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './writing-scenario-list.component.html',
})
export class WritingScenarioListComponent implements OnInit {
  state = signal<PageState>('loading');
  scenarios = signal<WritingScenarioDto[]>([]);
  errorMessage = signal('');

  constructor(private writingService: WritingService, private router: Router) {}

  ngOnInit(): void {
    this.writingService.getScenarios().subscribe({
      next: list => { this.scenarios.set(list); this.state.set('loaded'); },
      error: err => {
        this.errorMessage.set(err.error?.error ?? 'Could not load writing scenarios.');
        this.state.set('error');
      },
    });
  }

  selectScenario(scenario: WritingScenarioDto): void {
    this.router.navigate(['/writing/exercise', scenario.id]);
  }

  backToDashboard(): void {
    this.router.navigate(['/dashboard']);
  }

  difficultyLabel(difficulty: string): string {
    const map: Record<string, string> = { A1: 'Beginner', A2: 'Elementary', B1: 'Intermediate', B2: 'Upper intermediate', C1: 'Advanced' };
    return map[difficulty] ?? difficulty;
  }

  difficultyColour(difficulty: string): string {
    if (difficulty === 'A1' || difficulty === 'A2') return 'bg-emerald-100 text-emerald-800 ring-emerald-200';
    if (difficulty === 'B1' || difficulty === 'B2') return 'bg-amber-100 text-amber-800 ring-amber-200';
    return 'bg-indigo-100 text-indigo-800 ring-indigo-200';
  }
}
