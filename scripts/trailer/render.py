"""EQBuddy Evolved launch trailer - render the timeline to an MP4.

    python scripts/trailer/render.py <build-dir> <out.mp4> [--fps 30] [--from 0 --to 65.5] [--stills t1,t2,...]

<build-dir> holds what the capture recipes produced (README.md lists them):
    hud/%05d.png     keyed frames of `record-tray-gifs.ps1 -Gif trailer-hud`
    game/%05d.jpg    the blurred in-game backdrop
    rooms/*.png      `shoot.ps1 -Shot trailer-*` stills
    phone.png        the phone page, captured from the real companion server
    score.wav        `score.py`
    plan.json        crops, spotlights, captions and timings (plan.py writes it)

trailer.html and the fonts are copied in next to them; headless Edge loads the page, calls
seek(t) per frame, and the screenshots stream straight into ffmpeg (no frame dump on disk).
"""
import argparse, json, shutil, subprocess, sys, time
from pathlib import Path
from playwright.sync_api import sync_playwright

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent

ap = argparse.ArgumentParser()
ap.add_argument('build'); ap.add_argument('out')
ap.add_argument('--fps', type=int, default=30)
ap.add_argument('--from', dest='t0', type=float, default=0.0)
ap.add_argument('--to', dest='t1', type=float, default=None)
ap.add_argument('--stills', default='', help='comma-separated times: write PNGs instead of a video')
ap.add_argument('--crf', type=int, default=16)
a = ap.parse_args()

build = Path(a.build).resolve()
shutil.copy(HERE / 'trailer.html', build / 'trailer.html')
(build / 'fonts').mkdir(exist_ok=True)
for f in (REPO / 'site/assets/fonts').glob('*.ttf'):
    shutil.copy(f, build / 'fonts' / f.name)
plan = json.loads((build / 'plan.json').read_text(encoding='utf-8'))
duration = plan['duration'] if a.t1 is None else a.t1

with sync_playwright() as p:
    browser = p.chromium.launch(channel='msedge', headless=True,
                                args=['--force-color-profile=srgb', '--disable-lcd-text'])
    page = browser.new_page(viewport={'width': 1920, 'height': 1080}, device_scale_factor=1)
    page.goto((build / 'trailer.html').as_uri())
    page.evaluate('plan => window.load(plan)', plan)
    errors = []
    page.on('pageerror', lambda e: errors.append(str(e)))

    if a.stills:
        for s in a.stills.split(','):
            t = float(s)
            page.evaluate(f'seek({t})')
            out = Path(a.out).parent / f'still-{t:06.2f}.png'
            page.screenshot(path=str(out))
            print('wrote', out)
        if errors: print('PAGE ERRORS:', errors); sys.exit(1)
        sys.exit(0)

    n = int(round((duration - a.t0) * a.fps))
    audio = ['-ss', str(a.t0), '-t', str(duration - a.t0), '-i', str(build / 'score.wav')] if (build / 'score.wav').exists() else []
    cmd = ['ffmpeg', '-v', 'error', '-y', '-f', 'image2pipe', '-framerate', str(a.fps), '-c:v', 'mjpeg', '-i', '-',
           *audio,
           '-c:v', 'libx264', '-preset', 'slow', '-crf', str(a.crf), '-pix_fmt', 'yuv420p',
           '-profile:v', 'high', '-movflags', '+faststart',
           '-color_primaries', 'bt709', '-color_trc', 'bt709', '-colorspace', 'bt709']
    if audio:
        cmd += ['-c:a', 'aac', '-b:a', '320k', '-shortest']
    cmd += [a.out]
    enc = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    t_start = time.time()
    for i in range(n):
        t = a.t0 + i / a.fps
        page.evaluate(f'seek({t:.5f})')
        enc.stdin.write(page.screenshot(type='jpeg', quality=97))
        if i % 150 == 0:
            el = time.time() - t_start
            print(f'  frame {i}/{n}  t={t:6.2f}s  {el:5.0f}s elapsed', flush=True)
    enc.stdin.close()
    enc.wait()
    browser.close()
    if errors:
        print('PAGE ERRORS:', errors); sys.exit(1)
    print('wrote', a.out, f'({n} frames in {time.time() - t_start:.0f}s)')
