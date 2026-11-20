import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService } from '../../../../core/services/api';
import { ToastService } from '../../../../core/services/toast';
import { ChartPatient } from '../../../../core/components/patient-summary/patient-summary';
import { ConfirmService } from '../../../../core/services/confirm';
import { SoapAttachment, SoapDiagnosis, SoapNote } from '../patient-detail/patient-detail';

/** A file chosen but not yet sent, with an object URL for its preview. */
interface StagedFile {
  file: File;
  url: string;
  isImage: boolean;
}

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
export class SoapNotePageComponent implements OnInit, OnDestroy {
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

  // --- Files filed with the note -----------------------------------------

  /** What is already on the note, from the server. */
  attachments: SoapAttachment[] = [];

  /**
   * Files picked but not yet sent. They go up when the note is saved, not
   * when they are chosen: a new note has no id to hang them on until then,
   * and abandoning a half-written note should not leave files behind.
   */
  staged: StagedFile[] = [];

  /** How far through a multi-file upload we are, for the button label. */
  uploadProgress = '';

  /** What the file picker will take. Kept in step with the API. */
  /**
   * What the picker takes. A scanned chart from another clinic is the usual
   * case, so the cap is roomier than a single result page needs.
   */
  readonly acceptedTypes = 'application/pdf,image/png,image/jpeg,image/webp';
  readonly maxFileMb = 25;
  private readonly maxBytes = this.maxFileMb * 1024 * 1024;

  /**
   * Object URLs for the files already on the note, by attachment id.
   *
   * The file endpoint needs the bearer token, which a plain <img src> or a
   * link cannot send, so each one is fetched through the API client and held
   * as a blob URL the page can point at.
   */
  private previews = new Map<number, string>();

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
    private confirm: ConfirmService,
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

        this.attachments = note.attachments ?? [];
        this.loadPreviews();

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
  // Files filed with the note
  // ----------------------------------------------------------

  onFilesChosen(event: Event): void {
    const input = event.target as HTMLInputElement;
    const chosen = Array.from(input.files ?? []);

    for (const file of chosen) {
      if (file.size > this.maxBytes) {
        this.errorMessage = `${file.name} is larger than the ${this.maxFileMb} MB limit.`;
        continue;
      }

      const isImage = file.type.startsWith("image/");
      // Only an image gets a thumbnail; a PDF shows as a row with its name.
      this.staged.push({
        file,
        url: isImage ? URL.createObjectURL(file) : '',
        isImage,
      });
    }

    // Let the same file be picked again after being removed.
    input.value = '';
    this.cdr.markForCheck();
  }

  removeStaged(index: number): void {
    const [gone] = this.staged.splice(index, 1);
    if (gone?.url) URL.revokeObjectURL(gone.url);
  }

  private clearStaged(): void {
    for (const item of this.staged) {
      if (item.url) URL.revokeObjectURL(item.url);
    }
    this.staged = [];
  }

  /** Fetches each file as a blob, so the page can show and open it. */
  private loadPreviews(): void {
    for (const attachment of this.attachments) {
      if (this.previews.has(attachment.id)) continue;

      this.api
        .getBlob(
          `patients/${this.patientId}/soap-notes/${this.noteId}/attachments/${attachment.id}`)
        .subscribe({
          next: blob => {
            this.previews.set(attachment.id, URL.createObjectURL(blob));
            this.cdr.markForCheck();
          },
          // A file that will not come back simply has no preview; the row
          // still names it, and the note is not held up by it.
          error: () => {},
        });
    }
  }

  attachmentUrl(attachment: SoapAttachment): string {
    return this.previews.get(attachment.id) ?? '';
  }

  isImage(contentType: string): boolean {
    return (contentType ?? "").startsWith("image/");
  }

  /** "243 KB" / "1.4 MB" — a size somebody can judge at a glance. */
  fileSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  /**
   * Removing a file that is already on the note happens at once, rather than
   * waiting for the note to be saved: it is a deliberate, irreversible act of
   * its own, and Cancel should not be able to quietly bring it back.
   */
  async removeAttachment(attachment: SoapAttachment): Promise<void> {
    const confirmed = await this.confirm.ask({
      title: 'Remove file',
      message: `Remove ${attachment.fileName} from this note?`,
      detail: 'This cannot be undone.',
      confirmLabel: 'Remove',
    });
    if (!confirmed) return;

    this.api
      .delete<void>(
        `patients/${this.patientId}/soap-notes/${this.noteId}/attachments/${attachment.id}`)
      .subscribe({
        next: () => {
          this.attachments = this.attachments.filter(a => a.id !== attachment.id);
          this.cdr.markForCheck();
        },
        error: () => {
          this.toast.error('That file could not be removed. Please try again.');
          this.cdr.markForCheck();
        },
      });
  }

  /**
   * Sends the staged files one at a time, so a rejected one can say which it
   * was instead of failing the batch without a name.
   */
  private uploadStaged(noteId: number, done: () => void): void {
    const items = [...this.staged];

    const next = (i: number): void => {
      if (i >= items.length) {
        this.clearStaged();
        this.uploadProgress = '';
        done();
        return;
      }

      this.uploadProgress = items.length > 1 ? `file ${i + 1} of ${items.length}` : '';
      this.cdr.markForCheck();

      // FormData goes through untouched — HttpClient sets the multipart
      // boundary itself, which it cannot do if a Content-Type is set for it.
      const form = new FormData();
      form.append('file', items[i].file);

      this.api
        .post<SoapAttachment>(
          `patients/${this.patientId}/soap-notes/${noteId}/attachments`, form)
        .subscribe({
          next: () => next(i + 1),
          error: err => {
            // The note itself is saved by this point, so the half that got
            // through stays and the rest are left staged to try again.
            this.isSaving = false;
            this.uploadProgress = '';
            this.staged = items.slice(i);
            this.errorMessage =
              (err?.error?.message ?? 'That file could not be attached.') +
              ` (${items[i].file.name})`;
            this.noteId = noteId;
            this.cdr.markForCheck();
          },
        });
    };

    next(0);
  }

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

    if (!hasContent && this.staged.length === 0 && this.attachments.length === 0) {
      this.errorMessage = 'Write at least one section, or attach a file, before saving.';
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

    const wasEditing = this.noteId !== null;

    request$.subscribe({
      next: saved => {
        const finish = () => {
          this.isSaving = false;
          this.toast.success(wasEditing ? 'SOAP note updated.' : 'SOAP note saved.');
          this.back();
        };

        // Files can only be hung on a note that exists, so they go after it.
        if (this.staged.length > 0) {
          this.uploadStaged(saved.id, finish);
        } else {
          finish();
        }
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

  ngOnDestroy(): void {
    this.clearStaged();
    for (const url of this.previews.values()) URL.revokeObjectURL(url);
    this.previews.clear();
  }

  /** Back to the chart this note belongs to. */
  back(): void {
    this.router.navigate(['/app/patients', this.patientId]);
  }
}
