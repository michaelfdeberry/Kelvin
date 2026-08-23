import { html, LitElement, nothing, TemplateResult } from 'lit';
import { customElement, property, query, state } from 'lit/decorators.js';
import { classMap } from 'lit/directives/class-map.js';
import { styleMap } from 'lit/directives/style-map.js';
import { when } from 'lit/directives/when.js';

import sliderStyles from './slider.styles';
import sharedStyles from '../../../shared.styles';

type ThumbKind = 'single' | 'low' | 'high';

@customElement('app-slider')
export class Slider extends LitElement {
  static override styles = [sharedStyles, sliderStyles];
  static readonly formAssociated = true;

  private readonly internals: ElementInternals = this.attachInternals();

  @query('.slider__track')
  private trackElement?: HTMLDivElement;

  @property({ type: Number, reflect: true }) min = 0;
  @property({ type: Number, reflect: true }) max = 100;
  @property({ type: Number, reflect: true }) step = 1;
  @property({ type: Boolean, reflect: true }) range = false;

  @property({ type: Number }) value = 0;
  @property({ type: Number, attribute: 'value-low' }) valueLow = 0;
  @property({ type: Number, attribute: 'value-high' }) valueHigh = 100;

  @property({ type: Number, reflect: true, attribute: 'min-gap' }) minGap?: number;
  @property({ type: Number, reflect: true, attribute: 'max-gap' }) maxGap?: number;

  @property({ type: String, reflect: true }) name = '';
  @property({ type: String, attribute: 'low-name' }) lowName = '';
  @property({ type: String, attribute: 'high-name' }) highName = '';

  @property({ type: Boolean, reflect: true }) disabled = false;

  @property({ type: String }) label = '';
  @property({ type: String, attribute: 'low-label' }) lowLabel = 'Minimum value';
  @property({ type: String, attribute: 'high-label' }) highLabel = 'Maximum value';

  @property({ attribute: false }) formatValue: (value: number) => string = value => String(value);

  @state() private draggingThumb: ThumbKind | null = null;
  @state() private focusedThumb: ThumbKind | null = null;

  // Captured once so `formResetCallback` can restore the values authored on the element.
  private defaultsCaptured = false;
  private defaultValue = 0;
  private defaultValueLow = 0;
  private defaultValueHigh = 100;

  private get resolvedLowName(): string {
    return this.name ? `${this.name}-low` : this.lowName;
  }

  private get resolvedHighName(): string {
    return this.name ? `${this.name}-high` : this.highName;
  }

  override connectedCallback(): void {
    super.connectedCallback();

    if (!this.defaultsCaptured) {
      this.defaultValue = this.value;
      this.defaultValueLow = this.valueLow;
      this.defaultValueHigh = this.valueHigh;
      this.defaultsCaptured = true;
    }

    this.syncFormValue();
  }

  override willUpdate(changedProperties: Map<string, unknown>): void {
    const rangeConfigChanged =
      changedProperties.has('min') ||
      changedProperties.has('max') ||
      changedProperties.has('step') ||
      changedProperties.has('minGap') ||
      changedProperties.has('maxGap');

    if (rangeConfigChanged || changedProperties.has('value')) {
      this.value = this.clampToStep(this.value);
    }

    if (rangeConfigChanged || changedProperties.has('valueLow') || changedProperties.has('valueHigh')) {
      // A plain property/attribute assignment touches both - default to treating the low thumb as the mover.
      const movedThumb: ThumbKind = changedProperties.has('valueHigh') && !changedProperties.has('valueLow') ? 'high' : 'low';
      this.commitRangeValues(this.valueLow, this.valueHigh, movedThumb);
    }
  }

  override updated(changedProperties: Map<string, unknown>): void {
    if (
      changedProperties.has('value') ||
      changedProperties.has('valueLow') ||
      changedProperties.has('valueHigh') ||
      changedProperties.has('name') ||
      changedProperties.has('lowName') ||
      changedProperties.has('highName') ||
      changedProperties.has('range') ||
      changedProperties.has('disabled')
    ) {
      this.syncFormValue();
    }
  }

  private syncFormValue(): void {
    if (this.disabled) {
      this.internals.setFormValue(null);
      return;
    }

    if (!this.range) {
      this.internals.setFormValue(String(this.value));
      return;
    }

    const lowName = this.resolvedLowName;
    const highName = this.resolvedHighName;
    if (!lowName || !highName) {
      this.internals.setFormValue(null);
      return;
    }

    const formData = new FormData();
    formData.append(lowName, String(this.valueLow));
    formData.append(highName, String(this.valueHigh));
    this.internals.setFormValue(formData);
  }

