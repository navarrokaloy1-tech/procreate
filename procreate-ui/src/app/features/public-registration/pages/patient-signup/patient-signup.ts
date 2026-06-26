import { Component, ChangeDetectorRef } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ApiService } from '../../../../core/services/api';

interface RegisteredPatient {
  id: number;
  patientCode: string;
  firstName: string;
  lastName: string;
  middleName: string;
}

@Component({
  selector: 'app-patient-signup',
  standalone: false,
  templateUrl: './patient-signup.html',
  styleUrl: './patient-signup.scss',
})
export class PatientSignup {
  signupForm: FormGroup;
  isSubmitting = false;
  errorMessage = '';

  // Ticket state shown after a successful registration
  registered: RegisteredPatient | null = null;
  registeredAt: Date | null = null;

  genderOptions = ['Male', 'Female', 'Other'];
  bloodTypeOptions = ['A+', 'A-', 'B+', 'B-', 'AB+', 'AB-', 'O+', 'O-', 'Unknown'];

  constructor(
    private fb: FormBuilder,
    private apiService: ApiService,
    private cdr: ChangeDetectorRef
  ) {
    this.signupForm = this.fb.group({
      firstName: ['', [Validators.required]],
      lastName: ['', [Validators.required]],
      middleName: [''],
      dateOfBirth: ['', [Validators.required]],
      gender: ['', [Validators.required]],
      contactNumber: ['', [Validators.required]],
      email: ['', [Validators.email]],
      address: [''],
      bloodType: [''],
      emergencyContactName: [''],
      emergencyContactNumber: [''],
    });
  }

  get f() {
    return this.signupForm.controls;
  }

  /** The numeric portion of the patient code, used as the big queue number. */
  get queueNumber(): string {
    if (!this.registered) return '';
    const parts = this.registered.patientCode.split('-');
    return parts.length ? parts[parts.length - 1] : this.registered.patientCode;
  }

  get fullName(): string {
    if (!this.registered) return '';
    return [this.registered.firstName, this.registered.middleName, this.registered.lastName]
      .filter((p) => p && p.trim())
      .join(' ');
  }

  submit(): void {
    if (this.signupForm.invalid) {
      this.signupForm.markAllAsTouched();
      this.errorMessage = 'Please complete all required fields.';
      return;
    }

    this.isSubmitting = true;
    this.errorMessage = '';

    this.apiService.post<RegisteredPatient>('patients', this.signupForm.value).subscribe({
      next: (patient) => {
        this.registered = patient;
        this.registeredAt = new Date();
        this.isSubmitting = false;
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.errorMessage =
          err?.error?.message || 'Registration failed. Please call a staff member for help.';
        this.isSubmitting = false;
        this.cdr.markForCheck();
      },
    });
  }

  registerAnother(): void {
    this.registered = null;
    this.registeredAt = null;
    this.errorMessage = '';
    this.signupForm.reset({ gender: '', bloodType: '' });
    this.cdr.markForCheck();
  }

  printTicket(): void {
    window.print();
  }
}
