import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'sp-chip',
  standalone: true,
  imports: [CommonModule],
  template: `
    <button
      type="button"
      class="sp-pref-chip"
      [class.sp-pref-chip--on]="selected"
      [attr.aria-pressed]="selected"
      [attr.disabled]="disabled ? true : null"
      (click)="toggle.emit()"
    ><ng-content /></button>
  `,
})
export class StudentChipComponent {
  @Input() selected = false;
  @Input() disabled = false;
  @Output() toggle = new EventEmitter<void>();
}
