import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Subscription, distinctUntilChanged } from 'rxjs';
import * as QRCode from 'qrcode';
import { ApiService } from '../../../../core/services/api';
import { ComboOption } from '../../../../core/components/combo-select/combo-select';

interface RegisteredPatient {
  id: number;
  patientCode: string;
  firstName: string;
  lastName: string;
  middleName: string;
  dateOfBirth?: string;
  gender?: string;
  contactNumber?: string;
}

interface CountryOption { code: string; name: string; }
interface RegionOption { code: string; name: string; }
interface ProvinceOption { name: string; }
interface CityOption { name: string; }

/**
 * Public self-registration kiosk. Uses the same four-tab shape as the staff
 * Add Patient sheet so both collect the same record, and issues a card with a
 * scannable code on success.
 */
@Component({
  selector: 'app-patient-signup',
  standalone: false,
  templateUrl: './patient-signup.html',
  styleUrl: './patient-signup.scss',
})
export class PatientSignup implements OnInit, OnDestroy {
  signupForm: FormGroup;
  isSubmitting = false;
  errorMessage = '';

  registered: RegisteredPatient | null = null;
  registeredAt: Date | null = null;
  /** Data URL of the generated code, rendered on the card. */
  qrDataUrl = '';

  genderOptions = ['Male', 'Female', 'Other'];
  civilStatusOptions = ['Single', 'Married', 'Widowed', 'Separated', 'Divorced'];
  bloodTypeOptions = ['A+', 'A-', 'B+', 'B-', 'AB+', 'AB-', 'O+', 'O-', 'Unknown'];

  countries: CountryOption[] = [];
  regions: RegionOption[] = [];
  provinces: ProvinceOption[] = [];
  cities: CityOption[] = [];
  isLoadingRegions = false;
  isLoadingProvinces = false;
  isLoadingCities = false;

  private isPopulating = false;
  private subs = new Subscription();

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

  /** Blank strings rather than null: the API's string columns reject null. */
  private static readonly BLANK = {
    firstName: '', middleName: '', lastName: '', suffix: '',
    dateOfBirth: '', gender: '', civilStatus: '', occupation: '',
    nationality: 'Filipino',
    contactNumber: '', landline: '', email: '', address: '',
    country: 'Philippines', region: '', province: '', city: '', zipCode: '',
    bloodType: '', philHealthNumber: '', seniorCitizenId: '', pwdId: '',
    hmoProvider: '', hmoAccountNumber: '',
    emergencyContactName: '', emergencyContactRelationship: '',
    emergencyContactNumber: '', emergencyContactNotes: '',
  };

