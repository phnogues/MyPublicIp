"""Generates every icon of the app from code: MSIX visual assets, tray icon, README logo and Store logos.

Design: a teal-to-blue gradient tile with a bold white "IP" in front of a faint globe. At tray sizes
(32 px and below) the globe and the shadow are left out and the letters are bigger, so that the icon
stays readable next to the clock.

Usage: python tools/generate-assets.py
Requires Pillow, NumPy and the Segoe UI Variable font (Windows 11).
"""

from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parent.parent
ASSETS = ROOT / "src" / "MyPublicIp" / "Assets"
README_LOGO = ROOT / "docs" / "images" / "icon.png"
# Store listing images are uploaded to Partner Center, not committed (artifacts/ is ignored)
STORE_LOGOS = ROOT / "artifacts" / "store" / "logos"

FONT = Path("C:/Windows/Fonts/SegUIVar.ttf")
SUPERSAMPLE = 8

TEAL = (0, 184, 212)
BLUE = (37, 99, 235)
WHITE = (255, 255, 255, 255)
SMALL = 32  # up to this size: no globe, no shadow, bigger letters


def font(size: int, weight: str = "Bold Display") -> ImageFont.FreeTypeFont:
    f = ImageFont.truetype(str(FONT), size)
    f.set_variation_by_name(weight)
    return f


def gradient(width: int, height: int, start=TEAL, end=BLUE) -> Image.Image:
    """Diagonal gradient from the top-left corner to the bottom-right corner."""
    y, x = np.mgrid[0:height, 0:width].astype(np.float32)
    t = (x / max(width - 1, 1) + y / max(height - 1, 1)) / 2
    rgb = np.array(start, np.float32) + (np.array(end, np.float32) - np.array(start, np.float32)) * t[..., None]
    alpha = np.full((height, width, 1), 255, np.float32)
    return Image.fromarray(np.concatenate([rgb, alpha], axis=2).astype(np.uint8), "RGBA")


def overlay(canvas: Image.Image, draw) -> None:
    """Draws on a separate layer so that translucent strokes blend with the tile instead of cutting holes."""
    layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    draw(ImageDraw.Draw(layer))
    canvas.alpha_composite(layer)


def draw_globe(draw: ImageDraw.ImageDraw, cx: float, cy: float, r: float, width: int, color) -> None:
    draw.ellipse((cx - r, cy - r, cx + r, cy + r), outline=color, width=width)
    draw.ellipse((cx - r * 0.42, cy - r, cx + r * 0.42, cy + r), outline=color, width=width)
    draw.line((cx, cy - r, cx, cy + r), fill=color, width=width)
    draw.line((cx - r, cy, cx + r, cy), fill=color, width=width)
    for k in (-0.52, 0.52):
        yy = cy + r * k
        half = r * (1 - k * k) ** 0.5
        draw.line((cx - half, yy, cx + half, yy), fill=color, width=width)


def draw_ip(canvas: Image.Image, box, size: int, shadow_blur: float) -> None:
    """White "IP" centered in box, with a soft shadow that lifts it from the globe."""
    f = font(size)

    def text(draw: ImageDraw.ImageDraw, color) -> None:
        left, top, right, bottom = draw.textbbox((0, 0), "IP", font=f)
        x = box[0] + (box[2] - box[0] - (right - left)) / 2 - left
        y = box[1] + (box[3] - box[1] - (bottom - top)) / 2 - top
        draw.text((x, y), "IP", font=f, fill=color)

    if shadow_blur > 0:
        shadow = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
        text(ImageDraw.Draw(shadow), (10, 40, 110, 110))
        canvas.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(shadow_blur)))
    overlay(canvas, lambda d: text(d, WHITE))


def artwork(width: int, height: int, small: bool) -> Image.Image:
    """Gradient, top highlight, globe and "IP", filling the whole rectangle (no rounded corners)."""
    side = min(width, height)
    art = gradient(width, height)

    highlight = Image.new("L", (width, height), 0)
    ImageDraw.Draw(highlight).ellipse((-width * 0.3, -height * 0.9, width * 1.3, height * 0.45), fill=60)
    highlight = highlight.filter(ImageFilter.GaussianBlur(side * 0.06))
    art = Image.composite(Image.new("RGBA", (width, height), WHITE), art, highlight)

    if not small:
        overlay(art, lambda d: draw_globe(d, width / 2, height / 2, side * 0.40, max(1, round(side * 0.03)), (255, 255, 255, 70)))

    text_ratio = 0.66 if side <= 16 * SUPERSAMPLE else 0.60 if small else 0.50
    draw_ip(art, (0, 0, width, height), round(side * text_ratio), 0 if small else side * 0.012)
    return art


