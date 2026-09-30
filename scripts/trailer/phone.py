"""Photograph the shipped EQBuddy Mobile page, live, at a phone's viewport.

    python scripts/trailer/phone.py <http://ip:port/#token> <out-dir>

Called by phone.ps1 once the real app is serving. Headless Edge at 390x844 CSS px, DPR 3 —
a current iPhone — with a CLEAN profile, so the page makes its own first-run choice of
screens exactly as a phone that has just scanned the QR would (trap 67: the only honest
first pairing). Writes phone.png (the full page, for the trailer to scroll) and
phone-top.png (what fits on the screen), and fails if the page never draws a card.
"""
import sys, time
from pathlib import Path
from playwright.sync_api import sync_playwright

url, out = sys.argv[1], Path(sys.argv[2])
out.mkdir(parents=True, exist_ok=True)
with sync_playwright() as p:
    b = p.chromium.launch(channel='msedge', headless=True)
    ctx = b.new_context(viewport={'width': 390, 'height': 844}, device_scale_factor=3,
                        is_mobile=True, has_touch=True, color_scheme='dark')
    page = ctx.new_page()
    errors = []
    page.on('pageerror', lambda e: errors.append(str(e)))
    page.goto(url)
    # A snapshot has to arrive over the socket before anything but the header draws.
    deadline = time.time() + 30
    text = ''
    while time.time() < deadline:
        time.sleep(1.0)
        text = page.inner_text('body')
        if 'Dranak' in text or len(text) > 600:
            break
    time.sleep(3.0)  # let every section that was subscribed paint its first frame
    page.screenshot(path=str(out / 'phone-top.png'))
    page.screenshot(path=str(out / 'phone.png'), full_page=True)
    (out / 'phone.txt').write_text(page.inner_text('body'), encoding='utf-8')
    b.close()
    if errors:
        print('page errors:', errors)
    if len(text) < 200:
        print('The page drew almost nothing:', text[:300]); sys.exit(2)
    print('wrote', out / 'phone.png')
