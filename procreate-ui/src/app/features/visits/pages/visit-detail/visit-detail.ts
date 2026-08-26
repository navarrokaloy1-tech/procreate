import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService } from '../../../../core/services/api';

interface VisitTest {
  testName: string;
  code: string;
  price: number;
}

interface LabOrder {
  id: number;
  status: string;
  specimenBarcode: string;
  collectedAt: string | null;
  processedAt: string | null;
  releasedAt: string | null;
}

interface VisitRecord {
  id: number;
  visitCode: string;
  patientId: number;
  patientName: string;
  patientCode: string;
  visitDate: string;
  referringPhysician: string;
  purpose: string;
  status: string;
  totalAmount: number;
  amountPaid: number;
  paymentStatus: string;
  paymentMethod: string;
  tests: VisitTest[];
  labOrders: LabOrder[];
}

/**
 * Read-only consultation record.
 *
 * The `:id` route previously rendered VisitForm, which ignores the parameter,
 * so "View" always showed an empty new-visit form.
 */
@Component({
  selector: 'app-visit-detail',
  standalone: false,
  templateUrl: './visit-detail.html',
  styleUrl: './visit-detail.scss',
})
export class VisitDetail implements OnInit {
  visit: VisitRecord | null = null;
  isLoading = true;
  errorMessage = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private apiService: ApiService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));

    if (!id) {
      this.errorMessage = 'No consultation was specified.';
      this.isLoading = false;
      return;
    }

    this.load(id);
  }

  private load(id: number): void {
    this.isLoading = true;

    this.apiService.get<VisitRecord>(`visits/${id}`).subscribe({
      next: (visit) => {
        this.visit = visit;
        this.isLoading = false;
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.errorMessage =
          err?.status === 404
            ? 'That consultation no longer exists.'
            : 'Could not load the consultation.';
        this.isLoading = false;
        this.cdr.markForCheck();
      },
    });
  }

  back(): void {
    this.router.navigate(['/app/visits']);
  }

  get balance(): number {
    if (!this.visit) return 0;
    return this.visit.totalAmount - this.visit.amountPaid;
  }

  statusPill(status: string): string {
    switch (status) {
      case 'Completed': return 'pill pill--success';
      case 'In Progress': return 'pill pill--warning';
      case 'Cancelled': return 'pill pill--danger';
      case 'Registered': return 'pill pill--info';
      default: return 'pill pill--neutral';
    }
  }

  paymentPill(status: string): string {
    switch (status) {
      case 'Paid': return 'pill pill--success';
      case 'Partial': return 'pill pill--warning';
      default: return 'pill pill--danger';
    }
  }

  orderPill(status: string): string {
    switch (status) {
      case 'Released': return 'pill pill--success';
      case 'Processed': return 'pill pill--info';
      case 'Collected': return 'pill pill--warning';
      default: return 'pill pill--neutral';
    }
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
}
