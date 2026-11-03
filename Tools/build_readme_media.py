"""Build README preview artwork and banner from game assets.

Requires Pillow and Playwright (with Edge or a Chromium browser installed).
Run ReadmeCapture.Run in the isolated Unity project first. No game assets change.
"""
from pathlib import Path
from PIL import Image
from playwright.sync_api import sync_playwright, Error

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "Docs/Media"
WORK = ROOT / "Backups/ReadmeDesign"
CAPTURES = ROOT / "Backups/TutorialValidationProject/ReadmePreview"


def webp(source, destination, width=None, quality=90):
    with Image.open(source) as image:
        if width and image.width > width:
            image = image.resize((width, round(image.height * width / image.width)), Image.Resampling.LANCZOS)
        image.save(OUTPUT / destination, "WEBP", quality=quality, method=6)


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    WORK.mkdir(parents=True, exist_ok=True)
    for name in ("menu", "gameplay", "academy", "notebook"):
        webp(CAPTURES / (name + ".png"), name + ".webp", quality=91)
    for name in ("cur", "village"):
        webp(ROOT / f"Assets/UI/ModeIcons/mode-{name}.png", f"faction-{name}.webp", 320)

    background = (ROOT / "Assets/Resources/ThereasmUI/village-night.png").as_uri()
    emblem = (ROOT / "Assets/UI/ModeIcons/mode-cur.png").as_uri()
    font = (ROOT / "Assets/Almendra-Regular.ttf").as_uri()
    html = """<!doctype html><html><head><meta charset="utf-8"><style>
    @font-face {font-family:Almendra;src:url('__FONT__')}
    *{box-sizing:border-box}html,body{margin:0;width:1600px;height:660px;overflow:hidden}
    body{background:#100b0e;color:#f6e5bc;font-family:Arial,sans-serif}
    .scene{position:absolute;inset:0;background:url('__BACKGROUND__') center/cover}
    .shade{position:absolute;inset:0;background:linear-gradient(90deg,rgba(12,8,12,.97),rgba(12,8,12,.89) 43%,rgba(12,8,12,.26) 100%)}
    .frame{position:absolute;inset:25px;border:1px solid #b48b4c88}
    .frame:after{content:'';position:absolute;inset:6px;border:1px solid #b48b4c33}
    .corner{position:absolute;width:18px;height:18px;background:#d6b56e;transform:rotate(45deg)}
    .a{left:17px;top:17px}.b{right:17px;top:17px}.c{left:17px;bottom:17px}.d{right:17px;bottom:17px}
    .copy{position:absolute;left:82px;top:76px;width:790px}
    .eyebrow{font-size:16px;font-weight:bold;letter-spacing:4px;color:#d8b66f}
    h1{font-family:Almendra,Georgia,serif;font-size:151px;font-weight:normal;letter-spacing:5px;line-height:1;margin:29px 0 16px;text-shadow:0 4px 32px #0008}
    .tagline{font-family:Almendra,Georgia,serif;font-size:38px;margin:0 0 24px;color:#f6e5bc}
    .body{font-size:23px;line-height:1.65;color:#c9c1b3;margin:0;width:620px}
    .rule{width:70px;height:2px;background:#b88e4e;margin:30px 0}
    .modes{display:flex;gap:26px;font-size:14px;letter-spacing:1.7px;color:#d9cba9;font-weight:bold}
    .modes span+span{border-left:1px solid #b48b4c66;padding-left:26px}
    .halo{position:absolute;right:20px;top:10px;width:660px;height:620px;background:radial-gradient(ellipse,#911a1a40,transparent 65%)}
    .emblem{position:absolute;right:99px;top:77px;width:460px;height:460px;object-fit:contain;filter:drop-shadow(0 20px 26px #000b)}
    .whisper{position:absolute;right:82px;bottom:83px;width:500px;text-align:center;font-size:13px;letter-spacing:2.5px;color:#dec28e}
    </style></head><body><div class="scene"></div><div class="shade"></div><div class="halo"></div>
    <div class="frame"></div><i class="corner a"></i><i class="corner b"></i><i class="corner c"></i><i class="corner d"></i>
    <div class="copy"><div class="eyebrow">STRATEGY · DEDUCTION · DECEPTION</div>
    <h1>THEREASM</h1><p class="tagline">A curse hides in plain sight.</p>
    <p class="body">Read the table. Guard your secrets.<br>Make every vote count.</p>
    <div class="rule"></div><div class="modes"><span>SOLO VS BOTS</span><span>2-PLAYER ONLINE</span><span>EN / VI</span></div></div>
    <img class="emblem" src="__EMBLEM__" alt="Cursermancer emblem from the game">
    <div class="whisper">ONE VILLAGE. TWO SIDES. TRUST NO CARD.</div></body></html>"""
    html = html.replace("__FONT__", font).replace("__BACKGROUND__", background).replace("__EMBLEM__", emblem)
    source = WORK / "banner.html"
    source.write_text(html, encoding="utf-8")
    with sync_playwright() as playwright:
        try:
            browser = playwright.chromium.launch(channel="msedge", headless=True)
        except Error:
            browser = playwright.chromium.launch(headless=True)
        page = browser.new_page(viewport={"width": 1600, "height": 660}, device_scale_factor=1)
        page.goto(source.as_uri())
        page.evaluate("document.fonts.ready")
        page.locator(".emblem").evaluate("image => image.decode()")
        assert page.locator("h1").evaluate("e => e.scrollWidth <= e.clientWidth"), "Banner title overflows"
        page.screenshot(path=str(WORK / "banner.png"))
        browser.close()
    webp(WORK / "banner.png", "banner.webp", quality=94)
    for path in sorted(OUTPUT.iterdir()):
        print(f"{path.name}: {path.stat().st_size / 1024:.0f} KiB")


if __name__ == "__main__":
    main()
