import { CommonModule } from '@angular/common';
import {
  ChangeDetectorRef,
  Component,
  ElementRef,
  EventEmitter,
  Input,
  OnChanges,
  OnDestroy,
  Output,
  SimpleChanges,
  ViewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { IconComponent } from '../icon/icon';

/**
 * Camera QR reader.
 *
 * Decoding uses the browser's built-in BarcodeDetector rather than a bundled
 * library — it keeps the payload small and needs no dependency. It ships in
 * Chrome and Edge on Windows, which is what the clinic runs; anywhere it is
 * missing, the component falls back to typing the code, so the flow never
 * becomes unusable.
 */
@Component({
  selector: 'app-qr-scanner',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent],
  templateUrl: './qr-scanner.html',
  styleUrls: ['./qr-scanner.scss'],
})
export class QrScannerComponent implements OnChanges, OnDestroy {
  @Input() open = false;
  @Input() title = 'Scan Patient QR';
  @Input() hint = 'Hold the patient card steady inside the frame.';
  /** Set by the host after a failed lookup, so the panel can stay open. */
  @Input() errorMessage = '';

  @Output() scanned = new EventEmitter<string>();
  @Output() closed = new EventEmitter<void>();

  @ViewChild('video') videoRef?: ElementRef<HTMLVideoElement>;

  isStarting = false;
  cameraError = '';
  manualCode = '';
  /** False when BarcodeDetector is unavailable; the manual field is shown instead. */
  detectorSupported = 'BarcodeDetector' in window;

  private stream: MediaStream | null = null;
  private detector: any = null;
  private pollHandle: number | null = null;
  /** Guards against emitting the same code repeatedly while it stays in frame. */
  private lastEmitted = '';

  constructor(private cdr: ChangeDetectorRef) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (!changes['open']) return;

    if (this.open) {
      this.lastEmitted = '';
      this.manualCode = '';
      this.cameraError = '';
      // Wait a tick so the <video> exists before attaching the stream.
      setTimeout(() => this.start(), 0);
    } else {
      this.stop();
    }
  }

  ngOnDestroy(): void {
    this.stop();
  }

  private async start(): Promise<void> {
    if (!this.detectorSupported) return;

    if (!navigator.mediaDevices?.getUserMedia) {
      this.cameraError = 'This browser cannot access a camera.';
      this.detectorSupported = false;
      this.cdr.markForCheck();
      return;
    }

    this.isStarting = true;
    this.cdr.markForCheck();

    try {
      this.stream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: 'environment' },
      });

      const video = this.videoRef?.nativeElement;
      if (video) {
        video.srcObject = this.stream;
        await video.play();
      }

      this.detector = new (window as any).BarcodeDetector({ formats: ['qr_code'] });
      this.isStarting = false;
      this.cdr.markForCheck();

      this.pollHandle = window.setInterval(() => this.tick(), 250);
    } catch (err: any) {
      this.isStarting = false;
      this.cameraError =
        err?.name === 'NotAllowedError'
          ? 'Camera permission was denied. Allow it in the browser, or type the code below.'
          : 'Could not start the camera. Type the code below instead.';
      this.cdr.markForCheck();
    }
  }

  private async tick(): Promise<void> {
    const video = this.videoRef?.nativeElement;
    if (!video || !this.detector || video.readyState !== 4) return;

    try {
      const codes = await this.detector.detect(video);
      if (!codes?.length) return;

      const value = String(codes[0].rawValue ?? '').trim();
      if (!value || value === this.lastEmitted) return;

      this.lastEmitted = value;
      this.scanned.emit(value);
      this.cdr.markForCheck();
    } catch {
      // A single failed frame is not worth surfacing; the next tick retries.
    }
  }

  /** Releases the camera. Skipping this leaves the capture light on. */
  private stop(): void {
    if (this.pollHandle !== null) {
      clearInterval(this.pollHandle);
      this.pollHandle = null;
    }

    this.stream?.getTracks().forEach((track) => track.stop());
    this.stream = null;
    this.detector = null;

    const video = this.videoRef?.nativeElement;
    if (video) video.srcObject = null;
  }

  submitManual(): void {
    const code = this.manualCode.trim();
    if (!code) return;

    this.lastEmitted = code;
    this.scanned.emit(code);
  }

  close(): void {
    this.stop();
    this.closed.emit();
  }
}
