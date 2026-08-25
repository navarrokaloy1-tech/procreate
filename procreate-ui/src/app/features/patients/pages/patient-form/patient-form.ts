import {
  ChangeDetectorRef,
  Component,
  EventEmitter,
  Input,
  OnChanges,
  OnDestroy,
  OnInit,
  Output,
  SimpleChanges,
} from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Subscription, distinctUntilChanged } from 'rxjs';
import { ApiService } from '../../../../core/services/api';
import { ComboOption } from '../../../../core/components/combo-select/combo-select';
import { Patient } from '../patient-list/patient-list';

interface CountryOption { code: string; name: string; }
interface RegionOption { code: string; name: string; }
interface ProvinceOption { name: string; }
interface CityOption { name: string; }

/**
 * Patient registration / edit sheet. Rendered as a modal by the patient list
 * rather than a routed page, so the list stays visible behind it.
 */
@Component({
  selector: 'app-patient-form',
  standalone: false,
  templateUrl: './patient-form.html',
  styleUrl: './patient-form.scss',
})
export class PatientFormComponent implements OnInit, OnChanges, OnDestroy {
  /** Controls visibility. The host owns this so it can animate/stack modals. */
  @Input() open = false;
  /** Null opens a blank registration sheet; an id loads that patient for editing. */
  @Input() patientId: number | null = null;

  @Output() saved = new EventEmitter<Patient>();
  @Output() cancelled = new EventEmitter<void>();

  isLoading = false;
  isSaving = false;
  errorMessage = '';

  patientForm: FormGroup;

  genderOptions = ['Male', 'Female', 'Other'];
  civilStatusOptions = ['Single', 'Married', 'Widowed', 'Separated', 'Divorced'];
  bloodTypeOptions = ['A+', 'A-', 'B+', 'B-', 'AB+', 'AB-', 'O+', 'O-', 'Unknown'];

  // Cascading address reference data.
  countries: CountryOption[] = [];
  regions: RegionOption[] = [];
  provinces: ProvinceOption[] = [];
  cities: CityOption[] = [];
  isLoadingRegions = false;
  isLoadingProvinces = false;
  isLoadingCities = false;

  /**
   * Suppresses the cascade while the form is being populated programmatically,
   * so loading a patient does not immediately clear their stored region/city.
   */
  private isPopulating = false;
  private subs = new Subscription();

  /**
   * Tabbed sections. `controls` lets a tab flag itself when one of its own
   * fields is invalid, and lets submit jump to the first offending tab.
   */
  readonly tabs = [
    {
      id: 'personal',
      label: 'Personal Info',
      icon: 'user',
      controls: [
        'firstName', 'middleName', 'lastName', 'suffix',
        'dateOfBirth', 'gender', 'civilStatus', 'occupation', 'nationality',
      ],
    },
    {
      id: 'contact',
      label: 'Contact & Address',
      icon: 'phone',
      controls: [
        'contactNumber', 'landline', 'email',
        'address', 'country', 'region', 'province', 'city', 'zipCode',
      ],
    },
    {
      id: 'medical',
      label: 'Medical & Insurance',
      icon: 'heart',
      controls: [
        'bloodType', 'philHealthNumber', 'seniorCitizenId',
        'pwdId', 'hmoProvider', 'hmoAccountNumber',
      ],
    },
    {
      id: 'emergency',
      label: 'Emergency Contact',
      icon: 'alert-triangle',
      controls: [
        'emergencyContactName', 'emergencyContactRelationship',
        'emergencyContactNumber', 'emergencyContactNotes',
      ],
    },
  ];

  activeTab = 'personal';

  /**
   * Pristine values for every control. Used both to build the form and to
   * re-arm it on open — sending null for one of these would be rejected by
   * the API, whose string properties are non-nullable.
   */
  private static readonly BLANK = {
    firstName: '',
    middleName: '',
    lastName: '',
    suffix: '',
    dateOfBirth: '',
    gender: '',
    civilStatus: '',
    occupation: '',
    nationality: 'Filipino',
    contactNumber: '',
    landline: '',
    email: '',
    address: '',
    country: 'Philippines',
    region: '',
    province: '',
    city: '',
    zipCode: '',
    bloodType: '',
    philHealthNumber: '',
    seniorCitizenId: '',
    pwdId: '',
    hmoProvider: '',
    hmoAccountNumber: '',
    emergencyContactName: '',
    emergencyContactRelationship: '',
    emergencyContactNumber: '',
    emergencyContactNotes: '',
  };

