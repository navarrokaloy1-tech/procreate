import { Component, OnInit, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { Subject, Subscription } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { ApiService } from '../../../../core/services/api';

export interface LoginAttempt {
  id: number;
  usernameAttempted: string;
  fullName: string;
  role: string;
  method: string;        // Password | SSO
  audience: string;      // Staff | Patient
  success: boolean;
  failureReason: string;
  ipAddress: string;
  userAgent: string;
  createdAt: string;
}

interface ActivityResponse {
  total: number;
  page: number;
  pageSize: number;
  failures24h: number;
  data: LoginAttempt[];
}

@Component({
  selector: 'app-login-activity',
  standalone: false,
  templateUrl: './login-activity.html',
  styleUrl: './login-activity.scss',
})
export class LoginActivityComponent implements OnInit, OnDestroy {
  attempts: LoginAttempt[] = [];
  totalCount = 0;
  failures24h = 0;

  pageIndex = 0;
  pageSize = 25;
  readonly pageSizeOptions = [25, 50, 100];

  searchTerm = '';
  resultFilter = '';   // '' | 'success' | 'failure'
  methodFilter = '';   // '' | 'Password' | 'SSO'
  isFilterOpen = false;

  isLoading = false;
  errorMessage = '';

  private searchSubject = new Subject<string>();
  private subscriptions = new Subscription();

  constructor(private api: ApiService, private cdr: ChangeDetectorRef) {}

  ngOnInit(): void {
    this.subscriptions.add(
      this.searchSubject.pipe(debounceTime(300), distinctUntilChanged()).subscribe((term) => {
        this.searchTerm = term;
        this.pageIndex = 0;
        this.load();
      })
    );
    this.load();
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  load(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.api
      .get<ActivityResponse>('login-activity', {
        search: this.searchTerm,
        result: this.resultFilter,
        method: this.methodFilter,
        page: this.pageIndex + 1,
        pageSize: this.pageSize,
      })
      .subscribe({
        next: (res) => {
          this.attempts = res.data;
          this.totalCount = res.total;
          this.failures24h = res.failures24h;
          this.isLoading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.errorMessage = 'Could not load login activity.';
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

  setResultFilter(result: string): void {
    this.resultFilter = result;
    this.pageIndex = 0;
    this.isFilterOpen = false;
    this.load();
  }

  setMethodFilter(method: string): void {
    this.methodFilter = method;
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
  // Presentation
  // ----------------------------------------------------------

  /** A short, readable browser/OS hint from a raw user-agent string. */
  deviceOf(userAgent: string): string {
    const ua = userAgent ?? '';
    if (!ua) return 'Unknown';
    let os = 'Unknown';
    if (/Windows/i.test(ua)) os = 'Windows';
    else if (/iPhone|iPad|iOS/i.test(ua)) os = 'iOS';
    else if (/Mac OS X|Macintosh/i.test(ua)) os = 'macOS';
    else if (/Android/i.test(ua)) os = 'Android';
    else if (/Linux/i.test(ua)) os = 'Linux';

    let browser = '';
    if (/Edg\//i.test(ua)) browser = 'Edge';
    else if (/OPR\/|Opera/i.test(ua)) browser = 'Opera';
    else if (/Chrome\//i.test(ua)) browser = 'Chrome';
    else if (/Firefox\//i.test(ua)) browser = 'Firefox';
    else if (/Safari\//i.test(ua)) browser = 'Safari';

    return browser ? `${browser} · ${os}` : os;
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

  get activeFilterLabel(): string {
    const parts: string[] = [];
    if (this.resultFilter) parts.push(this.resultFilter === 'success' ? 'Successful' : 'Failed');
    if (this.methodFilter) parts.push(this.methodFilter);
    return parts.length ? parts.join(' · ') : 'All activity';
  }
}
