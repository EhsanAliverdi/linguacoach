import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-feedback-skill-context',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './feedback-skill-context.component.html',
})
export class FeedbackSkillContextComponent {
  @Input() primarySkill: string | null = null;
  @Input() exerciseType: string | null = null;
  @Input() difficulty: string | null = null;

  get hasAnyContext(): boolean {
    return !!(this.primarySkill || this.exerciseType || this.difficulty);
  }
}
