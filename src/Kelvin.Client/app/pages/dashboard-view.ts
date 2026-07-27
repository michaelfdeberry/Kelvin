import { LitElement, html } from 'lit';

export class DashboardView extends LitElement {
  override render() {
    return html`<h1>Dashboard</h1>`;
  }
}
customElements.define('dashboard-view', DashboardView);