def icon(size: int, fill: float | None = None) -> Image.Image:
    """The app icon: artwork in a rounded tile, with a drop shadow above the tray sizes."""
    small = size <= SMALL
    fill = fill if fill is not None else 0.98 if small else 0.86
    big = size * SUPERSAMPLE
    inset = round(big * (1 - fill) / 2)
    side = big - 2 * inset
    radius = round(side * 0.24)
    canvas = Image.new("RGBA", (big, big), (0, 0, 0, 0))

    if not small:
        shadow = Image.new("RGBA", (big, big), (0, 0, 0, 0))
        offset = big * 0.02
        ImageDraw.Draw(shadow).rounded_rectangle(
            (inset, inset + offset, big - inset - 1, big - inset - 1 + offset), radius, fill=(10, 10, 40, 90))
        canvas.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(big * 0.025)))

    tile = artwork(side, side, small)
    mask = Image.new("L", (side, side), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, side - 1, side - 1), radius, fill=255)
    tile.putalpha(mask)
    canvas.alpha_composite(tile, (inset, inset))
    return canvas.resize((size, size), Image.LANCZOS)


def centered(width: int, height: int, icon_size: int) -> Image.Image:
    img = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    img.alpha_composite(icon(icon_size), ((width - icon_size) // 2, (height - icon_size) // 2))
    return img


def store_box_art(size: int) -> Image.Image:
    """1:1 box art: full-bleed artwork (the Store rounds the corners itself)."""
    big = artwork(size * 2, size * 2, small=False)
    return big.resize((size, size), Image.LANCZOS).convert("RGB")


def store_poster_art(width: int, height: int) -> Image.Image:
    """2:3 poster art: background gradient, the icon, and the app name."""
    poster = gradient(width, height)
    icon_size = round(width * 0.62)
    poster.alpha_composite(icon(icon_size), ((width - icon_size) // 2, round(height * 0.22)))
    draw = ImageDraw.Draw(poster)
    title = font(round(width * 0.085), "Semibold Display")
    for i, line in enumerate(("My Public", "IP Address")):
        left, top, right, bottom = draw.textbbox((0, 0), line, font=title)
        y = round(height * 0.68) + i * round((bottom - top) * 1.35)
        draw.text(((width - (right - left)) / 2 - left, y - top), line, font=title, fill=WHITE)
    return poster.convert("RGB")


def main() -> None:
    ASSETS.mkdir(parents=True, exist_ok=True)

    def save(img: Image.Image, name: str) -> None:
        img.save(ASSETS / name)

    for scale, factor in {100: 1.0, 125: 1.25, 150: 1.5, 200: 2.0, 400: 4.0}.items():
        # App list and taskbar (plated)
        save(icon(round(44 * factor)), f"Square44x44Logo.scale-{scale}.png")
        s150 = round(150 * factor)
        save(centered(s150, s150, round(s150 * 0.62)), f"Square150x150Logo.scale-{scale}.png")
        save(centered(round(310 * factor), round(150 * factor), round(150 * factor * 0.62)), f"Wide310x150Logo.scale-{scale}.png")
        save(icon(round(50 * factor), fill=0.94), f"StoreLogo.scale-{scale}.png")
        # Splash screen: unused by desktop apps, but required by the manifest schema
        save(centered(round(620 * factor), round(300 * factor), round(300 * factor * 0.5)), f"SplashScreen.scale-{scale}.png")

    # Unplated icons used by the taskbar, Start and Explorer
    for size in (16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256):
        img = icon(size)
        save(img, f"Square44x44Logo.targetsize-{size}.png")
        save(img, f"Square44x44Logo.targetsize-{size}_altform-unplated.png")
        save(img, f"Square44x44Logo.targetsize-{size}_altform-lightunplated.png")

    # Tray icon and executable icon
    sizes = [16, 20, 24, 32, 40, 48, 64, 256]
    frames = [icon(s) for s in sizes]
    frames[-1].save(ASSETS / "TrayIcon.ico", format="ICO", sizes=[(s, s) for s in sizes], append_images=frames[:-1])

    icon(256).save(README_LOGO)

    STORE_LOGOS.mkdir(parents=True, exist_ok=True)
    store_box_art(2160).save(STORE_LOGOS / "box-art-2160x2160.png")
    store_poster_art(1440, 2160).save(STORE_LOGOS / "poster-art-1440x2160.png")
    icon(300, fill=0.94).save(STORE_LOGOS / "app-tile-icon-300x300.png")

    print(f"Assets written to {ASSETS}, {README_LOGO.name} and {STORE_LOGOS}")


if __name__ == "__main__":
    main()
