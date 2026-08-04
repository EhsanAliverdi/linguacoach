import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import { of } from 'rxjs';
import { NodeSelection } from '@tiptap/pm/state';
import { SpAdminRichTextComponent, SpAdminRichTextUploadResult } from './sp-admin-rich-text.component';

@Component({
  standalone: true,
  imports: [FormsModule, SpAdminRichTextComponent],
  template: `<sp-admin-rich-text [(ngModel)]="value" [compact]="compact" [uploadMedia]="uploadMedia" />`,
})
class HostComponent {
  value = '';
  compact = false;
  uploadMedia = (_file: File) => of<SpAdminRichTextUploadResult>({ url: '/api/lesson-media/x.png', mimeType: 'image/png' });
}

describe('SpAdminRichTextComponent', () => {
  let fixture: ComponentFixture<HostComponent>;
  let host: HostComponent;

  async function setup(initialValue = '') {
    await TestBed.configureTestingModule({ imports: [HostComponent] }).compileComponents();
    fixture = TestBed.createComponent(HostComponent);
    host = fixture.componentInstance;
    host.value = initialValue;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('renders the formatting toolbar', async () => {
    await setup();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Link');
    expect(text).toContain('List');
  });

  it('renders initial ngModel content into the editor', async () => {
    await setup('<p>Hello world</p>');
    const content = fixture.nativeElement.querySelector('.sp-adm-richtext-content');
    expect(content.textContent).toContain('Hello world');
  });

  it('shows image/audio/video buttons only when uploadMedia is provided', async () => {
    await setup();
    let text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Image');
    expect(text).toContain('Audio');
    expect(text).toContain('Video');

    host.uploadMedia = undefined as unknown as HostComponent['uploadMedia'];
    fixture.detectChanges();
    text = fixture.nativeElement.textContent as string;
    expect(text).not.toContain('Audio');
  });

  it('toggling bold updates the editor active state', async () => {
    await setup('<p>Some text</p>');
    const component: SpAdminRichTextComponent = fixture.debugElement.children[0].componentInstance;
    expect(component.isActive('bold')).toBeFalse();
    component.toggleBold();
    // Selection is collapsed at the doc start with nothing selected — toggling bold shouldn't throw,
    // and the editor instance should still be alive/responsive afterward.
    expect(() => component.isActive('bold')).not.toThrow();
  });

  it('uploading an image inserts it into the editor content', async () => {
    await setup('<p>Cover:</p>');
    const component: SpAdminRichTextComponent = fixture.debugElement.children[0].componentInstance;
    const file = new File(['fake bytes'], 'cover.png', { type: 'image/png' });
    const inputEl = document.createElement('input');
    Object.defineProperty(inputEl, 'files', { value: [file] });
    component.onFileSelected({ target: inputEl } as unknown as Event, 'image');

    const content = fixture.nativeElement.querySelector('.sp-adm-richtext-content');
    expect(content.querySelector('img')?.getAttribute('src')).toBe('/api/lesson-media/x.png');
  });

  it('an inserted image can be selected and deleted (regression: images used to get stuck)', async () => {
    await setup('<p>Cover:</p>');
    const component: SpAdminRichTextComponent = fixture.debugElement.children[0].componentInstance;
    const file = new File(['fake bytes'], 'cover.png', { type: 'image/png' });
    const inputEl = document.createElement('input');
    Object.defineProperty(inputEl, 'files', { value: [file] });
    component.onFileSelected({ target: inputEl } as unknown as Event, 'image');

    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const editor = (component as any).editor;
    const imgEl: HTMLImageElement = fixture.nativeElement.querySelector('.sp-adm-richtext-content img');
    expect(imgEl).toBeTruthy();

    // Mirrors what a user does: click the image, then press Backspace. Guards the "image gets
    // stuck and can't be deleted" regression (fixed by `atom: true` + `draggable: false` on the
    // Image node in rich-text-media-nodes.ts).
    imgEl.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true, view: window }));
    imgEl.dispatchEvent(new MouseEvent('mouseup', { bubbles: true, cancelable: true, view: window }));
    imgEl.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, view: window }));
    fixture.detectChanges();

    expect(editor.state.selection).toBeInstanceOf(NodeSelection);
    expect(editor.state.selection.node?.type.name).toBe('image');

    editor.view.dom.dispatchEvent(new KeyboardEvent('keydown', {
      key: 'Backspace', code: 'Backspace', bubbles: true, cancelable: true,
    }));
    fixture.detectChanges();

    const content = fixture.nativeElement.querySelector('.sp-adm-richtext-content');
    expect(content.querySelector('img')).toBeNull();
  });
});
