import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

/**
 * Minimal page that lives at /ctrader-callback.
 * cTrader redirects here after OAuth with ?code=XXX.
 * This component posts the code to the opener window, then closes the popup.
 */
@Component({
  selector: 'app-ctrader-callback',
  standalone: true,
  template: `
    <div style="display:flex;align-items:center;justify-content:center;height:100vh;
                font-family:sans-serif;background:#0f172a;color:#e2e8f0;">
      <div style="text-align:center">
        <div style="font-size:2rem;margin-bottom:1rem">✓</div>
        <p>Authentication successful. Closing window…</p>
      </div>
    </div>
  `,
})
export class CTraderCallbackComponent implements OnInit {
  constructor(private route: ActivatedRoute) {}

  ngOnInit(): void {
    const code  = this.route.snapshot.queryParamMap.get('code');
    const error = this.route.snapshot.queryParamMap.get('error');

    if (window.opener) {
      window.opener.postMessage(
        { type: 'ctrader_oauth', code, error },
        window.location.origin
      );
      window.close();
    }
  }
}