  formResetCallback(): void {
    this.value = this.defaultValue;
    this.valueLow = this.defaultValueLow;
    this.valueHigh = this.defaultValueHigh;
  }

  formDisabledCallback(disabled: boolean): void {
    this.disabled = disabled;
  }

  private clampToRange(value: number): number {
    return Math.min(this.max, Math.max(this.min, value));
  }

  private clampToStep(value: number): number {
    const clamped = this.clampToRange(value);
    if (this.step <= 0) return clamped;

    const steps = Math.round((clamped - this.min) / this.step);
    return this.clampToRange(this.min + steps * this.step);
  }

  // Clamps/orders/gap-enforces synchronously so callers can dispatch events immediately afterwards with correct values.
  private commitRangeValues(rawLow: number, rawHigh: number, movedThumb: ThumbKind): void {
    let low = this.clampToStep(rawLow);
    let high = this.clampToStep(rawHigh);
    if (low > high) {
      if (movedThumb === 'low') low = high;
      else high = low;
    }

    const enforced = this.enforceGap(low, high, movedThumb);
    this.valueLow = enforced.low;
    this.valueHigh = enforced.high;
  }

  private enforceGap(low: number, high: number, movedThumb: ThumbKind): { low: number; high: number } {
    let result = { low, high };

    if (this.minGap !== undefined && result.high - result.low < this.minGap) {
      result =
        movedThumb === 'low'
          ? { low: result.low, high: this.clampToRange(result.low + this.minGap) }
          : { low: this.clampToRange(result.high - this.minGap), high: result.high };

      // Pushing the other thumb hit min/max - fall back to stopping the dragged thumb instead.
      if (result.high - result.low < this.minGap) {
        result =
          movedThumb === 'low'
            ? { low: this.clampToRange(result.high - this.minGap), high: result.high }
            : { low: result.low, high: this.clampToRange(result.low + this.minGap) };
      }
    }

    if (this.maxGap !== undefined && result.high - result.low > this.maxGap) {
      result =
        movedThumb === 'low'
          ? { low: result.low, high: this.clampToRange(result.low + this.maxGap) }
          : { low: this.clampToRange(result.high - this.maxGap), high: result.high };
    }

    return result;
  }

  private percentFor(value: number): number {
    const span = this.max - this.min;
    if (span <= 0) return 0;

    return Math.min(100, Math.max(0, ((value - this.min) / span) * 100));
  }

  private valueFromClientX(clientX: number): number {
    const track = this.trackElement;
    if (!track) return this.min;

    const rect = track.getBoundingClientRect();
    const ratio = rect.width === 0 ? 0 : Math.min(1, Math.max(0, (clientX - rect.left) / rect.width));
    return this.min + ratio * (this.max - this.min);
  }

  private resolveThumbForPosition(rawValue: number): ThumbKind {
    if (!this.range) return 'single';

    const distanceLow = Math.abs(rawValue - this.valueLow);
    const distanceHigh = Math.abs(rawValue - this.valueHigh);
    return distanceLow <= distanceHigh ? 'low' : 'high';
  }

  private moveThumb(thumb: ThumbKind, rawValue: number): void {
    if (thumb === 'single') {
      this.value = this.clampToStep(rawValue);
      return;
    }

    if (thumb === 'low') {
      this.commitRangeValues(rawValue, this.valueHigh, 'low');
    } else {
      this.commitRangeValues(this.valueLow, rawValue, 'high');
    }
  }

  private focusThumb(thumb: ThumbKind): void {
    this.shadowRoot?.querySelector<HTMLElement>(`.slider__thumb--${thumb}`)?.focus();
  }

  private dispatchInput(): void {
    this.dispatchEvent(new Event('input', { bubbles: true, composed: true }));
  }

  private dispatchChange(): void {
    this.dispatchEvent(new Event('change', { bubbles: true, composed: true }));
  }

  private handleTrackPointerDown(event: PointerEvent): void {
    if (this.disabled) return;

    const track = event.currentTarget as HTMLDivElement;
    track.setPointerCapture(event.pointerId);

    const rawValue = this.valueFromClientX(event.clientX);
    const thumb = this.resolveThumbForPosition(rawValue);
    this.draggingThumb = thumb;
    this.moveThumb(thumb, rawValue);
    this.dispatchInput();
    this.focusThumb(thumb);
  }

  private handleTrackPointerMove(event: PointerEvent): void {
    if (!this.draggingThumb) return;

    this.moveThumb(this.draggingThumb, this.valueFromClientX(event.clientX));
    this.dispatchInput();
  }

  private handleTrackPointerEnd(event: PointerEvent): void {
    if (!this.draggingThumb) return;

    const track = event.currentTarget as HTMLDivElement;
    if (track.hasPointerCapture(event.pointerId)) track.releasePointerCapture(event.pointerId);

    this.draggingThumb = null;
    this.dispatchChange();
  }

