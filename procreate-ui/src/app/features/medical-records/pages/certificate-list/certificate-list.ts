import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { ApiService } from '../../../../core/services/api';
import { ComboOption } from '../../../../core/components/combo-select/combo-select';

interface Certificate {
  id: number;
  certificateNumber: string;
  patientId: number | null;
  issuedTo: string;
  patientCode: string;
  doctorId: number;
  doctorName: string;
  issueDate: string;
  template: string;
  diagnosis: string;
  recommendation: string;
  remarks: string;
  isWalkIn: boolean;
}

@Component({
  selector: 'app-certificate-list',
  standalone: false,
  templateUrl: './certificate-list.html',
  styleUrl: './certificate-list.scss',
})
export class CertificateListComponent implements OnInit {
  readonly templates = [
    { id: 'General', label: 'General', icon: 'file-text' },
    { id: 'Work', label: 'Work', icon: 'briefcase' },
    { id: 'School', label: 'School', icon: 'graduation-cap' },
  ];

  certificates: Certificate[] = [];
  totalCount = 0;
  pageIndex = 0;
  pageSize = 10;
  readonly pageSizeOptions = [10, 25, 50];

  searchTerm = '';
  templateFilter = '';
  isLoading = false;
  errorMessage = '';

  /** The issue sheet itself lives in app-certificate-form. */
  isModalOpen = false;

  private searchSubject = new Subject<string>();

  /** The certificate currently rendered into the print-only sheet. */
  printing: Certificate | null = null;

  constructor(private api: ApiService, private cdr: ChangeDetectorRef) {}

  ngOnInit(): void {
    this.searchSubject.pipe(debounceTime(300), distinctUntilChanged()).subscribe((term) => {
      this.searchTerm = term;
      this.pageIndex = 0;
      this.load();
    });

    this.load();
  }

  // ----------------------------------------------------------
  // Loading
  // ----------------------------------------------------------

  load(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.api
      .get<{ total: number; data: Certificate[] }>('medicalcertificates', {
        search: this.searchTerm,
        template: this.templateFilter,
        page: this.pageIndex + 1,
        pageSize: this.pageSize,
      })
      .subscribe({
        next: (res) => {
          this.certificates = res.data;
          this.totalCount = res.total;
          this.isLoading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.errorMessage = 'Could not load certificates.';
          this.isLoading = false;
          this.cdr.markForCheck();
        },
      });
  }

  get templateOptions(): ComboOption[] {
    return [{ value: '', label: 'All Types' }].concat(
      this.templates.map((t) => ({ value: t.id, label: t.label }))
    );
  }

  get pageSizeSelection(): ComboOption[] {
    return this.pageSizeOptions.map((n) => ({ value: String(n), label: String(n) }));
  }

  onSearch(term: string): void {
    this.searchSubject.next(term);
  }

  onTemplateFilter(value: string): void {
    this.templateFilter = value;
    this.pageIndex = 0;
    this.load();
  }

  onPageSizeChange(value: string): void {
    this.pageSize = Number(value);
    this.pageIndex = 0;
    this.load();
  }

  onPageChange(page: number): void {
    this.pageIndex = page;
    this.load();
  }

  // ----------------------------------------------------------
  // Issue sheet
  // ----------------------------------------------------------

  openModal(): void {
    this.isModalOpen = true;
  }

  closeModal(): void {
    this.isModalOpen = false;
  }

  /** A new certificate exists, so the first page is the one to show. */
  onIssued(): void {
    this.isModalOpen = false;
    this.pageIndex = 0;
    this.load();
  }

  remove(certificate: Certificate): void {
    if (!window.confirm(`Delete certificate ${certificate.certificateNumber}?`)) return;

    this.api.delete<void>(`medicalcertificates/${certificate.id}`).subscribe({
      next: () => {
        this.load();
        this.cdr.markForCheck();
      },
      error: () => {
        this.errorMessage = 'Could not delete that certificate.';
        this.cdr.markForCheck();
      },
    });
  }

  /** Renders the certificate into the print-only sheet and opens the dialog. */
  print(certificate: Certificate): void {
    this.printing = certificate;
    this.cdr.detectChanges();

    document.body.classList.add('printing-slip');

    const cleanup = () => {
      document.body.classList.remove('printing-slip');
      this.printing = null;
      this.cdr.markForCheck();
      window.removeEventListener('afterprint', cleanup);
    };

    window.addEventListener('afterprint', cleanup);
    window.print();
    setTimeout(cleanup, 1000);
  }

  templatePill(template: string): string {
    switch (template) {
      case 'Work': return 'tag tag--info';
      case 'School': return 'tag tag--success';
      default: return 'tag tag--primary';
    }
  }

  templateIcon(template: string): string {
    return this.templates.find((t) => t.id === template)?.icon ?? 'file-text';
  }

  initials(name: string): string {
    return name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((p) => p[0])
      .join('')
      .toLowerCase();
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
    return Array.from({ length: this.totalPages }, (_, i) => i);
  }
}
