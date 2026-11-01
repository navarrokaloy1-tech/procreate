import { AfterViewInit, Directive, ElementRef, OnDestroy } from '@angular/core';

/**
 * Brings an error banner into view whenever it appears or its message changes.
 *
 * Applied by class selector to every `.alert-danger`, so each modal (and page)
 * error is revealed without per-component wiring. The original problem: an error
 * banner sits at the top of a scrolling modal body, but triggering it from a
 * footer button left it off-screen below the fold.
 *
 * `scrollIntoView` walks up to whichever ancestor actually scrolls, so it works
 * regardless of a modal's layout, and `block: 'nearest'` is a no-op when the
 * banner is already visible (e.g. a page error at the top of the screen).
 *
 * A MutationObserver re-reveals the banner when its text changes to a new
 * message without the element being recreated — e.g. one validation error
 * replacing another after a second save attempt.
 */
@Directive({
  selector: '.alert-danger',
  standalone: true,
})
export class ScrollOnErrorDirective implements AfterViewInit, OnDestroy {
  private observer?: MutationObserver;

  constructor(private host: ElementRef<HTMLElement>) {}

  ngAfterViewInit(): void {
    this.reveal();
    this.observer = new MutationObserver(() => this.reveal());
    this.observer.observe(this.host.nativeElement, {
      childList: true,
      characterData: true,
      subtree: true,
    });
  }

  ngOnDestroy(): void {
    this.observer?.disconnect();
  }

  /** Deferred so layout has settled before the banner is scrolled to. */
  private reveal(): void {
    const el = this.host.nativeElement;
    setTimeout(() => el.scrollIntoView({ behavior: 'smooth', block: 'nearest' }));
  }
}
