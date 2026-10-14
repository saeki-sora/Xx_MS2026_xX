# Result screen art (same language as the HUD art). Writes into <project>/Assets/_Game/UI/Art/Hud wherever it is run from.
# Needs Python + Pillow (pip install pillow).
import math, os, random
from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "_Game", "UI", "Art", "Hud")
SS = 4
WHITE = (255, 255, 255)


def save(img, name, size):
    img.resize(size, Image.LANCZOS).save(os.path.join(OUT, name))
    print("wrote", name, size)


def stamp():
    """Rank stamp: a hand-pressed ring (white, tinted at runtime). Uneven ink so it reads as a real stamp, not a perfect circle."""
    S = 360 * SS
    c = S / 2
    rnd = random.Random(11)
    img = Image.new("L", (S, S), 0)
    d = ImageDraw.Draw(img)
    # outer thick ring, radius wobbles a little
    pts_out, pts_in = [], []
    for i in range(240):
        a = i / 240 * math.tau
        wob = 1 + 0.012 * math.sin(a * 3 + 0.7) + 0.008 * math.sin(a * 7)
        pts_out.append((c + math.cos(a) * 168 * SS * wob, c + math.sin(a) * 168 * SS * wob))
        pts_in.append((c + math.cos(a) * 142 * SS * wob, c + math.sin(a) * 142 * SS * wob))
    d.polygon(pts_out, fill=255)
    d.polygon(pts_in, fill=0)
    # thin inner ring
    d.ellipse([c - 128 * SS, c - 128 * SS, c + 128 * SS, c + 128 * SS], outline=255, width=5 * SS)
    # ink gaps: knock out small random specks so the print looks pressed by hand
    for _ in range(110):
        a = rnd.random() * math.tau
        r = rnd.uniform(128, 170) * SS
        s = rnd.uniform(1.0, 3.2) * SS
        x, y = c + math.cos(a) * r, c + math.sin(a) * r
        d.ellipse([x - s, y - s, x + s, y + s], fill=0)
    alpha = img.filter(ImageFilter.GaussianBlur(0.6 * SS))
    out = Image.new("RGBA", (S, S), WHITE + (0,))
    out.putalpha(alpha)
    save(out, "Result_Stamp.png", (360, 360))


def crystal():
    """Neutral (white/grey shaded) faceted crystal, tinted with the core colour before / after the match."""
    S = 300 * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    cx, cy = S / 2, S / 2
    top, mid, bot, half = cy - 120 * SS, cy - 34 * SS, cy + 128 * SS, 92 * SS
    outline = (70, 70, 80, 255)
    gem = [(cx, top), (cx + half, mid), (cx, bot), (cx - half, mid)]
    d.polygon([(x + (6 * SS if x > cx else -6 * SS if x < cx else 0), y + (-6 * SS if y < cy else 6 * SS)) for x, y in gem], fill=outline)
    d.polygon([(cx, top), (cx - half, mid), (cx - 10 * SS, mid + 14 * SS)], fill=(255, 255, 255, 255))
    d.polygon([(cx, top), (cx + half, mid), (cx - 10 * SS, mid + 14 * SS)], fill=(225, 225, 230, 255))
    d.polygon([(cx - half, mid), (cx, bot), (cx - 10 * SS, mid + 14 * SS)], fill=(200, 200, 208, 255))
    d.polygon([(cx + half, mid), (cx, bot), (cx - 10 * SS, mid + 14 * SS)], fill=(160, 160, 172, 255))
    # facet edge highlights
    d.line([(cx, top), (cx - 10 * SS, mid + 14 * SS)], fill=(255, 255, 255, 255), width=3 * SS)
    d.line([(cx - half, mid), (cx + half, mid)], fill=(245, 245, 250, 255), width=2 * SS)
    save(img, "Result_Crystal.png", (300, 300))


stamp()
crystal()
