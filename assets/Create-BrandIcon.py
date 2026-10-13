"""Genera le immagini del marchio: assets/PresenzeXmlConverter.ico (icona dell'app, dell'installer e
dei collegamenti) e le immagini della procedura guidata (WizardImage*.bmp, WizardSmallImage*.bmp, a
100%, 150% e 200% di scala: crisp anche sugli schermi ad alta risoluzione).

Uso: python3 assets/Create-BrandIcon.py   (richiede Pillow; per il testo usa il font Inter se presente)

Ogni dimensione ha un disegno suo: a 16-32 px il foglio con le righe di testo diventa
una macchia, quindi le icone piccole usano solo foglio e spunta, grandi e ben leggibili;
da 48 px in su compaiono anche anelli, righe e riflesso.
"""
import io
import struct
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

SIZES = [16, 24, 32, 48, 64, 128, 256]
SUPER = 8  # sovracampionamento per bordi netti


def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def gradient(size, c1, c2):
    """Gradiente diagonale (alto-sinistra -> basso-destra)."""
    img = Image.new("RGBA", (size, size))
    px = img.load()
    for y in range(size):
        for x in range(size):
            t = (x + y) / (2 * (size - 1))
            px[x, y] = lerp(c1, c2, t) + (255,)
    return img


def rounded_mask(size, box, radius):
    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).rounded_rectangle(box, radius=radius, fill=255)
    return mask


def render(px):
    s = px * SUPER
    u = s / 256.0
    detail = px >= 48
    canvas = Image.new("RGBA", (s, s), (0, 0, 0, 0))

    # Fondo: quadrato arrotondato con gradiente blu -> notte.
    bg = gradient(s, (36, 103, 178), (9, 30, 58))
    canvas.paste(bg, (0, 0), rounded_mask(s, (0, 0, s - 1, s - 1), int(s * 0.225)))
    if detail:
        # Riflesso leggero in alto.
        glow = Image.new("RGBA", (s, s), (0, 0, 0, 0))
        ImageDraw.Draw(glow).ellipse((-0.2 * s, -0.55 * s, 1.2 * s, 0.45 * s), fill=(255, 255, 255, 30))
        glow_mask = rounded_mask(s, (0, 0, s - 1, s - 1), int(s * 0.225))
        canvas.alpha_composite(Image.composite(glow, Image.new("RGBA", (s, s), (0, 0, 0, 0)), glow_mask))

    d = ImageDraw.Draw(canvas)

    # Foglio (calendario) con ombra.
    if detail:
        left, top, right, bottom = 44 * u, 40 * u, 200 * u, 206 * u
    else:
        left, top, right, bottom = 36 * u, 34 * u, 208 * u, 214 * u
    shadow = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rounded_rectangle(
        (left + 4 * u, top + 9 * u, right + 4 * u, bottom + 9 * u), radius=16 * u, fill=(0, 12, 30, 110))
    canvas.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(7 * u)))
    d = ImageDraw.Draw(canvas)
    d.rounded_rectangle((left, top, right, bottom), radius=16 * u, fill=(247, 251, 255, 255))
    # Fascia superiore.
    band = 50 * u if detail else 56 * u
    d.rounded_rectangle((left, top, right, top + band), radius=16 * u, fill=(47, 128, 237, 255))
    d.rectangle((left, top + band - 18 * u, right, top + band), fill=(47, 128, 237, 255))

    if detail:
        for cx in (left + 38 * u, right - 38 * u):
            d.rounded_rectangle((cx - 6 * u, top - 12 * u, cx + 6 * u, top + 24 * u), radius=6 * u,
                                fill=(200, 224, 255, 255))
        for i in range(3):
            y = top + (74 + i * 32) * u
            d.rounded_rectangle((left + 18 * u, y, left + 36 * u, y + 18 * u), radius=5 * u, fill=(214, 232, 247, 255))
            d.rounded_rectangle((left + 46 * u, y + 2 * u, left + (118 - i * 14) * u, y + 9 * u), radius=3 * u,
                                fill=(150, 183, 209, 255))
            d.rounded_rectangle((left + 46 * u, y + 12 * u, left + (96 - i * 8) * u, y + 17 * u), radius=2.5 * u,
                                fill=(204, 222, 235, 255))

    # Medaglione verde con spunta.
    r = (52 if detail else 70) * u
    cx, cy = (184 * u, 184 * u) if detail else (166 * u, 160 * u)
    badge_shadow = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    ImageDraw.Draw(badge_shadow).ellipse((cx - r, cy - r + 6 * u, cx + r, cy + r + 6 * u), fill=(0, 20, 20, 120))
    canvas.alpha_composite(badge_shadow.filter(ImageFilter.GaussianBlur(5 * u)))
    badge = gradient(s, (92, 224, 184), (14, 150, 118))
    mask = Image.new("L", (s, s), 0)
    ImageDraw.Draw(mask).ellipse((cx - r, cy - r, cx + r, cy + r), fill=255)
    canvas.paste(badge, (0, 0), mask)
    d = ImageDraw.Draw(canvas)
    d.ellipse((cx - r, cy - r, cx + r, cy + r), outline=(255, 255, 255, 235), width=max(1, int(4 * u)))
    w = int((13 if not detail else 11) * u)
    pts = [(cx - r * 0.48, cy + r * 0.02), (cx - r * 0.12, cy + r * 0.40), (cx + r * 0.52, cy - r * 0.36)]
    d.line(pts, fill=(255, 255, 255, 255), width=w, joint="curve")
    for p in (pts[0], pts[2]):
        d.ellipse((p[0] - w / 2, p[1] - w / 2, p[0] + w / 2, p[1] + w / 2), fill=(255, 255, 255, 255))

    return canvas.resize((px, px), Image.LANCZOS)


