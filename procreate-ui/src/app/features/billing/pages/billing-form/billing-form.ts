import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService } from '../../../../core/services/api';

export interface VisitForBilling {
  id: number;
  visitCode: string;
  patientName: string;
  patientCode: string;
  visitDate: string;
  tests: { testName: string; price: number }[];
}

export interface BillPayload {
  visitId: number;
  discountAmount: number;
  amountPaid: number;
  paymentMethod: string;
  notes: string;
}

@Component({
  selector: 'app-billing-form',
  standalone: false,
  templateUrl: './billing-form.html',
  styleUrl: './billing-form.scss',
})
export class BillingForm implements OnInit {
  visitId!: number;
  visit: VisitForBilling | null = null;
  isLoading = true;
  isSaving = false;
  errorMessage = '';
  successMessage = '';

  billForm!: FormGroup;

  paymentMethods = ['Cash', 'Card', 'GCash', 'PhilHealth', 'HMO'];

  constructor(
    private fb: FormBuilder,
    private route: ActivatedRoute,
    private router: Router,
    private apiService: ApiService,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.billForm = this.fb.group({
      discountAmount: [0, [Validators.min(0)]],
      amountPaid: [null, [Validators.required, Validators.min(0)]],
      paymentMethod: ['Cash', Validators.required],
      notes: [''],
    });

    this.route.params.subscribe((params) => {
      this.visitId = +params['visitId'];
      this.loadVisit();
      this.cdr.markForCheck();
    });
  }

  loadVisit(): void {
    this.isLoading = true;
    this.apiService.get<VisitForBilling>('visits/' + this.visitId).subscribe({
      next: (visit) => {
        this.visit = visit;
        this.isLoading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.errorMessage = 'Failed to load visit information.';
        this.isLoading = false;
        this.cdr.markForCheck();
      },
    });
  }

  get subtotal(): number {
    if (!this.visit) return 0;
    return this.visit.tests.reduce((sum, t) => sum + t.price, 0);
  }

  get discount(): number {
    return this.billForm.get('discountAmount')?.value || 0;
  }

  get totalAmount(): number {
    return this.subtotal - this.discount;
  }

  get amountPaid(): number {
    return this.billForm.get('amountPaid')?.value || 0;
  }

  get change(): number {
    return Math.max(0, this.amountPaid - this.totalAmount);
  }

  onSubmit(): void {
    if (this.billForm.invalid) {
      this.billForm.markAllAsTouched();
      return;
    }

    const paid = this.billForm.get('amountPaid')?.value || 0;
    if (paid < this.totalAmount) {
      this.billForm.patchValue({ amountPaid: this.totalAmount });
    }

    const formValue = this.billForm.value;
    const payload: BillPayload = {
      visitId: this.visitId,
      discountAmount: formValue.discountAmount || 0,
      amountPaid: formValue.amountPaid,
      paymentMethod: formValue.paymentMethod,
      notes: formValue.notes || '',
    };

    this.isSaving = true;
    this.errorMessage = '';

    this.apiService.post<any>('billing', payload).subscribe({
      next: () => {
        this.isSaving = false;
        this.successMessage = 'Bill created successfully!';
        setTimeout(() => {
          this.router.navigate(['/app/billing']);
          this.cdr.markForCheck();
        }, 1000);
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.isSaving = false;
        this.errorMessage =
          err?.error?.message || 'An error occurred while creating the bill.';
        this.cdr.markForCheck();
      },
    });
  }

  cancel(): void {
    this.router.navigate(['/app/billing']);
  }
}
