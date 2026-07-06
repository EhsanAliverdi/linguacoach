import {
  Component,
  ElementRef,
  OnChanges,
  OnDestroy,
  SimpleChanges,
  ViewChild,
  input,
  output,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Formio } from '@formio/js';

/**
 * Thin standalone wrapper around @formio/js's plain Formio.createForm() API — used to render
 * onboarding's admin-authored wizard and placement's per-item adaptive forms. Deliberately does
 * NOT use @formio/angular (pulls in ngx-bootstrap/Bootstrap CSS, conflicting with this project's
 * Tailwind design system).
 *
 * Styling lives in the global styles.css under `.formio-scope` (Formio.createForm() injects raw
 * DOM outside Angular's template compiler, so per-component scoped/emulated styles never reach
 * it — only global CSS matched by class name does).
 */
@Component({
  selector: 'app-formio-renderer',
  standalone: true,
  imports: [CommonModule],
  template: `<div class="formio-scope" #host></div>`,
})
export class FormioRendererComponent implements OnChanges, OnDestroy {
  /** Parsed Form.io schema JSON object (not a string). */
  schema = input.required<any>();
  /** Optional prefill submission data object (Formio submission.data shape, not the whole submission). */
  submissionData = input<any>(null);
  /** Disables the rendered form (e.g. while a submit request is in flight). */
  disabled = input<boolean>(false);

  /** Emits the Form.io submission's `.data` object whenever the form is submitted. */
  submit = output<any>();
  /** Emits the current submission `.data` object on every change — for debounced draft-saving. */
  change = output<any>();
  /** Emits once the Form.io instance has finished creating and is ready to use. */
  formReady = output<any>();

  @ViewChild('host', { static: true }) host!: ElementRef<HTMLDivElement>;

  private formInstance: any = null;
  private lastSchema: any = undefined;

  ngOnChanges(changes: SimpleChanges): void {
    const schemaChanged = changes['schema'] && changes['schema'].currentValue !== this.lastSchema;
    if (schemaChanged) {
      this.lastSchema = this.schema();
      this.buildForm();
      return;
    }
    if (changes['disabled'] && this.formInstance) {
      this.formInstance.disabled = this.disabled();
    }
  }

  ngOnDestroy(): void {
    this.destroyForm();
  }

  /** Triggers Form.io's own submission pipeline (validation + the 'submit' event) programmatically
   *  — useful if a caller wants its own "Submit" button instead of relying on the form's last-page
   *  submit button (Form.io wizards render one natively, which is usually sufficient). */
  submitForm(): void {
    this.formInstance?.submit();
  }

  private buildForm(): void {
    this.destroyForm();
    const schema = this.schema();
    if (!schema || !this.host?.nativeElement) return;

    Formio.createForm(this.host.nativeElement, schema, {
      submission: { data: this.submissionData() ?? {} },
      noAlerts: false,
    }).then((instance: any) => {
      this.formInstance = instance;
      if (this.disabled()) instance.disabled = true;

      instance.on('submit', (submission: any) => {
        this.submit.emit(submission?.data ?? {});
      });
      instance.on('change', (value: any) => {
        this.change.emit(value?.data ?? this.formInstance?.submission?.data ?? {});
      });

      this.formReady.emit(instance);
    });
  }

  private destroyForm(): void {
    if (this.formInstance) {
      try {
        this.formInstance.destroy(true);
      } catch {
        // best-effort teardown
      }
      this.formInstance = null;
    }
  }
}
