import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-feedback-ai-disclaimer',
  standalone: true,
  template: `<p data-testid="ai-disclaimer" style="font-size:11px;color:var(--sp-muted);line-height:1.5;margin:0">{{ text }}</p>`,
})
export class FeedbackAiDisclaimerComponent {
  @Input() text = 'This feedback is AI-assisted and may be approximate. It does not represent an official assessment.';
}