  constructor(
    private fb: FormBuilder,
    private apiService: ApiService,
    private cdr: ChangeDetectorRef
  ) {
    const blank = PatientSignup.BLANK;

    this.signupForm = this.fb.group({
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

  ngOnInit(): void {
    this.subs.add(
      this.signupForm.get('country')!.valueChanges
        .pipe(distinctUntilChanged())
        .subscribe((v) => this.handleCountryChange(v ?? ''))
    );
    this.subs.add(
      this.signupForm.get('region')!.valueChanges
        .pipe(distinctUntilChanged())
        .subscribe((v) => this.handleRegionChange(v ?? ''))
    );
    this.subs.add(
      this.signupForm.get('province')!.valueChanges
        .pipe(distinctUntilChanged())
        .subscribe((v) => this.handleProvinceChange(v ?? ''))
    );

    this.loadCountries();
    this.loadRegions(PatientSignup.BLANK.country);
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  get f() {
    return this.signupForm.controls;
  }

  selectTab(id: string): void {
    this.activeTab = id;
  }

  tabHasError(tab: { controls: string[] }): boolean {
    return tab.controls.some((name) => {
      const control = this.signupForm.get(name);
      return !!control && control.invalid && control.touched;
    });
  }

  private revealFirstInvalidTab(): void {
    const target = this.tabs.find((tab) =>
      tab.controls.some((name) => this.signupForm.get(name)?.invalid)
    );
    if (target) this.activeTab = target.id;
  }

  // ----------------------------------------------------------
  // Address cascade
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
    return !!this.signupForm.get('country')?.value;
  }

  get selectedRegion(): string {
    return this.signupForm.get('region')?.value ?? '';
  }

  get regionIsFreeText(): boolean {
    return this.hasCountry && !this.isLoadingRegions && this.regions.length === 0;
  }

  get provinceIsFreeText(): boolean {
    return this.regionIsFreeText;
  }

  private handleCountryChange(country: string): void {
    if (this.isPopulating) return;
    this.setQuietly({ region: '', province: '', city: '' });
    this.provinces = [];
    this.cities = [];
    this.loadRegions(country);
  }

  private handleRegionChange(region: string): void {
    if (this.isPopulating) return;
    this.setQuietly({ province: '', city: '' });
    this.cities = [];
    this.loadProvinces(this.signupForm.get('country')?.value ?? '', region);
  }

  private handleProvinceChange(province: string): void {
    if (this.isPopulating) return;
    this.setQuietly({ city: '' });
    this.loadCities(this.signupForm.get('country')?.value ?? '', province);
  }

  private setQuietly(values: Record<string, string>): void {
    this.signupForm.patchValue(values, { emitEvent: false });
  }

  private loadCountries(): void {
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
    this.apiService.get<ProvinceOption[]>('locations/provinces', { country, region }).subscribe({
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
  // Card
  // ----------------------------------------------------------

  /** The numeric portion of the patient code, used as the big queue number. */
  get queueNumber(): string {
    if (!this.registered) return '';
    const parts = this.registered.patientCode.split('-');
    return parts.length ? parts[parts.length - 1] : this.registered.patientCode;
  }

  get fullName(): string {
    if (!this.registered) return '';
    return [this.registered.firstName, this.registered.middleName, this.registered.lastName]
      .filter((part) => part && part.trim())
      .join(' ');
  }

  /**
   * Encodes the patient identifier, not their details.
   *
   * A card can be photographed by anyone who sees it, so putting a name, birth
   * date or contact number in the payload would hand medical-adjacent data to
   * whoever scans it. The identifier is enough: every scanner in the app
   * resolves it against the API, where access is controlled. Human-readable
   * details are printed as text beside the code instead.
   */
  private qrPayload(patientCode: string): string {
    return 'patient:' + patientCode;
  }

  private async renderQr(patientCode: string): Promise<void> {
    try {
      this.qrDataUrl = await QRCode.toDataURL(this.qrPayload(patientCode), {
        errorCorrectionLevel: 'M',
        margin: 1,
        width: 320,
        color: { dark: '#2D1B0E', light: '#FFFFFF' },
      });
    } catch {
      // Registration itself succeeded; the code is a convenience, so a failure
      // here must not present as a failed registration.
      this.qrDataUrl = '';
    }
    this.cdr.markForCheck();
  }

  submit(): void {
    if (this.signupForm.invalid) {
      this.signupForm.markAllAsTouched();
      this.revealFirstInvalidTab();
      this.errorMessage = 'Please complete the required fields.';
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
        void this.renderQr(patient.patientCode);
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
    this.qrDataUrl = '';
    this.errorMessage = '';
    this.activeTab = 'personal';

    this.isPopulating = true;
    this.signupForm.reset(PatientSignup.BLANK);
    this.isPopulating = false;

    this.provinces = [];
    this.cities = [];
    this.loadRegions(PatientSignup.BLANK.country);
    this.cdr.markForCheck();
  }

  printCard(): void {
    document.body.classList.add('printing-slip');

    const cleanup = () => {
      document.body.classList.remove('printing-slip');
      window.removeEventListener('afterprint', cleanup);
    };

    window.addEventListener('afterprint', cleanup);
    window.print();
    setTimeout(cleanup, 1000);
  }
}
