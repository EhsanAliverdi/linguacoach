import { Component, ElementRef, OnChanges, OnDestroy, SimpleChanges, ViewChild, computed, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Formio } from '@formio/js';
import { QUIZ_EDIT_FORM_OVERRIDES } from './quiz-edit-tab';

/**
 * The "Premium" Form.io palette group (signature, datamap, and other components requiring a
 * Form.io Enterprise/hosted-project license) is hidden — none of its components are in the
 * backend's (IFormIoSchemaValidationService) approved allow-list, and they'd need a licensed
 * Form.io project to even render correctly, which this app deliberately never configures (schemas
 * render fully client-side, no Form.io project URL is ever set). Basic/Advanced/Layout/Data stay
 * fully shown — restricting individual component keys within those groups down to the exact
 * backend allow-list would need per-key verification against Form.io's own group membership this
 * hasn't been done for, so this is a narrower, verified-safe restriction, not a 1:1 mirror of the
 * backend list. The backend remains the actual security boundary regardless — it rejects
 * script/eval properties, external data sources, and answer-leak keys regardless of what the
 * builder lets an admin drag onto the canvas, so a draft using a disallowed component still gets a
 * clear 400 on save rather than silently succeeding.
 *
 * `editForm` adds the "Quiz" tab to the six basic input types' own component-settings modal —
 * shared identically by every consumer of this component (onboarding + placement), since it's a
 * single module-level option object, not per-caller configuration.
 */
const BUILDER_OPTIONS = {
  noDefaultSubmitButton: false,
  editForm: QUIZ_EDIT_FORM_OVERRIDES,
  builder: { premium: false },
};

/** Wraps any existing flat components in a single wizard page when switching from single-page
 *  to multi-step display, so no authored fields are lost. */
function ensureWizardHasPage(schema: any): any {
  const comps: any[] = Array.isArray(schema?.components) ? schema.components : [];
  const hasPanel = comps.some((c: any) => c?.type === 'panel');
  if (hasPanel) return schema;
  return {
    ...schema,
    components: [{
      type: 'panel', breadcrumb: 'Page 1', title: 'Page 1',
      label: 'Page 1', key: 'page1', components: comps,
    }],
  };
}

/**
 * @formio/js v5 (unlike the older v4 "formiojs" package a reference Form.io+Tailwind project was
 * built against) already wires up the sidebar accordion's collapse/expand click handling
 * internally — no Bootstrap JS needed, and no custom click listener should be added here. An
 * earlier version of this function added its own delegated listener that duplicated Form.io's own
 * internal toggle, causing the two to cancel each other out (a group would open then immediately
 * close again on the very same click). All that's needed is opening the first group by default so
 * the palette isn't empty on first render — the CSS making `.show` actually visible still lives
 * globally in styles.css (`.builder-sidebar .collapse[.show]`), since that part of Form.io's own
 * theme isn't loaded.
 */
function initSidebarAccordion(root: HTMLElement): () => void {
  const first = root.querySelector<HTMLElement>('.builder-sidebar .collapse');
  if (first && !first.classList.contains('show')) {
    first.classList.add('show');
    const btn = root.querySelector(`[data-target="#${first.id}"],[data-bs-target="#${first.id}"]`);
    if (btn) (btn as HTMLElement).setAttribute('aria-expanded', 'true');
  }
  return () => {};
}

/**
 * Thin standalone wrapper around @formio/js's plain Formio.builder() API. Used by admin onboarding
 * template authoring and (additively) admin placement-item authoring. Not @formio/angular — see
 * FormioRendererComponent for why. Styling lives entirely in the global styles.css
 * (`.formio-builder-shell`/`.formio-scope`/`.builder-sidebar` etc.) — Formio.builder() injects raw
 * DOM outside Angular's template compiler, so per-component scoped styles never reach it.
 */
