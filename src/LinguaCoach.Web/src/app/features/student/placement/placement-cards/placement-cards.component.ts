import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PlacementService } from '../../../../core/services/placement.service';
import { AdaptivePlacementSummary, PlacementSkillStatus } from '../../../../core/models/placement.models';

export type PlacementCardsPageState = 'loading' | 'cards' | 'finishing' | 'done' | 'error';

/**
 * Placement landing page: one card per skill (Listening/Reading/Writing/Vocabulary/Grammar/
 * Speaking). Each card shows percent complete and locks once that skill is done. Clicking an
 * unlocked card starts/resumes a scoped adaptive run for that skill (PlacementComponent at
 * /placement/:skill), which hands control back here when the card finishes.
 */
@Component({
  selector: 'app-placement-cards',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './placement-cards.component.html',
})
export class PlacementCardsComponent implements OnInit {
  state = signal<PlacementCardsPageState>('loading');
  error = signal('');
  skills = signal<PlacementSkillStatus[]>([]);
  result = signal<AdaptivePlacementSummary | null>(null);

  private assessmentId = '';

  allCompleted(): boolean {
    const skills = this.skills();
    return skills.length > 0 && skills.every(s => s.completed);
  }

  constructor(private placement: PlacementService, private router: Router) {}

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.state.set('loading');
    this.placement.getAdaptiveCurrent().subscribe({
      next: current => {
        if (current?.hasPlacement && current.status === 'Completed') {
          this.result.set(current);
          this.state.set('done');
          return;
        }
        this.assessmentId = current?.assessmentId ?? '';
        this.loadSkills();
      },
      error: () => this.loadSkills(),
    });
  }

  private loadSkills(): void {
    this.placement.getSkillStatus().subscribe({
      next: skills => {
        this.skills.set(skills);
        this.state.set('cards');
      },
      error: () => {
        this.error.set('Could not load your placement progress. Please try again.');
        this.state.set('error');
      },
    });
  }

  openCard(skill: PlacementSkillStatus): void {
    if (skill.completed) return;
    this.router.navigate(['/placement', skill.skill]);
  }

  finish(): void {
    if (!this.assessmentId) return;
    this.state.set('finishing');
    this.placement.completeAdaptive(this.assessmentId).subscribe({
      next: summary => {
        this.result.set(summary);
        this.state.set('done');
      },
      error: err => {
        this.error.set(err?.error?.error ?? 'Could not finalise your placement. Please try again.');
        this.state.set('error');
      },
    });
  }

  continueToDashboard(): void {
    this.router.navigate(['/dashboard']);
  }

  retry(): void {
    this.error.set('');
    this.load();
  }

  skillLabel(skill: string | null | undefined): string {
    if (!skill) return '';
    return skill.charAt(0).toUpperCase() + skill.slice(1);
  }
}
