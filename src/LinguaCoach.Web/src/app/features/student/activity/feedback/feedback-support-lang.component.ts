import { Component, Input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-feedback-support-lang',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './feedback-support-lang.component.html',
})
export class FeedbackSupportLangComponent {
  @Input() text: string | null = null;

  showContent = signal(false);

  toggle(): void {
    this.showContent.update(v => !v);
  }
}
