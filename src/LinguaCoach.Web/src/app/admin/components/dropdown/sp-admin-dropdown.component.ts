import {
  Component,
  Input,
  Output,
  EventEmitter,
  ElementRef,
  HostListener,
  OnDestroy,
} from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * TailAdmin dropdown wrapper.
 * Source pattern: shared/components/ui/dropdown/dropdown.component.html
 * Classes: absolute z-40 rounded-xl border border-gray-200 bg-white shadow-theme-lg dark:border-gray-800 dark:bg-gray-dark
 *
 * Usage:
 *   <sp-admin-dropdown>
 *     <ng-container trigger>...</ng-container>
 *     <ng-container menu>...</ng-container>
 *   </sp-admin-dropdown>
 */
@Component({
  selector: 'sp-admin-dropdown',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-adm-dropdown relative inline-block">
      <div class="sp-adm-dropdown-trigger" (click)="toggle()" (keydown.enter)="toggle()" (keydown.space)="toggle()">
        <ng-content select="[trigger]" />
      </div>
      @if (isOpen) {
        <div
          #menuRef
          role="menu"
          [class]="menuClasses"
          (click)="closeOnMenuClick && close()"
        >
          <ng-content select="[menu]" />
        </div>
      }
    </div>
  `,
  styles: [`
    /* TailAdmin-backed: absolute z-40 rounded-xl border border-gray-200 bg-white shadow */
    :host { display: inline-block; }
  `],
})
export class SpAdminDropdownComponent implements OnDestroy {
  @Input() align: 'left' | 'right' = 'right';
  @Input() width: 'auto' | 'sm' | 'md' | 'lg' = 'md';
  @Input() closeOnMenuClick = true;
  @Input() isOpen = false;
  @Output() isOpenChange = new EventEmitter<boolean>();

  get menuClasses(): string {
    const align = this.align === 'right' ? 'right-0' : 'left-0';
    const widthMap: Record<string, string> = { auto: '', sm: 'w-40', md: 'w-48', lg: 'w-64' };
    const w = widthMap[this.width] ?? 'w-48';
    return `absolute z-40 ${align} mt-2 ${w} rounded-xl border border-gray-200 bg-white shadow-lg dark:border-gray-800 dark:bg-gray-900`;
  }

  constructor(private elRef: ElementRef<HTMLElement>) {}

  toggle(): void {
    this.isOpen = !this.isOpen;
    this.isOpenChange.emit(this.isOpen);
  }

  close(): void {
    this.isOpen = false;
    this.isOpenChange.emit(false);
  }

  @HostListener('document:mousedown', ['$event'])
  onDocumentMousedown(event: MouseEvent): void {
    if (this.isOpen && !this.elRef.nativeElement.contains(event.target as Node)) {
      this.close();
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.isOpen) this.close();
  }

  ngOnDestroy(): void {}
}
