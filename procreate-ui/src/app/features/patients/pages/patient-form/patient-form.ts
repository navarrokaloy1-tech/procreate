import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService } from '../../../../core/services/api';
import { Patient } from '../patient-list/patient-list';

@Component({
  selector: 'app-patient-form',
  standalone: false,
  templateUrl: './patient-form.html',
  styleUrl: './patient-form.scss',
})
export class PatientFormComponent implements OnInit {
  isEditMode = false;
  patientId: number | null = null;
  isLoading = false;
  isSaving = false;
  errorMessage = '';
  successMessage = '';

  patientForm: FormGroup;

  genderOptions = ['Male', 'Female', 'Other'];
  bloodTypeOptions = ['A+', 'A-', 'B+', 'B-', 'AB+', 'AB-', 'O+', 'O-', 'Unknown'];

  constructor(
    private fb: FormBuilder,
    private route: ActivatedRoute,
    private router: Router,
    private apiService: ApiService,
    private cdr: ChangeDetectorRef
  ) {
    this.patientForm = this.fb.group({
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

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.isEditMode = true;
      this.patientId = Number(idParam);
      this.loadPatient(this.patientId);
    }
  }

  get f() {
    return this.patientForm.controls;
  }

  loadPatient(id: number): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.apiService.get<Patient>('patients/' + id).subscribe({
      next: (patient) => {
        this.patientForm.patchValue({
          firstName: patient.firstName,
          lastName: patient.lastName,
          middleName: patient.middleName,
          dateOfBirth: patient.dateOfBirth
            ? patient.dateOfBirth.substring(0, 10)
            : '',
          gender: patient.gender,
          contactNumber: patient.contactNumber,
          email: patient.email,
          address: patient.address,
          bloodType: patient.bloodType,
        });
        this.isLoading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.errorMessage = 'Failed to load patient data. Please try again.';
        this.isLoading = false;
        this.cdr.markForCheck();
      },
    });
  }

  onSubmit(): void {
    if (this.patientForm.invalid) {
      this.patientForm.markAllAsTouched();
      return;
    }

    this.isSaving = true;
    this.errorMessage = '';
    this.successMessage = '';

    const payload = this.patientForm.value;

    const request$ =
      this.isEditMode && this.patientId !== null
        ? this.apiService.put<Patient>('patients/' + this.patientId, payload)
        : this.apiService.post<Patient>('patients', payload);

    request$.subscribe({
      next: () => {
        this.successMessage = this.isEditMode
          ? 'Patient updated successfully.'
          : 'Patient created successfully.';
        this.isSaving = false;
        setTimeout(() => {
          this.router.navigate(['/app/patients']);
          this.cdr.markForCheck();
        }, 800);
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.errorMessage =
          err?.error?.message ||
          (this.isEditMode
            ? 'Failed to update patient. Please try again.'
            : 'Failed to create patient. Please try again.');
        this.isSaving = false;
        this.cdr.markForCheck();
      },
    });
  }

  cancel(): void {
    this.router.navigate(['/app/patients']);
  }
}
