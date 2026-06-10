import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface ChatMessage {
  id: string;
  sender: string;
  text: string;
  isStudent?: boolean;
}

export interface ChatReplyContent {
  scenario?: string | null;
  instructions?: string | null;
  messages: ChatMessage[];
  replyPrompt?: string | null;
  targetPhrases?: string[];
  wordCountTarget?: number | null;
}

export interface ChatReplyAnswer {
  text: string;
}

@Component({
  selector: 'app-chat-reply',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './chat-reply.component.html',
})
export class ChatReplyComponent {
  @Input() content!: ChatReplyContent;
  @Input() disabled = false;
  @Output() submitted = new EventEmitter<ChatReplyAnswer>();

  replyText = '';

  get wordCount(): number {
    return this.replyText.trim().split(/\s+/).filter(Boolean).length;
  }

  submit(): void {
    if (this.replyText.trim()) {
      this.submitted.emit({ text: this.replyText });
    }
  }
}