@Component({
  selector: 'app-formio-builder',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (showDisplayModeToggle()) {
      <div class="sf-formio-display-toggle" style="display:flex; align-items:center; gap:16px; padding: 0 4px 12px;">
        <span style="font-size:14px; font-weight:500; color:#374151;">Form type:</span>
        <label style="display:flex; align-items:center; gap:6px; cursor:pointer;">
          <input type="radio" name="formioDisplayMode" value="form"
            [checked]="displayMode() === 'form'" (change)="setDisplayMode('form')" />
          <span style="font-size:14px;">Single page</span>
        </label>
        <label style="display:flex; align-items:center; gap:6px; cursor:pointer;">
          <input type="radio" name="formioDisplayMode" value="wizard"
            [checked]="displayMode() === 'wizard'" (change)="setDisplayMode('wizard')" />
          <span style="font-size:14px;">Multi-step wizard</span>
        </label>
      </div>
    }
    <div class="formio-builder-shell formio-scope">
      <div #host></div>
    </div>
  `,
})
export class FormioBuilderComponent implements OnChanges, OnDestroy {
  /** Parsed Form.io schema JSON object to seed the builder with (not a string). */
  schema = input<any>(null);

  /** Emits the updated schema object on every builder change. */
  schemaChange = output<any>();

  /** Opt-in: shows a single-page/multi-step-wizard toggle above the builder canvas. Off by
   *  default — placement items are always single-schema and never pass this input. */
  showDisplayModeToggle = input(false);

  /** Emitted whenever the admin switches display mode via the toggle above. */
  displayModeChange = output<'form' | 'wizard'>();

  displayMode = computed<'form' | 'wizard'>(() => this.schema()?.display === 'wizard' ? 'wizard' : 'form');

  @ViewChild('host', { static: true }) host!: ElementRef<HTMLDivElement>;

  private builder: any = null;
  private built = false;
  private sidebarCleanup: (() => void) | null = null;
  private dialogObserver: MutationObserver | null = null;
  private wizardHeaderObserver: MutationObserver | null = null;

  /** Switches between single-page and multi-step wizard display — requires a full builder
   *  teardown+recreate, since Form.io's builder can't do this in-place via setForm(). */
  setDisplayMode(mode: 'form' | 'wizard'): void {
    let schema = this.getSchema();
    schema = { ...schema, display: mode };
    if (mode === 'wizard') schema = ensureWizardHasPage(schema);
    this.rebuild(schema);
    this.schemaChange.emit(schema);
    this.displayModeChange.emit(mode);
  }

  ngOnChanges(changes: SimpleChanges): void {
    // Only (re)build once per host — Form.io's builder owns its own internal editing state after
    // that; re-creating it on every incoming schema patch (e.g. from our own schemaChange loop
    // being echoed back by a parent) would fight the admin mid-edit. Use rebuild() (below) to force
    // a fresh builder, e.g. when switching between single-page and wizard display modes.
    if (!this.built) {
      this.buildBuilder();
    }
  }

  ngOnDestroy(): void {
    this.destroyBuilder();
  }

  /** Current schema straight from the live builder instance (more current than the `schema` input,
   *  which only reflects what was last passed in). */
  getSchema(): any {
    return this.builder?.schema ?? this.schema();
  }

  /** Forces a full teardown+recreate of the builder with a new seed schema — required when
   *  switching between 'form' and 'wizard' display modes, which Form.io's builder can't do
   *  in-place via setForm(). */
  rebuild(newSchema: any): void {
    this.destroyBuilder();
    this.buildBuilder(newSchema);
  }

  private buildBuilder(seedOverride?: any): void {
    if (!this.host?.nativeElement) return;
    this.built = true;
    const seed = seedOverride ?? this.schema() ?? { display: 'form', components: [] };

    Formio.builder(this.host.nativeElement, seed, BUILDER_OPTIONS).then((instance: any) => {
      this.builder = instance;

      this.sidebarCleanup = initSidebarAccordion(this.host.nativeElement);

      instance.on('change', () => {
        // Read from the captured `instance`, not `this.builder` — a trailing 'change' event can
        // still fire asynchronously from an instance that rebuild()/destroyBuilder() already
        // nulled out `this.builder` for, which would otherwise throw here.
        if (this.builder !== instance) return;
        this.schemaChange.emit(instance.schema);
      });

      // formiojs appends .formio-dialog directly to <body> (component settings modal) — toggle a
      // body class so surrounding chrome (sidebar/header) can't receive clicks while it's open.
      this.dialogObserver = new MutationObserver(() => {
        const hasDialog = !!document.body.querySelector('.formio-dialog');
        document.body.classList.toggle('formio-dialog-open', hasDialog);
      });
      this.dialogObserver.observe(document.body, { childList: true });

      this.installWizardHeaderEditing();
    });
  }

  private destroyBuilder(): void {
    this.sidebarCleanup?.();
    this.sidebarCleanup = null;
    this.dialogObserver?.disconnect();
    this.dialogObserver = null;
    this.wizardHeaderObserver?.disconnect();
    this.wizardHeaderObserver = null;
    document.removeEventListener('click', this.onWizardHeaderClick);
    document.body.classList.remove('formio-dialog-open');

    if (this.builder) {
      try {
        this.builder.instance?.destroy?.(true);
      } catch {
        // best-effort teardown
      }
      this.builder = null;
    }
    this.built = false;
  }

  // ── Wizard page-tab editing (rename/delete/move) ──────────────────────────
  // Form.io's own wizard builder renders a plain row of page tabs (.wizard-pages) with no way to
  // rename, delete, or reorder pages from the canvas — we inject small icon buttons onto each tab
  // label ourselves and wire their clicks to schema mutations, mirroring the proven approach from
  // a reference Form.io+Tailwind project.

  private installWizardHeaderEditing(): void {
    const root = this.host.nativeElement;
    const render = () => this.renderWizardHeaderEditing();
    render();
    this.wizardHeaderObserver?.disconnect();
    this.wizardHeaderObserver = new MutationObserver(render);
    this.wizardHeaderObserver.observe(root, { childList: true, subtree: true });
    // Delegated from `document` for the same reason as the sidebar accordion above — Form.io can
    // detach/replace its rendered subtree after this listener is first attached.
    document.addEventListener('click', this.onWizardHeaderClick);
  }

  private onWizardHeaderClick = (event: Event): void => {
    const button = (event.target as HTMLElement)?.closest<HTMLButtonElement>('[data-sf-wizard-action]');
    if (!button) return;
    event.preventDefault();
    event.stopPropagation();

    const action = button.dataset['sfWizardAction'];
    const index = Number(button.dataset['sfWizardIndex']);
    if (!Number.isFinite(index)) return;

    if (action === 'rename') this.renameWizardPage(index);
    if (action === 'delete') this.deleteWizardPage(index);
    if (action === 'left') this.moveWizardPage(index, -1);
    if (action === 'right') this.moveWizardPage(index, 1);
  };

  private renderWizardHeaderEditing(): void {
    const root = this.host?.nativeElement;
    if (!root) return;

    const header = root.querySelector<HTMLElement>('.wizard-pages');
    if (!header || header.dataset['sfWizardEditable'] === 'true') return;

    const items = Array.from(header.querySelectorAll<HTMLElement>('li:not(.wizard-add-page)'));
    if (!items.length) return;
    header.dataset['sfWizardEditable'] = 'true';
    items.forEach((item, index) => {
      const label = item.querySelector<HTMLElement>('.wizard-page-label');
      if (!label || label.querySelector('[data-sf-wizard-action]')) return;

      label.insertAdjacentHTML('beforeend', `
        <span class="sf-wizard-page-actions">
          <button type="button" data-sf-wizard-action="rename" data-sf-wizard-index="${index}" title="Rename page">
            <i class="bi bi-pencil"></i>
          </button>
          <button type="button" data-sf-wizard-action="delete" data-sf-wizard-index="${index}" title="Delete page">
            <i class="bi bi-x-lg"></i>
          </button>
          ${index > 0 ? `<button type="button" data-sf-wizard-action="left" data-sf-wizard-index="${index}" title="Move left"><i class="bi bi-arrow-left"></i></button>` : ''}
          ${index < items.length - 1 ? `<button type="button" data-sf-wizard-action="right" data-sf-wizard-index="${index}" title="Move right"><i class="bi bi-arrow-right"></i></button>` : ''}
        </span>
      `);
    });
  }

  private renameWizardPage(index: number): void {
    const schema = structuredClone(this.getSchema());
    const components = Array.isArray(schema.components) ? schema.components : [];
    const current = components[index];
    if (!current) return;

    const title = prompt('Rename page:', current.breadcrumb || current.title || current.label || `Page ${index + 1}`);
    if (!title?.trim()) return;

    components[index] = { ...current, breadcrumb: title.trim(), title: title.trim(), label: title.trim() };
    this.applyWizardSchema(schema);
  }

  private deleteWizardPage(index: number): void {
    const schema = structuredClone(this.getSchema());
    const components = Array.isArray(schema.components) ? schema.components : [];
    if (components.length <= 1 || !components[index]) return;
    if (!confirm('Delete this wizard page? Components on the page will be removed.')) return;

    components.splice(index, 1);
    this.applyWizardSchema(schema);
  }

  private moveWizardPage(index: number, direction: -1 | 1): void {
    const schema = structuredClone(this.getSchema());
    const components = Array.isArray(schema.components) ? schema.components : [];
    const target = index + direction;
    if (target < 0 || target >= components.length) return;

    [components[index], components[target]] = [components[target], components[index]];
    this.applyWizardSchema(schema);
  }

  private applyWizardSchema(schema: any): void {
    this.builder?.setForm(schema);
    this.schemaChange.emit(structuredClone(schema));
    setTimeout(() => this.renderWizardHeaderEditing(), 0);
  }
}
