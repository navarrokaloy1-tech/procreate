import {
  ChangeDetectorRef,
  Component,
  EventEmitter,
  Input,
  OnChanges,
  Output,
  SimpleChanges,
} from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ApiService } from '../../../../core/services/api';
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
export class PatientFormComponent implements OnChanges {
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

  constructor(
    private fb: FormBuilder,
    private apiService: ApiService,
    private cdr: ChangeDetectorRef
  ) {
    this.patientForm = this.buildForm();
  }

  ngOnChanges(changes: SimpleChanges): void {
    // Re-arm each time the host opens the sheet, so a previous edit never
    // leaks into the next registration.
    if (changes['open'] && this.open) {
      this.activeTab = 'personal';
      this.errorMessage = '';
      // Reset to blank strings, not null: the API's non-nullable string
      // properties reject null under nullable reference types.
      this.patientForm.reset(PatientFormComponent.BLANK);

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

  /**
   * Pristine values for every control. Used both to build the form and to
   * re-arm it on open — sending null for one of these would be rejected by
   * the API, whose string properties are non-nullable.
   */
  private static readonly BLANK = {
    // Personal
    firstName: '',
    middleName: '',
    lastName: '',
    suffix: '',
    dateOfBirth: '',
    gender: '',
    civilStatus: '',
    occupation: '',
    nationality: 'Filipino',
    // Contact
    contactNumber: '',
    landline: '',
    email: '',
    address: '',
    country: 'Philippines',
    region: '',
    province: '',
    city: '',
    zipCode: '',
    // Medical & insurance
    bloodType: '',
    philHealthNumber: '',
    seniorCitizenId: '',
    pwdId: '',
    hmoProvider: '',
    hmoAccountNumber: '',
    // Emergency
    emergencyContactName: '',
    emergencyContactRelationship: '',
    emergencyContactNumber: '',
    emergencyContactNotes: '',
  };

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

  // ----------------------------------------------------------
  // Cascading address dropdowns
  // ----------------------------------------------------------

  /**
   * When a country has no reference data we show a free-text box instead of an
   * empty dropdown, so addresses outside the Philippines are still enterable.
   */
  get hasCountry(): boolean {
    return !!this.patientForm.get('country')?.value;
  }

  get selectedRegion(): string {
    return this.patientForm.get('region')?.value ?? '';
  }

  get selectedProvince(): string {
    return this.patientForm.get('province')?.value ?? '';
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

  onCountryChange(country: string): void {
    // Clear every dependent level: a Philippine region is meaningless once the
    // country becomes Japan.
    this.patientForm.patchValue({ region: '', province: '', city: '' });
    this.provinces = [];
    this.cities = [];
    this.loadRegions(country);
  }

  onRegionChange(region: string): void {
    this.patientForm.patchValue({ province: '', city: '' });
    this.cities = [];
    this.loadProvinces(this.patientForm.get('country')?.value ?? '', region);
  }

  onProvinceChange(province: string): void {
    this.patientForm.patchValue({ city: '' });
    this.closeCityPanel();
    this.loadCities(this.patientForm.get('country')?.value ?? '', province);
  }

  // ----------------------------------------------------------
  // City combobox
  // ----------------------------------------------------------
  // A hand-rolled dropdown rather than <datalist>: the native popup is drawn
  // by the browser, ignores our styling, and follows the OS colour scheme, so
  // it looked nothing like the region/province selects. This keeps the field
  // typeable (municipalities missing from the list can still be entered) while
  // matching the rest of the form.

  cityPanelOpen = false;
  cityHighlight = -1;

  /** Suggestions filtered by what has been typed so far. */
  get filteredCities(): CityOption[] {
    const term = (this.patientForm.get('city')?.value ?? '').trim().toLowerCase();
    if (!term) return this.cities;

    return this.cities.filter((c) => c.name.toLowerCase().includes(term));
  }

  openCityPanel(): void {
    if (!this.cities.length) return;
    this.cityPanelOpen = true;
    this.cityHighlight = -1;
  }

  closeCityPanel(): void {
    this.cityPanelOpen = false;
    this.cityHighlight = -1;
  }

  toggleCityPanel(): void {
    if (this.cityPanelOpen) this.closeCityPanel();
    else this.openCityPanel();
  }

  onCityInput(): void {
    // Reopen as the user narrows the list, but never fight an empty result set.
    this.cityPanelOpen = this.filteredCities.length > 0;
    this.cityHighlight = -1;
  }

  pickCity(name: string): void {
    this.patientForm.patchValue({ city: name });
    this.closeCityPanel();
  }

  onCityKeydown(event: KeyboardEvent): void {
    const options = this.filteredCities;

    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        if (!this.cityPanelOpen) this.openCityPanel();
        else if (options.length) this.cityHighlight = (this.cityHighlight + 1) % options.length;
        break;

      case 'ArrowUp':
        event.preventDefault();
        if (options.length) {
          this.cityHighlight =
            this.cityHighlight <= 0 ? options.length - 1 : this.cityHighlight - 1;
        }
        break;

      case 'Enter':
        // Only intercept when actively choosing, so Enter still submits.
        if (this.cityPanelOpen && this.cityHighlight >= 0 && options[this.cityHighlight]) {
          event.preventDefault();
          this.pickCity(options[this.cityHighlight].name);
        }
        break;

      case 'Escape':
        if (this.cityPanelOpen) {
          event.stopPropagation();
          this.closeCityPanel();
        }
        break;
    }
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

        this.patientForm.patchValue(patch);

        // Repopulate the cascade for the stored address, preserving the saved
        // region/city rather than clearing them.
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
