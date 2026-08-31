import { Component, OnInit, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { Observable, Subject, Subscription } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { ApiService } from '../../../../core/services/api';
import { AuthService } from '../../../../core/services/auth';

export interface ManagedUser {
  id: number;
  username: string;
  fullName: string;
  firstName: string;
  middleName: string;
  lastName: string;
  suffix: string;
  sex: string;
  birthday: string | null;
  contactNumber: string;
  address: string;
  email: string;
  role: string;
  roleLabel: string;
  isActive: boolean;
  mustChangePassword: boolean;
  createdAt: string;
  lastLoginAt: string | null;
  isOnline: boolean;
}

interface RoleOption {
  value: string;
  label: string;
}

@Component({
  selector: 'app-user-list',
  standalone: false,
  templateUrl: './user-list.html',
  styleUrl: './user-list.scss',
})
export class UserListComponent implements OnInit, OnDestroy {
  users: ManagedUser[] = [];
  roles: RoleOption[] = [];
  totalCount = 0;

  pageIndex = 0;
  pageSize = 5;
  readonly pageSizeOptions = [5, 10, 25];

  searchTerm = '';
  roleFilter = '';
  isFilterOpen = false;

  isLoading = false;
  errorMessage = '';

  // --- Create / edit sheet ---
  isModalOpen = false;
  isSaving = false;
  modalError = '';
  editingId: number | null = null;
  form = this.blankForm();

  /**
   * A freshly issued temporary password. Held only long enough to show it —
   * the server hashes it and can never return it again.
   */
  issued: { username: string; password: string; message: string } | null = null;

  private searchSubject = new Subject<string>();
  private subscriptions = new Subscription();

  constructor(
    private api: ApiService,
    private auth: AuthService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.subscriptions.add(
      this.searchSubject.pipe(debounceTime(300), distinctUntilChanged()).subscribe((term) => {
        this.searchTerm = term;
        this.pageIndex = 0;
        this.load();
      })
    );

    this.loadRoles();
    this.load();
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  private blankForm() {
    return {
      firstName: '',
      middleName: '',
      lastName: '',
      suffix: '',
      sex: '',
      birthday: '',
      contactNumber: '',
      address: '',
      email: '',
      role: 'Staff',
      isActive: true,
    };
  }

  // ----------------------------------------------------------
  // Loading
  // ----------------------------------------------------------

  loadRoles(): void {
    this.api.get<RoleOption[]>('users/roles').subscribe({
      next: (roles) => {
        this.roles = roles;
        this.cdr.markForCheck();
      },
      error: () => this.cdr.markForCheck(),
    });
  }

  load(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.api
      .get<{ total: number; data: ManagedUser[] }>('users', {
        search: this.searchTerm,
        role: this.roleFilter,
        page: this.pageIndex + 1,
        pageSize: this.pageSize,
      })
      .subscribe({
        next: (res) => {
          this.users = res.data;
          this.totalCount = res.total;
          this.isLoading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.errorMessage = 'Could not load user accounts.';
          this.isLoading = false;
          this.cdr.markForCheck();
        },
      });
  }

  onSearch(term: string): void {
    this.searchSubject.next(term);
  }

  toggleFilter(): void {
    this.isFilterOpen = !this.isFilterOpen;
  }

  onRoleFilter(role: string): void {
    this.roleFilter = role;
    this.pageIndex = 0;
    this.isFilterOpen = false;
    this.load();
  }

  onPageSizeChange(size: string | number): void {
    const parsed = Number(size);
    if (!Number.isFinite(parsed) || parsed < 1) return;

    this.pageSize = Math.floor(parsed);
    this.pageIndex = 0;
    this.load();
  }

  onPageChange(page: number): void {
    this.pageIndex = page;
    this.load();
  }

  // ----------------------------------------------------------
  // Create / edit
  // ----------------------------------------------------------

  openAdd(): void {
    this.editingId = null;
    this.form = this.blankForm();
    this.modalError = '';
    this.isModalOpen = true;
  }

  openEdit(user: ManagedUser): void {
    this.editingId = user.id;
    this.form = {
      firstName: user.firstName,
      middleName: user.middleName,
      lastName: user.lastName,
      suffix: user.suffix,
      sex: user.sex,
      birthday: user.birthday ? user.birthday.substring(0, 10) : '',
      contactNumber: user.contactNumber,
      address: user.address,
      email: user.email,
      role: user.role,
      isActive: user.isActive,
    };
    this.modalError = '';
    this.isModalOpen = true;
  }

  closeModal(): void {
    this.isModalOpen = false;
    this.modalError = '';
    this.isSaving = false;
  }

  save(): void {
    if (!this.form.firstName.trim()) {
      this.modalError = 'Enter a first name.';
      return;
    }
    if (!this.form.email.trim()) {
      this.modalError = 'Enter an email address.';
      return;
    }

    const payload = {
      ...this.form,
      birthday: this.form.birthday || null,
    };

    this.isSaving = true;

    // Creating returns the new account plus a one-time password; updating
    // returns only the account. The shapes differ, so the union is widened
    // rather than making the caller handle two incompatible signatures.
    const request: Observable<any> = this.editingId
      ? this.api.put<ManagedUser>(`users/${this.editingId}`, payload)
      : this.api.post<{ user: ManagedUser; temporaryPassword: string; message: string }>(
          'users',
          payload
        );

    request.subscribe({
      next: (res: any) => {
        this.isSaving = false;
        this.closeModal();

        // Creating an account hands back a one-time password that has to reach
        // the holder now; nothing can retrieve it afterwards.
        if (res?.temporaryPassword) {
          this.issued = {
            username: res.user.username,
            password: res.temporaryPassword,
            message: res.message,
          };
        }

        this.load();
        this.cdr.markForCheck();
      },
      error: (err: any) => {
        this.isSaving = false;
        this.modalError = err?.error?.message ?? 'Saving failed. Please try again.';
        this.cdr.markForCheck();
      },
    });
  }

  resetPassword(user: ManagedUser): void {
    const confirmed = window.confirm(
      `Issue a new temporary password for ${user.fullName}? Their current password stops working immediately.`
    );
    if (!confirmed) return;

    this.api
      .post<{ temporaryPassword: string; message: string }>(`users/${user.id}/reset-password`, {})
      .subscribe({
        next: (res) => {
          this.issued = {
            username: user.username,
            password: res.temporaryPassword,
            message: res.message,
          };
          this.load();
          this.cdr.markForCheck();
        },
        error: (err) => {
          window.alert(err?.error?.message ?? 'Could not reset the password.');
          this.cdr.markForCheck();
        },
      });
  }

  remove(user: ManagedUser): void {
    const confirmed = window.confirm(
      `Delete the account for ${user.fullName}? This cannot be undone.`
    );
    if (!confirmed) return;

    this.api.delete<void>(`users/${user.id}`).subscribe({
      next: () => {
        this.load();
        this.cdr.markForCheck();
      },
      error: (err) => {
        window.alert(err?.error?.message ?? 'Could not delete the account.');
        this.cdr.markForCheck();
      },
    });
  }

  dismissIssued(): void {
    this.issued = null;
  }

  copyIssued(): void {
    if (!this.issued) return;
    navigator.clipboard?.writeText(this.issued.password);
  }

  // ----------------------------------------------------------
  // Presentation
  // ----------------------------------------------------------

  /** The signed-in account cannot be deleted, so its row shows a marker instead. */
  isCurrentUser(user: ManagedUser): boolean {
    return this.auth.currentUser?.id === user.id;
  }

  rolePill(role: string): string {
    switch (role) {
      case 'Admin':
        return 'pill pill--primary';
      case 'Doctor':
        return 'pill pill--info';
      case 'Cashier':
        return 'pill pill--warning';
      default:
        return 'pill';
    }
  }

  initials(name: string): string {
    return (name ?? '')
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0])
      .join('')
      .toUpperCase();
  }

  get rangeStart(): number {
    return this.totalCount === 0 ? 0 : this.pageIndex * this.pageSize + 1;
  }

  get rangeEnd(): number {
    return Math.min((this.pageIndex + 1) * this.pageSize, this.totalCount);
  }

  get totalPages(): number {
    return Math.ceil(this.totalCount / this.pageSize);
  }

  get pages(): number[] {
    const total = this.totalPages;
    if (total <= 7) return Array.from({ length: total }, (_, i) => i);

    const current = this.pageIndex;
    const pages: number[] = [0];
    if (current > 3) pages.push(-1);

    const start = Math.max(1, current - 1);
    const end = Math.min(total - 2, current + 1);
    for (let i = start; i <= end; i++) pages.push(i);

    if (current < total - 4) pages.push(-2);
    pages.push(total - 1);
    return pages;
  }
}
