import { html, LitElement, nothing, TemplateResult } from 'lit';
import { customElement, property, query, queryAssignedElements, state } from 'lit/decorators.js';

import tabsStyles from './tabs.styles';
import sharedStyles from '../../../shared.styles';

@customElement('app-tabs')
export class Tabs extends LitElement {
  static override styles = [sharedStyles, tabsStyles];

  @queryAssignedElements({ slot: 'tab', flatten: true })
  private tabs!: HTMLButtonElement[];

  @queryAssignedElements({ slot: 'panel', flatten: true })
  private panels!: HTMLElement[];

  @query('.tabs__tablist')
  private tablistElement?: HTMLDivElement;

  @property()
  description = '';

  @state()
  private canScrollLeft = false;

  @state()
  private canScrollRight = false;

  private resizeObserver?: ResizeObserver;

  override firstUpdated(): void {
    this.configureTabs();
  }

  override disconnectedCallback(): void {
    super.disconnectedCallback();
    this.resizeObserver?.disconnect();
  }

  private updateScrollState = (): void => {
    const tablist = this.tablistElement;
    if (!tablist) return;

    const maxScrollLeft = tablist.scrollWidth - tablist.clientWidth;
    this.canScrollLeft = tablist.scrollLeft > 1;
    this.canScrollRight = tablist.scrollLeft < maxScrollLeft - 1;
  };

  private handleTabSlotChange(): void {
    this.updateScrollState();
    this.configureTabs();
  }

  private configureTabs(): void {
    this.resizeObserver = new ResizeObserver(this.updateScrollState);
    if (this.tablistElement) this.resizeObserver.observe(this.tablistElement);
    this.updateScrollState();

    if (!this.tabs?.length) return;
    if (!this.panels?.length) return;

    this.tabs.forEach((tab, index) => {
      const panel = this.panels[index];
      if (!panel) return;

      tab.setAttribute('aria-controls', panel.id);
      tab.setAttribute('role', 'tab');
      tab.setAttribute('tabindex', index === 0 ? '0' : '-1');
      tab.setAttribute('aria-selected', index === 0 ? 'true' : 'false');
      tab.classList.toggle('tab', true);
      tab.classList.toggle('tab--selected', index === 0);
    });

    this.panels.forEach((panel, index) => {
      const tab = this.tabs[index];
      if (!tab) return;

      panel.setAttribute('role', 'tabpanel');
      panel.setAttribute('aria-labelledby', tab.id);
      panel.classList.toggle('panel', true);
      panel.classList.toggle('panel--selected', index === 0);
    });
  }

  private handleScrollButtonClick(direction: -1 | 1): void {
    const tablist = this.tablistElement;
    if (!tablist) return;

    tablist.scrollBy({ left: tablist.clientWidth * 0.8 * direction, behavior: 'smooth' });
  }

  private handleTabClick(event: Event): void {
    event.preventDefault();
    event.stopPropagation();

    const path = event.composedPath();
    const clickedTab = path.find(el => el instanceof HTMLButtonElement && el.slot === 'tab') as HTMLButtonElement | undefined;
    if (!clickedTab) return;

    const clickedIndex = this.tabs.indexOf(clickedTab);

    this.tabs.forEach((tab, index) => {
      const panel = this.panels[index];
      if (!panel) return;

      const isSelected = index === clickedIndex;
      tab.setAttribute('tabindex', isSelected ? '0' : '-1');
      tab.setAttribute('aria-selected', isSelected ? 'true' : 'false');
      tab.classList.toggle('tab--selected', isSelected);
      panel.classList.toggle('panel--selected', isSelected);
    });
  }

  override render(): TemplateResult | typeof nothing {
    return html`
      <div
        class="tabs"
        aria-label="${this.description}"
      >
        <div class="tabs__tablist-wrapper">
          <button
            type="button"
            class="tabs__scroll-button tabs__scroll-button--left"
            aria-label="Scroll tabs left"
            ?hidden=${!this.canScrollLeft}
            @click=${() => this.handleScrollButtonClick(-1)}
          >
            ‹
          </button>
          <div
            class="tabs__tablist"
            role="tablist"
            @scroll=${this.updateScrollState}
          >
            <slot
              name="tab"
              @click=${this.handleTabClick}
              @slotchange=${this.handleTabSlotChange}
            ></slot>
          </div>
          <button
            type="button"
            class="tabs__scroll-button tabs__scroll-button--right"
            aria-label="Scroll tabs right"
            ?hidden=${!this.canScrollRight}
            @click=${() => this.handleScrollButtonClick(1)}
          >
            ›
          </button>
        </div>
        <div class="tabs__panels">
          <slot
            name="panel"
            @slotchange=${this.configureTabs}
          ></slot>
        </div>
      </div>
    `;
  }
}
