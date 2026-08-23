import { consume } from '@lit/context';
import { customElement, property } from 'lit/decorators.js';

import temperatureSliderStyles from './temperature-slider.styles';
import { preferencesContext } from '../../../contexts/preferences-context';
import { Preferences } from '../../../models/preferences';
import { getPreferredUnit } from '../../../services/utilities';
import sharedStyles from '../../../shared.styles';
import { Slider } from '../slider/slider';
import sliderStyles from '../slider/slider.styles';

const CELSIUS_MIN = 10;
const CELSIUS_MAX = 32;
const CELSIUS_STEP = 0.5;
const FAHRENHEIT_MIN = 50;
const FAHRENHEIT_MAX = 90;
const FAHRENHEIT_STEP = 1;

// Restyles app-slider (input-like track, rectangular full-height thumbs) without changing its behavior.
@customElement('app-temperature-slider')
export class TemperatureSlider extends Slider {
  static override styles = [sharedStyles, sliderStyles, temperatureSliderStyles];

  @consume({ context: preferencesContext, subscribe: true })
  preferences?: Preferences;

  @property({ type: Boolean, reflect: true })
  cooling = false;

  @property({ type: Boolean, reflect: true })
  heating = false;

  override formatValue = (value: number): string => {
    const unit = this.preferences?.temperatureUnit;
    const formatted = unit === 'Celsius' ? value.toFixed(1) : String(Math.round(value));
    return `${formatted}${getPreferredUnit(unit ?? 'Fahrenheit')}`;
  };

  // Applies temperature-appropriate min/max/step once preferences are known, but only while they still hold
  // Slider's own generic defaults - leaves them alone if a consumer already customized them.
  override willUpdate(changedProperties: Map<string, unknown>): void {
    if (this.preferences && this.min === 0 && this.max === 100 && this.step === 1) {
      const isCelsius = this.preferences.temperatureUnit === 'Celsius';
      this.min = isCelsius ? CELSIUS_MIN : FAHRENHEIT_MIN;
      this.max = isCelsius ? CELSIUS_MAX : FAHRENHEIT_MAX;
      this.step = isCelsius ? CELSIUS_STEP : FAHRENHEIT_STEP;
    }

    super.willUpdate(changedProperties);
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions -- declaration merging requires interface
  interface HTMLElementTagNameMap {
    'app-temperature-slider': TemperatureSlider;
  }
}
