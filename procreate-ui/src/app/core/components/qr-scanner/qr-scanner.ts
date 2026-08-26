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
import jsQR from 'jsqr';
import { IconComponent } from '../icon/icon';

/**
 * Camera QR reader.
 *
 * Decoding uses jsQR rather than the browser's BarcodeDetector. The native API
 * looks attractive because it needs no dependency, but Chrome only implements
 * it on Android, ChromeOS and macOS — on Windows desktop it is absent, so it
 * degraded to manual entry on every machine in the clinic. jsQR is ~45 KB and
 * works everywhere a canvas does.
 *
 * Manual entry is still offered, for a damaged code or a machine with no
 * camera.
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
  /** False only when the machine has no camera API at all. */
  cameraSupported = !!navigator.mediaDevices?.getUserMedia;

  private stream: MediaStream | null = null;
  private canvas: HTMLCanvasElement | null = null;
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
    if (!this.cameraSupported) {
      this.cameraError = 'This browser cannot access a camera.';
      this.cdr.markForCheck();
      return;
    }

    this.isStarting = true;
    this.cameraError = '';
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

      this.canvas = document.createElement('canvas');
      this.isStarting = false;
      this.cdr.markForCheck();

      this.pollHandle = window.setInterval(() => this.tick(), 200);
    } catch (err: any) {
      this.isStarting = false;
      this.cameraError = this.describeCameraError(err);
      this.cdr.markForCheck();
    }
  }

  private describeCameraError(err: any): string {
    switch (err?.name) {
      case 'NotAllowedError':
      case 'SecurityError':
        return 'Camera permission was denied. Allow it in the browser, or type the code below.';
      case 'NotFoundError':
      case 'OverconstrainedError':
        return 'No camera was found on this machine. Type the code below instead.';
      case 'NotReadableError':
        return 'The camera is in use by another application. Close it, or type the code below.';
      default:
        return 'Could not start the camera. Type the code below instead.';
    }
  }

  /** Grabs a frame and runs it through jsQR. */
  private tick(): void {
    const video = this.videoRef?.nativeElement;
    const canvas = this.canvas;

    if (!video || !canvas) return;
    if (video.readyState < 2 || !video.videoWidth) return;

    const ctx = canvas.getContext('2d', { willReadFrequently: true });
    if (!ctx) return;

    canvas.width = video.videoWidth;
    canvas.height = video.videoHeight;
    ctx.drawImage(video, 0, 0, canvas.width, canvas.height);

    let frame: ImageData;
    try {
      frame = ctx.getImageData(0, 0, canvas.width, canvas.height);
    } catch {
      // A tainted or not-yet-painted frame; the next tick retries.
      return;
    }

    const result = jsQR(frame.data, frame.width, frame.height, {
      inversionAttempts: 'dontInvert',
    });

    const value = result?.data?.trim();
    if (!value || value === this.lastEmitted) return;

    this.lastEmitted = value;
    this.scanned.emit(value);
    this.cdr.markForCheck();
  }

  /** Releases the camera. Skipping this leaves the capture light on. */
  private stop(): void {
    if (this.pollHandle !== null) {
      clearInterval(this.pollHandle);
      this.pollHandle = null;
    }

    this.stream?.getTracks().forEach((track) => track.stop());
    this.stream = null;
    this.canvas = null;

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
