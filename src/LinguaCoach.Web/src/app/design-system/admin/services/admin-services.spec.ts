import { TestBed } from '@angular/core/testing';
import { AdminDrawerService, AdminModalService, AdminToastService } from '../index';
import { ToastService } from '../../../core/services/toast.service';

describe('admin services', () => {
  it('shows and dismisses toast messages through the admin facade', () => {
    TestBed.configureTestingModule({});
    const adminToast = TestBed.inject(AdminToastService);
    const toast = TestBed.inject(ToastService);

    adminToast.warning('Watch usage');
    const message = toast.messages()[0];

    expect(message.kind).toBe('warning');
    expect(message.message).toBe('Watch usage');

    adminToast.dismiss(message.id);
    expect(toast.messages().length).toBe(0);
  });

  it('stores and clears modal confirm state', () => {
    TestBed.configureTestingModule({});
    const modal = TestBed.inject(AdminModalService);

    modal.confirm({ title: 'Archive', body: 'Archive student?', danger: true });
    expect(modal.confirmRequest()?.title).toBe('Archive');

    modal.close();
    expect(modal.confirmRequest()).toBeNull();
  });

  it('stores and clears drawer state', () => {
    TestBed.configureTestingModule({});
    const drawer = TestBed.inject(AdminDrawerService);

    drawer.open({ title: 'Student details', context: { id: 1 } });
    expect(drawer.state()?.title).toBe('Student details');

    drawer.close();
    expect(drawer.state()).toBeNull();
  });
});