  constructor(
    private fb: FormBuilder,
    private apiService: ApiService,
    private cdr: ChangeDetectorRef
  ) {
    this.patientForm = this.buildForm();
  }

  ngOnInit(): void {
    // The pickers write straight to the form, so the cascade reacts to value
    // changes rather than to (change) handlers on the controls.
    this.subs.add(
      this.patientForm.get('country')!.valueChanges
        .pipe(distinctUntilChanged())
        .subscribe((value) => this.handleCountryChange(value ?? ''))
    );

    this.subs.add(
      this.patientForm.get('region')!.valueChanges
        .pipe(distinctUntilChanged())
        .subscribe((value) => this.handleRegionChange(value ?? ''))
    );

    this.subs.add(
      this.patientForm.get('province')!.valueChanges
        .pipe(distinctUntilChanged())
        .subscribe((value) => this.handleProvinceChange(value ?? ''))
    );
  }

  ngOnChanges(changes: SimpleChanges): void {
    // Re-arm each time the host opens the sheet, so a previous edit never
    // leaks into the next registration.
    if (changes['open'] && this.open) {
      this.activeTab = 'personal';
      this.errorMessage = '';

      this.isPopulating = true;
      this.patientForm.reset(PatientFormComponent.BLANK);
      this.isPopulating = false;

      this.loadCountries();

      if (this.patientId !== null) {
        this.loadPatient(this.patientId);
      } else {
        this.provinces = [];
        this.cities = [];
        this.loadRegions(PatientFormComponent.BLANK.country);
      }
    }
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  private buildForm(): FormGroup {
    const blank = PatientFormComponent.BLANK;

    return this.fb.group({
      firstName: [blank.firstName, [Validators.required]],
      middleName: [blank.middleName],
      lastName: [blank.lastName, [Validators.required]],
      suffix: [blank.suffix],
      dateOfBirth: [blank.dateOfBirth, [Validators.required]],
      gender: [blank.gender, [Validators.required]],
      civilStatus: [blank.civilStatus],
      occupation: [blank.occupation],
      nationality: [blank.nationality],
      contactNumber: [blank.contactNumber, [Validators.required]],
      landline: [blank.landline],
      email: [blank.email, [Validators.email]],
      address: [blank.address],
      country: [blank.country],
      region: [blank.region],
      province: [blank.province],
      city: [blank.city],
      zipCode: [blank.zipCode],
      bloodType: [blank.bloodType],
      philHealthNumber: [blank.philHealthNumber],
      seniorCitizenId: [blank.seniorCitizenId],
      pwdId: [blank.pwdId],
      hmoProvider: [blank.hmoProvider],
      hmoAccountNumber: [blank.hmoAccountNumber],
      emergencyContactName: [blank.emergencyContactName],
      emergencyContactRelationship: [blank.emergencyContactRelationship],
      emergencyContactNumber: [blank.emergencyContactNumber],
      emergencyContactNotes: [blank.emergencyContactNotes],
    });
  }

  get f() {
    return this.patientForm.controls;
  }

  get isEditMode(): boolean {
    return this.patientId !== null;
  }

  selectTab(id: string): void {
    this.activeTab = id;
  }

  /** True when a tab holds a field that has failed validation and been touched. */
  tabHasError(tab: { controls: string[] }): boolean {
    return tab.controls.some((name) => {
      const control = this.patientForm.get(name);
      return !!control && control.invalid && control.touched;
    });
  }

  private revealFirstInvalidTab(): void {
    const target = this.tabs.find((tab) =>
      tab.controls.some((name) => this.patientForm.get(name)?.invalid)
    );
    if (target) this.activeTab = target.id;
  }

  // ----------------------------------------------------------
  // Address pickers
  // ----------------------------------------------------------

  get countryOptions(): ComboOption[] {
    return this.countries.map((c) => ({ value: c.name, label: c.name }));
  }

  get regionOptions(): ComboOption[] {
    return this.regions.map((r) => ({ value: r.name, label: r.name }));
  }

  get provinceOptions(): ComboOption[] {
    return this.provinces.map((p) => ({ value: p.name, label: p.name }));
  }

  get cityOptions(): ComboOption[] {
    return this.cities.map((c) => ({ value: c.name, label: c.name }));
  }

  get hasCountry(): boolean {
    return !!this.patientForm.get('country')?.value;
  }

  get selectedRegion(): string {
    return this.patientForm.get('region')?.value ?? '';
  }

  /**
   * Countries we hold no data for get free-text region/province boxes rather
   * than dead empty dropdowns.
   */
  get regionIsFreeText(): boolean {
    return this.hasCountry && !this.isLoadingRegions && this.regions.length === 0;
  }

  get provinceIsFreeText(): boolean {
    return this.regionIsFreeText;
  }

  private handleCountryChange(country: string): void {
    if (this.isPopulating) return;

    // Clear every dependent level: a Philippine region is meaningless once the
    // country becomes Japan.
    this.setQuietly({ region: '', province: '', city: '' });
    this.provinces = [];
    this.cities = [];
    this.loadRegions(country);
  }

  private handleRegionChange(region: string): void {
    if (this.isPopulating) return;

    this.setQuietly({ province: '', city: '' });
    this.cities = [];
    this.loadProvinces(this.patientForm.get('country')?.value ?? '', region);
  }

  private handleProvinceChange(province: string): void {
    if (this.isPopulating) return;

    this.setQuietly({ city: '' });
    this.loadCities(this.patientForm.get('country')?.value ?? '', province);
  }

  /** Clears dependent fields without re-entering the cascade. */
  private setQuietly(values: Record<string, string>): void {
    this.patientForm.patchValue(values, { emitEvent: false });
  }

  private loadCountries(): void {
    if (this.countries.length) return;

    this.apiService.get<CountryOption[]>('locations/countries').subscribe({
      next: (countries) => {
        this.countries = countries;
        this.cdr.markForCheck();
      },
      error: () => this.cdr.markForCheck(),
    });
  }

  private loadRegions(country: string): void {
    if (!country) {
      this.regions = [];
      return;
    }

    this.isLoadingRegions = true;

    this.apiService.get<RegionOption[]>('locations/regions', { country }).subscribe({
      next: (regions) => {
        this.regions = regions;
        this.isLoadingRegions = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.regions = [];
        this.isLoadingRegions = false;
        this.cdr.markForCheck();
      },
    });
  }

  private loadProvinces(country: string, region: string): void {
    if (!country || !region) {
      this.provinces = [];
      return;
    }

    this.isLoadingProvinces = true;

    this.apiService
      .get<ProvinceOption[]>('locations/provinces', { country, region })
      .subscribe({
        next: (provinces) => {
          this.provinces = provinces;
          this.isLoadingProvinces = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.provinces = [];
          this.isLoadingProvinces = false;
          this.cdr.markForCheck();
        },
      });
  }

  private loadCities(country: string, province: string): void {
    if (!country || !province) {
      this.cities = [];
      return;
    }

    this.isLoadingCities = true;

    this.apiService.get<CityOption[]>('locations/cities', { country, province }).subscribe({
      next: (cities) => {
        this.cities = cities;
        this.isLoadingCities = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.cities = [];
        this.isLoadingCities = false;
        this.cdr.markForCheck();
      },
    });
  }

  // ----------------------------------------------------------
  // Load / save
  // ----------------------------------------------------------

  loadPatient(id: number): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.apiService.get<Patient>('patients/' + id).subscribe({
      next: (patient) => {
        // Optional columns come back as null; coalesce so they are not sent
        // straight back as null on save.
        const patch: Record<string, unknown> = {};
        for (const key of Object.keys(PatientFormComponent.BLANK)) {
          patch[key] = (patient as unknown as Record<string, unknown>)[key] ?? '';
        }
        patch['dateOfBirth'] = patient.dateOfBirth
          ? patient.dateOfBirth.substring(0, 10)
          : '';

        this.isPopulating = true;
        this.patientForm.patchValue(patch);
        this.isPopulating = false;

        // Repopulate the cascade for the stored address, preserving the saved
        // region/province/city rather than clearing them.
        const country = String(patch['country'] ?? '');
        const region = String(patch['region'] ?? '');
        const province = String(patch['province'] ?? '');
        if (country) {
          this.loadRegions(country);
          if (region) this.loadProvinces(country, region);
          if (province) this.loadCities(country, province);
        }

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
      this.revealFirstInvalidTab();
      return;
    }

    this.isSaving = true;
    this.errorMessage = '';

    const payload = this.patientForm.value;

    const request$ = this.isEditMode
      ? this.apiService.put<Patient>('patients/' + this.patientId, payload)
      : this.apiService.post<Patient>('patients', payload);

    request$.subscribe({
      next: (patient) => {
        this.isSaving = false;
        this.saved.emit(patient);
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.errorMessage =
          err?.error?.message ||
          (this.isEditMode
            ? 'Failed to update patient. Please try again.'
            : 'Failed to register patient. Please try again.');
        this.isSaving = false;
        this.cdr.markForCheck();
      },
    });
  }

  cancel(): void {
    this.cancelled.emit();
  }
}
