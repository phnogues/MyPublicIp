"""Generates the app icons (MSIX visual assets + tray icon) from code.

Usage: python tools/generate-assets.py
Requires Pillow and the Segoe UI Bold font (Windows).
"""

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent
ASSETS = ROOT / "src" / "MyPublicIp" / "Assets"
FONT = Path("C:/Windows/Fonts/segoeuib.ttf")

BRAND = (73, 80, 182, 255)  # #4950B6
WHITE = (255, 255, 255, 255)
SUPERSAMPLE = 8


def badge(size: int, fill: float = 1.0) -> Image.Image:
    """Rounded square with "IP", occupying `fill` of a `size` x `size` transparent canvas."""
    big = size * SUPERSAMPLE
    img = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    side = round(big * fill)
    offset = (big - side) // 2
    radius = round(side * 0.18)
    draw.rounded_rectangle((offset, offset, offset + side - 1, offset + side - 1), radius, fill=BRAND)

    font = ImageFont.truetype(str(FONT), round(side * 0.58))
    left, top, right, bottom = draw.textbbox((0, 0), "IP", font=font)
    x = offset + (side - (right - left)) / 2 - left
    y = offset + (side - (bottom - top)) / 2 - top
    draw.text((x, y), "IP", font=font, fill=WHITE)

    return img.resize((size, size), Image.LANCZOS)


def centered(width: int, height: int, badge_size: int) -> Image.Image:
    img = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    b = badge(badge_size)
    img.alpha_composite(b, ((width - badge_size) // 2, (height - badge_size) // 2))
    return img


def save(img: Image.Image, name: str) -> None:
    img.save(ASSETS / name)


def main() -> None:
    ASSETS.mkdir(parents=True, exist_ok=True)
    scales = {100: 1.0, 125: 1.25, 150: 1.5, 200: 2.0, 400: 4.0}

    for scale, factor in scales.items():
        # App list / taskbar icon (plated)
        save(badge(round(44 * factor), fill=0.84), f"Square44x44Logo.scale-{scale}.png")
        # Medium tile: badge on transparent background
        s150 = round(150 * factor)
        save(centered(s150, s150, round(s150 * 0.6)), f"Square150x150Logo.scale-{scale}.png")
        # Wide tile
        save(centered(round(310 * factor), round(150 * factor), round(150 * factor * 0.6)), f"Wide310x150Logo.scale-{scale}.png")
        # Store logo
        save(badge(round(50 * factor), fill=0.9), f"StoreLogo.scale-{scale}.png")
        # Splash screen (unused by desktop apps, but required by the manifest schema)
        save(centered(round(620 * factor), round(300 * factor), round(300 * factor * 0.5)), f"SplashScreen.scale-{scale}.png")

    # Unplated icons used by the taskbar, Start and Explorer
    for size in (16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256):
        img = badge(size, fill=0.94 if size <= 24 else 0.88)
        save(img, f"Square44x44Logo.targetsize-{size}.png")
        save(img, f"Square44x44Logo.targetsize-{size}_altform-unplated.png")
        save(img, f"Square44x44Logo.targetsize-{size}_altform-lightunplated.png")

    # Tray / exe icon: the badge must fill nearly the whole cell to stay legible at 16 px
    sizes = [16, 20, 24, 32, 40, 48, 64, 256]
    frames = [badge(s, fill=1.0 if s <= 24 else 0.94) for s in sizes]
    frames[-1].save(ASSETS / "TrayIcon.ico", format="ICO", sizes=[(s, s) for s in sizes], append_images=frames[:-1])

    print(f"Assets written to {ASSETS}")


if __name__ == "__main__":
    main()
