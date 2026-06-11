import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface EmailReplyContent {
  situation?: string | null;
  audience?: string | null;
  suggestedSubject?: string | null;
  targetPhrases?: string[];
  exampleText?: string | null;
  coachNote?: string | null;
  wordCountTarget?: number | null;
}

export interface EmailReplyAnswer {
  subject: string;
  body: string;
}

@Component({
  selector: 'app-email-reply',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './email-reply.component.html',
})
export class EmailReplyComponent {
  @Input() content!: EmailReplyContent;
  @Input() disabled = false;
  @Input() attemptCount = 0;
  @Output() submitted = new EventEmitter<EmailReplyAnswer>();

  subject = '';
  body = '';

  get wordCount(): number {
    return this.body.trim().split(/\s+/).filter(Boolean).length;
  }

  submit(): void {
    if (this.subject.trim() && this.body.trim()) {
      this.submitted.emit({ subject: this.subject, body: this.body });
    }
  }
}