def write_ico(path, images):
    entries, blobs = [], []
    offset = 6 + 16 * len(images)
    for img in images:
        buf = io.BytesIO()
        img.save(buf, format="PNG", optimize=True)
        data = buf.getvalue()
        w = img.width
        entries.append(struct.pack("<BBBBHHII", 0 if w >= 256 else w, 0 if w >= 256 else w, 0, 0, 1, 32, len(data), offset))
        blobs.append(data)
        offset += len(data)
    Path(path).write_bytes(struct.pack("<HHH", 0, 1, len(images)) + b"".join(entries) + b"".join(blobs))


APP_NAME = "Vyrn Presence"
TAGLINE = "Da Excel a PAGHE"
FONT_DIRS = ["/usr/share/fonts/opentype/inter", "/usr/share/fonts/truetype/dejavu", "C:/Windows/Fonts"]


def font(size, bold=False):
    from PIL import ImageFont
    names = ["Inter-Bold.otf", "DejaVuSans-Bold.ttf", "segoeuib.ttf"] if bold else ["Inter-Regular.otf", "DejaVuSans.ttf", "segoeui.ttf"]
    for directory in FONT_DIRS:
        for name in names:
            candidate = Path(directory) / name
            if candidate.exists():
                return ImageFont.truetype(str(candidate), size)
    return ImageFont.load_default()


def wizard_image(scale):
    """Pannello laterale della procedura guidata (164x314 a 100%)."""
    w, h = 164 * scale, 314 * scale
    img = Image.new("RGB", (w, h))
    px = img.load()
    top, bottom = (27, 88, 160), (8, 27, 52)
    for y in range(h):
        t = y / (h - 1)
        row = lerp(top, bottom, t)
        for x in range(w):
            px[x, y] = row
    d = ImageDraw.Draw(img)
    icon = render(256).resize((96 * scale, 96 * scale), Image.LANCZOS)
    img.paste(icon, ((w - icon.width) // 2, 52 * scale), icon)
    name_font, tag_font, foot_font = font(19 * scale, True), font(11 * scale), font(8 * scale)
    d.text((w / 2, 168 * scale), "Vyrn", font=name_font, fill=(255, 255, 255), anchor="mm")
    d.text((w / 2, 190 * scale), "Presence", font=name_font, fill=(255, 255, 255), anchor="mm")
    d.line((w * 0.3, 208 * scale, w * 0.7, 208 * scale), fill=(92, 224, 184), width=max(1, scale * 2))
    d.text((w / 2, 224 * scale), TAGLINE, font=tag_font, fill=(190, 218, 245), anchor="mm")
    d.text((w / 2, h - 20 * scale), "Conversione presenze", font=foot_font, fill=(120, 160, 205), anchor="mm")
    d.text((w / 2, h - 10 * scale), "XML TRRIPW", font=foot_font, fill=(120, 160, 205), anchor="mm")
    return img


def wizard_small_image(scale):
    """Icona nell'angolo delle pagine interne (55x55 a 100%), su fondo bianco."""
    size = 55 * scale
    img = Image.new("RGB", (size, size), (255, 255, 255))
    icon = render(256).resize((size - 6 * scale, size - 6 * scale), Image.LANCZOS)
    img.paste(icon, (3 * scale, 3 * scale), icon)
    return img


if __name__ == "__main__":
    root = Path(__file__).resolve().parent
    images = [render(size) for size in SIZES]
    write_ico(root / "PresenzeXmlConverter.ico", images)
    for scale, suffix in ((1, ""), (1.5, "-150"), (2, "-200")):
        # La scala frazionaria si ottiene rendendo a 3x e riducendo.
        big = wizard_image(3)
        small = wizard_small_image(3)
        big.resize((int(164 * scale), int(314 * scale)), Image.LANCZOS).save(root / f"WizardImage{suffix}.bmp")
        small.resize((int(55 * scale), int(55 * scale)), Image.LANCZOS).save(root / f"WizardSmallImage{suffix}.bmp")
    print("Icona scritta:", ", ".join(str(i.width) for i in images))
