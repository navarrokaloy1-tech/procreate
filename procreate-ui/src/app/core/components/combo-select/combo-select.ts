import { CommonModule } from '@angular/common';
import {
  ChangeDetectorRef,
  Component,
  ElementRef,
  HostListener,
  Input,
  ViewChild,
  forwardRef,
} from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { IconComponent } from '../icon/icon';

export interface ComboOption {
  value: string;
  label: string;
}

/**
 * A dropdown the app renders itself, used instead of a native `<select>`.
 *
 * Two reasons it exists:
 *  - A native select's popup is drawn by the browser. It ignores page styling
 *    and follows the OS colour scheme, so it never matches the rest of the
 *    form. This one uses the app's own menu treatment.
 *  - A native select repaints its selected option lazily, so text that changes
 *    while the control is idle (e.g. "Loading..." becoming "Select province")
 *    can linger until something forces a reflow.
 *
 * Set `allowFreeText` for fields where the option list is only a suggestion.
 */
@Component({
  selector: 'app-combo-select',
  standalone: true,
  imports: [CommonModule, IconComponent],
  templateUrl: './combo-select.html',
  styleUrls: ['./combo-select.scss'],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => ComboSelectComponent),
      multi: true,
    },
  ],
})
export class ComboSelectComponent implements ControlValueAccessor {
  @Input() options: ComboOption[] = [];
  @Input() placeholder = 'Select...';
  /** Shown in place of the placeholder while an async list is in flight. */
  @Input() loading = false;
  @Input() loadingText = 'Loading...';
  /** Shown instead of the placeholder when the control is disabled. */
  @Input() disabledText = '';
  /**
   * Locks the control without disabling the form control. Disabling via the
   * form would drop the field from patientForm.value, so a dependent field
   * (region before a country is chosen) must be locked this way instead.
   */
  @Input() locked = false;
  /** When true the field accepts values that are not in the option list. */
  @Input() allowFreeText = false;
  @Input() inputId = '';

  @ViewChild('field') fieldRef?: ElementRef<HTMLInputElement>;

  value = '';
  isOpen = false;
  highlight = -1;
  isDisabled = false;

  /** Free text typed since the panel opened, used to filter the list. */
  private query = '';

  private onChange: (value: string) => void = () => {};
  private onTouched: () => void = () => {};

  constructor(private host: ElementRef<HTMLElement>, private cdr: ChangeDetectorRef) {}

  // ---------------- ControlValueAccessor ----------------

  writeValue(value: string | null): void {
    this.value = value ?? '';
    this.query = '';
    this.cdr.markForCheck();
  }

  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.isDisabled = isDisabled;
    if (isDisabled) this.close();
    this.cdr.markForCheck();
  }

  // ---------------- Display ----------------

  /** What the closed control shows: the chosen label, or free text as typed. */
  get displayValue(): string {
    if (this.allowFreeText) return this.value;

    const match = this.options.find((o) => o.value === this.value);
    return match?.label ?? this.value;
  }

  get placeholderText(): string {
    if (this.loading) return this.loadingText;
    if ((this.isDisabled || this.locked) && this.disabledText) return this.disabledText;
    return this.placeholder;
  }

  get visibleOptions(): ComboOption[] {
    const term = this.query.trim().toLowerCase();
    if (!term) return this.options;

    return this.options.filter((o) => o.label.toLowerCase().includes(term));
  }

  get isInteractive(): boolean {
    return !this.isDisabled && !this.locked && !this.loading;
  }

  // ---------------- Interaction ----------------

  open(): void {
    if (!this.isInteractive || !this.options.length) return;

    this.isOpen = true;
    this.highlight = this.options.findIndex((o) => o.value === this.value);
    this.cdr.markForCheck();
  }

  close(): void {
    this.isOpen = false;
    this.highlight = -1;
    this.query = '';
    this.cdr.markForCheck();
  }

  toggle(): void {
    if (this.isOpen) this.close();
    else this.open();
  }

  select(option: ComboOption): void {
    this.value = option.value;
    this.onChange(this.value);
    this.onTouched();
    this.close();
  }

  onInput(raw: string): void {
    if (!this.allowFreeText) {
      // Read-only fields still support type-to-filter while the panel is open.
      this.query = raw;
      this.isOpen = this.visibleOptions.length > 0;
      this.highlight = -1;
      this.cdr.markForCheck();
      return;
    }

    this.value = raw;
    this.query = raw;
    this.onChange(raw);
    this.isOpen = this.visibleOptions.length > 0;
    this.highlight = -1;
    this.cdr.markForCheck();
  }

  onBlur(): void {
    this.onTouched();
    this.close();
  }

  onKeydown(event: KeyboardEvent): void {
    const options = this.visibleOptions;

    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        if (!this.isOpen) this.open();
        else if (options.length) this.highlight = (this.highlight + 1) % options.length;
        break;

      case 'ArrowUp':
        event.preventDefault();
        if (options.length) {
          this.highlight = this.highlight <= 0 ? options.length - 1 : this.highlight - 1;
        }
        break;

      case 'Enter':
        // Only swallow Enter while actively choosing, so it still submits.
        if (this.isOpen && this.highlight >= 0 && options[this.highlight]) {
          event.preventDefault();
          this.select(options[this.highlight]);
        }
        break;

      case 'Escape':
        if (this.isOpen) {
          event.stopPropagation();
          this.close();
        }
        break;

      case 'Tab':
        this.close();
        break;
    }
  }

  /** Closing on outside click keeps a stray panel from covering other fields. */
  @HostListener('document:mousedown', ['$event'])
  onDocumentMouseDown(event: MouseEvent): void {
    if (!this.isOpen) return;
    if (!this.host.nativeElement.contains(event.target as Node)) this.close();
  }

  trackByValue = (_: number, option: ComboOption) => option.value;
}
