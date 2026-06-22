import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AssessmentService } from '../../../../core/services/assessment.service';
import { CefrAssessmentResult } from '../../../../core/models/assessment.models';

type PageState = 'intro' | 'writing' | 'submitting' | 'result' | 'error';

@Component({
  selector: 'app-cefr-assessment',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './cefr-assessment.component.html',
})
export class CefrAssessmentComponent {
  state = signal<PageState>('intro');
  result = signal<CefrAssessmentResult | null>(null);
  sampleText = '';
  errorMessage = signal('');

  readonly prompt = 'Write a short email (5â€“10 sentences) to a project manager asking for an update on a pending document approval. Use professional English.';

  constructor(private assessmentService: AssessmentService, private router: Router) {}

  startWriting(): void {
    this.state.set('writing');
  }

  onSubmit(): void {
    if (!this.sampleText.trim()) return;
    this.state.set('submitting');
    this.assessmentService.assessCefr(this.sampleText).subscribe({
      next: r => { this.result.set(r); this.state.set('result'); },
      error: err => {
        this.errorMessage.set(err.error?.error ?? 'Assessment failed. Please try again.');
        this.state.set('writing');
      },
    });
  }

  goToDashboard(): void {
    this.router.navigate(['/dashboard']);
  }

  levelColour(level: string): string {
    if (['C1', 'C2'].includes(level)) return 'text-green-600';
    if (['B1', 'B2'].includes(level)) return 'text-indigo-600';
    return 'text-amber-600';
  }

  levelBg(level: string): string {
    if (['C1', 'C2'].includes(level)) return 'bg-green-50 border-green-200';
    if (['B1', 'B2'].includes(level)) return 'bg-indigo-50 border-indigo-200';
    return 'bg-amber-50 border-amber-200';
  }
}

