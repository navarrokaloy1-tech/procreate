import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IconComponent } from '../icon/icon';

/**
 * A patient as their chart reports them.
 *
 * Declared here rather than on the chart page because two screens now render
 * it — the chart itself and the SOAP note editor — and whoever adds a field
 * should only have to add it once.
 */
export interface ChartPatient {
  id: number;
  patientCode: string;
  fullName: string;
  firstName: string;
  middleName?: string;
  lastName: string;
  suffix?: string;
  dateOfBirth: string;
  age: number;
  gender: string;
  civilStatus?: string;
  occupation?: string;
  nationality?: string;
  bloodType?: string;
  contactNumber?: string;
  landline?: string;
  email?: string;
  address?: string;
  emergencyContactName?: string;
  emergencyContactRelationship?: string;
  emergencyContactNumber?: string;
  philHealthNumber?: string;
  seniorCitizenId?: string;
  pwdId?: string;
  hmoProvider?: string;
  hmoAccountNumber?: string;
  createdAt: string;
}

/**
 * Who the patient is: the stack of identity cards that runs down the left of
 * the chart, and now down the left of the SOAP note editor too.
 *
 * It is one component rather than the same hundred-odd lines of markup in two
 * templates, which would have drifted the first time a field was added. Purely
 * presentational — it fetches nothing and changes nothing.
 */
@Component({
  selector: 'app-patient-summary',
  standalone: true,
  imports: [CommonModule, IconComponent],
  templateUrl: './patient-summary.html',
})
export class PatientSummaryComponent {
  @Input({ required: true }) patient!: ChartPatient;

  /**
   * Emergency contact is the one card that is not always worth a heading, so
   * the caller says whether to show it. The chart does; the note editor does
   * not, where the column is a reference rather than the whole record.
   */
  @Input() showEmergencyContact = true;

  /** Whether the patient has any statutory discount or insurance on file. */
  get hasInsurance(): boolean {
    const p = this.patient;
    if (!p) return false;
    return !!(p.philHealthNumber || p.seniorCitizenId || p.pwdId || p.hmoProvider);
  }
}
