import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService } from '../../../../core/services/api';
import { ToastService } from '../../../../core/services/toast';
import { ChartPatient } from '../../../../core/components/patient-summary/patient-summary';
import { SoapDiagnosis, SoapNote } from '../patient-detail/patient-detail';

/**
 * Writing one SOAP note, on a page of its own.
 *
 * This used to be a modal on the patient chart. A consultation note is the
 * longest thing anybody types into this system — five sections and a table of
 * coded diagnoses — and a sheet floating over the chart gave it a scrolling
 * window a few hundred pixels tall. It now gets the whole page.
 *
 * There is one editor, not two. The Simple/Advanced tabs are gone and every
 * note is written in full: a note missing its Objective and its ICD-10 codes
 * because somebody left the tab on Simple is a note that has to be written
 * again. Existing Simple notes open here unchanged and are saved as full ones,
 * with whatever was never filled in still empty.
 */
@Component({
  selector: 'app-soap-note',
  standalone: false,
  templateUrl: './soap-note.html',
  styleUrl: './soap-note.scss',
})
export class SoapNotePageComponent implements OnInit {
  patientId = 0;
  /** The note being edited, or null when this is a new one. */
  noteId: number | null = null;

  /**
   * The whole patient, not just their name: the column down the left of this
   * page is the same one the chart shows, so writing a note never means going
   * back to look something up.
   */
  patient: ChartPatient | null = null;

  isLoading = false;
  isSaving = false;
  errorMessage = '';

  draft = {
    noteDate: new Date().toISOString().slice(0, 10),
    chiefComplaint: '',
    historyOfPresentIllness: '',
    objectiveRemarks: '',
    assessment: '',
    plan: '',
    diagnoses: [] as SoapDiagnosis[],
  };

  constructor(
    private api: ApiService,
    private route: ActivatedRoute,
    private router: Router,
    private cdr: ChangeDetectorRef,
    private toast: ToastService,
  ) {}

  ngOnInit(): void {
    this.patientId = Number(this.route.snapshot.paramMap.get('id'));

    const noteId = this.route.snapshot.paramMap.get('noteId');
    this.noteId = noteId ? Number(noteId) : null;

    this.loadPatient();
    if (this.noteId) this.loadNote();
  }

  get title(): string {
    return this.noteId ? 'Edit SOAP Note' : 'New SOAP Note';
  }

  // ----------------------------------------------------------
  // Loading
  // ----------------------------------------------------------

  /**
   * The chart, for the hero and the column beside the form. Read-only here —
   * the note is saved against the id from the route, so a slow or failed chart
   * never stops somebody writing.
   */
  private loadPatient(): void {
    this.api
      .get<{ patient: ChartPatient }>(`patients/${this.patientId}/chart`)
      .subscribe({
        next: chart => {
          this.patient = chart.patient;
          this.cdr.markForCheck();
        },
        error: () => {
          this.patient = null;
          this.cdr.markForCheck();
        },
      });
  }

  /**
   * The note being edited. There is no endpoint for a single note, so this
   * takes the patient's list and picks it out — a chart holds few enough notes
   * that one round trip is cheaper than an endpoint nothing else would use.
   */
  private loadNote(): void {
    this.isLoading = true;

    this.api.get<{ data: SoapNote[] }>(`patients/${this.patientId}/soap-notes`).subscribe({
      next: response => {
        const note = (response.data ?? []).find(n => n.id === this.noteId);

        if (!note) {
          this.errorMessage = 'That note could not be found.';
          this.isLoading = false;
          this.cdr.markForCheck();
          return;
        }

        this.draft = {
          noteDate: (note.noteDate ?? '').slice(0, 10) || new Date().toISOString().slice(0, 10),
          chiefComplaint: note.chiefComplaint ?? '',
          historyOfPresentIllness: note.historyOfPresentIllness ?? '',
          objectiveRemarks: note.objectiveRemarks ?? '',
          assessment: note.assessment ?? '',
          plan: note.plan ?? '',
          diagnoses: (note.diagnoses ?? []).map(d => ({ ...d })),
        };

        this.isLoading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.errorMessage = 'That note could not be loaded.';
        this.isLoading = false;
        this.cdr.markForCheck();
      },
    });
  }

  // ----------------------------------------------------------
  // Coded diagnoses
  // ----------------------------------------------------------

  addDiagnosis(): void {
    this.draft.diagnoses.push({ description: '', icd10: '' });
  }

  removeDiagnosis(index: number): void {
    this.draft.diagnoses.splice(index, 1);
  }

  trackDiagnosis = (index: number) => index;

  // ----------------------------------------------------------
  // Saving
  // ----------------------------------------------------------

  save(): void {
    const draft = this.draft;

    const diagnoses = draft.diagnoses
      .map(d => ({ description: d.description.trim(), icd10: d.icd10.trim() }))
      .filter(d => d.description || d.icd10);

    // A note needs at least one section filled; an empty note is noise on the chart.
    const hasContent =
      draft.chiefComplaint.trim() ||
      draft.historyOfPresentIllness.trim() ||
      draft.objectiveRemarks.trim() ||
      draft.assessment.trim() ||
      draft.plan.trim() ||
      diagnoses.length > 0;

    if (!hasContent) {
      this.errorMessage = 'Write at least one section before saving.';
      return;
    }

    const body = {
      // Every note is written in full now, so the mode the API stores is always
      // the same. The field is still sent because older notes carry 'Simple'
      // and re-saving one here brings it over.
      mode: 'Advanced',
      noteDate: draft.noteDate,
      chiefComplaint: draft.chiefComplaint.trim(),
      historyOfPresentIllness: draft.historyOfPresentIllness.trim(),
      objectiveRemarks: draft.objectiveRemarks.trim(),
      assessment: draft.assessment.trim(),
      plan: draft.plan.trim(),
      diagnoses,
    };

    this.isSaving = true;
    this.errorMessage = '';

    const request$ = this.noteId
      ? this.api.put<SoapNote>(`patients/${this.patientId}/soap-notes/${this.noteId}`, body)
      : this.api.post<SoapNote>(`patients/${this.patientId}/soap-notes`, body);

    request$.subscribe({
      next: () => {
        this.isSaving = false;
        this.toast.success(this.noteId ? 'SOAP note updated.' : 'SOAP note saved.');
        this.back();
      },
      error: err => {
        this.isSaving = false;
        this.errorMessage = err?.error?.message ?? 'Saving failed. Please try again.';
        this.cdr.markForCheck();
      },
    });
  }

  /** The two letters the hero avatar shows, as on the chart. */
  initials(name: string): string {
    return (name ?? '')
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map(part => part[0])
      .join('')
      .toUpperCase();
  }

  /** Back to the chart this note belongs to. */
  back(): void {
    this.router.navigate(['/app/patients', this.patientId]);
  }
}