  private handleThumbPointerDown(thumb: ThumbKind, event: PointerEvent): void {
    if (this.disabled) return;

    // Stop the track's own pointerdown handler from also re-resolving the target thumb.
    event.stopPropagation();
    this.trackElement?.setPointerCapture(event.pointerId);
    this.draggingThumb = thumb;
    (event.currentTarget as HTMLElement).focus();
  }

  private handleThumbKeyDown(thumb: ThumbKind, event: KeyboardEvent): void {
    if (this.disabled) return;

    const bigStep = this.step * 10;
    let delta: number;
    switch (event.key) {
      case 'ArrowLeft':
      case 'ArrowDown':
        delta = -this.step;
        break;
      case 'ArrowRight':
      case 'ArrowUp':
        delta = this.step;
        break;
      case 'PageDown':
        delta = -bigStep;
        break;
      case 'PageUp':
        delta = bigStep;
        break;
      case 'Home':
        delta = Number.NEGATIVE_INFINITY;
        break;
      case 'End':
        delta = Number.POSITIVE_INFINITY;
        break;
      default:
        return;
    }

    event.preventDefault();
    const current = thumb === 'single' ? this.value : thumb === 'low' ? this.valueLow : this.valueHigh;
    const rawValue = delta === Number.NEGATIVE_INFINITY ? this.min : delta === Number.POSITIVE_INFINITY ? this.max : current + delta;
    this.moveThumb(thumb, rawValue);
    this.dispatchInput();
    this.dispatchChange();
  }

  private handleThumbFocus(thumb: ThumbKind): void {
    this.focusedThumb = thumb;
  }

  private handleThumbBlur(thumb: ThumbKind): void {
    if (this.focusedThumb === thumb) this.focusedThumb = null;
  }

  private renderThumb(thumb: ThumbKind, pct: number, value: number, ariaLabel: string): TemplateResult {
    const showBubble = this.draggingThumb === thumb || this.focusedThumb === thumb;

    return html`
      <div
        class=${classMap({
          slider__thumb: true,
          'slider__thumb--single': thumb === 'single',
          'slider__thumb--low': thumb === 'low',
          'slider__thumb--high': thumb === 'high',
          'slider__thumb--active': showBubble,
        })}
        style="left: ${pct}%"
        role="slider"
        tabindex=${this.disabled ? -1 : 0}
        aria-label=${ariaLabel || nothing}
        aria-valuemin=${this.min}
        aria-valuemax=${this.max}
        aria-valuenow=${value}
        aria-valuetext=${this.formatValue(value)}
        aria-disabled=${this.disabled ? 'true' : 'false'}
        @pointerdown=${(event: PointerEvent) => this.handleThumbPointerDown(thumb, event)}
        @keydown=${(event: KeyboardEvent) => this.handleThumbKeyDown(thumb, event)}
        @focus=${() => this.handleThumbFocus(thumb)}
        @blur=${() => this.handleThumbBlur(thumb)}
      >
        ${when(showBubble, () => html`<span class="slider__bubble">${this.formatValue(value)}</span>`)}
      </div>
    `;
  }

  override render(): TemplateResult {
    const lowValue = this.range ? this.valueLow : this.value;
    const lowPct = this.percentFor(lowValue);
    const highPct = this.range ? this.percentFor(this.valueHigh) : lowPct;

    return html`
      <div class=${classMap({ slider: true, 'slider--range': this.range })}>
        <div
          class="slider__track"
          @pointerdown=${this.handleTrackPointerDown}
          @pointermove=${this.handleTrackPointerMove}
          @pointerup=${this.handleTrackPointerEnd}
          @pointercancel=${this.handleTrackPointerEnd}
        >
          <div
            class="slider__segment slider__segment--start"
            style=${styleMap({
              width: `${lowPct}%`,
            })}
          ></div>
          ${when(
            this.range,
            () => html`
              <div
                class="slider__segment slider__segment--middle"
                style=${styleMap({
                  left: `${lowPct}%`,
                  width: `${highPct - lowPct}%`,
                })}
              ></div>
            `,
          )}
          <div
            class="slider__segment slider__segment--end"
            style=${styleMap({
              left: `${highPct}%`,
              width: `${100 - highPct}%`,
            })}
          ></div>

          ${this.renderThumb(this.range ? 'low' : 'single', lowPct, lowValue, this.range ? this.lowLabel : this.label)}
          ${when(this.range, () => this.renderThumb('high', highPct, this.valueHigh, this.highLabel))}
        </div>
      </div>
    `;
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions -- declaration merging requires interface
  interface HTMLElementTagNameMap {
    'app-slider': Slider;
  }
}
