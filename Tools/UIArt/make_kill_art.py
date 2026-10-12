# Kill counter HUD art (pastel cyber / toothpaste world). Writes PNGs into Assets/_Game/UI/Art/Hud/.
import math, os, random
from PIL import Image, ImageDraw, ImageFilter, ImageChops

# Writes into <project>/Assets/_Game/UI/Art/Hud wherever it is run from (needs Python + Pillow: pip install pillow).
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "_Game", "UI", "Art", "Hud")
os.makedirs(OUT, exist_ok=True)
SS = 4  # supersampling

CYAN = (110, 236, 255)
PINK = (255, 128, 214)
LAV = (178, 156, 255)
NAVY = (16, 20, 52)
WHITE = (255, 255, 255)


def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(len(a)))


def save(img, name, size):
    img = img.resize(size, Image.LANCZOS)
    img.save(os.path.join(OUT, name))
    print("wrote", name, size)


def para(w, h, skew, inset=0):
    """parallelogram points (leaning right like '/')"""
    s = skew
    return [(s + inset, inset), (w - inset, inset), (w - s - inset, h - inset), (inset, h - inset)]


def gradient_h(w, h, c0, c1):
    g = Image.new("RGBA", (w, h))
    px = g.load()
    for x in range(w):
        c = lerp(c0, c1, x / max(1, w - 1))
        for y in range(h):
            px[x, y] = c
    return g


# ---------- 1. plate ----------
def plate():
    W, H = 900 * SS, 176 * SS
    skew = 44 * SS
    base = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    # outer border (gradient cyan -> lavender -> pink)
    mask_outer = Image.new("L", (W, H), 0)
    ImageDraw.Draw(mask_outer).polygon(para(W, H, skew), fill=255)
    border_grad = Image.new("RGBA", (W, H))
    bpx = border_grad.load()
    for x in range(W):
        t = x / (W - 1)
        c = lerp(CYAN, LAV, t * 2) if t < 0.5 else lerp(LAV, PINK, (t - 0.5) * 2)
        for y in range(H):
            bpx[x, y] = c + (255,)
    base.paste(border_grad, (0, 0), mask_outer)
    # inner fill: navy, slightly lighter at the top, translucent
    b = 6 * SS
    mask_inner = Image.new("L", (W, H), 0)
    ImageDraw.Draw(mask_inner).polygon(para(W, H, skew, b), fill=255)
    fill = Image.new("RGBA", (W, H))
    fpx = fill.load()
    for y in range(H):
        t = y / (H - 1)
        c = lerp((44, 40, 96), NAVY, t)
        a = int(228 - 20 * t)
        for x in range(W):
            fpx[x, y] = c + (a,)
    # diagonal stripes, very faint
    stripes = Image.new("L", (W, H), 0)
    sd = ImageDraw.Draw(stripes)
    step = 26 * SS
    for x0 in range(-H, W + H, step):
        sd.polygon([(x0, H), (x0 + 10 * SS, H), (x0 + 10 * SS + H, 0), (x0 + H, 0)], fill=22)
    fill_rgb = fill.copy()
    light = Image.new("RGBA", (W, H), (150, 200, 255, 255))
    fill = Image.composite(Image.blend(fill_rgb, light, 0.18), fill_rgb, stripes)
    base.paste(fill, (0, 0), mask_inner)
    # inner hairline highlight
    hl = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(hl).line(para(W, H, skew, b + 5 * SS)[:2], fill=(255, 255, 255, 90), width=2 * SS)
    base = Image.alpha_composite(base, hl)
    # cut corner accents (small bright ticks top-left / bottom-right)
    acc = ImageDraw.Draw(base)
    acc.polygon([(skew + b + 12 * SS, b + 10 * SS), (skew + b + 70 * SS, b + 10 * SS), (skew + b + 64 * SS, b + 16 * SS), (skew + b + 6 * SS, b + 16 * SS)], fill=CYAN + (255,))
    acc.polygon([(W - skew - b - 70 * SS, H - b - 16 * SS), (W - skew - b - 12 * SS, H - b - 16 * SS), (W - skew - b - 18 * SS, H - b - 10 * SS), (W - skew - b - 76 * SS, H - b - 10 * SS)], fill=PINK + (255,))
    # soft outer glow
    glow = mask_outer.filter(ImageFilter.GaussianBlur(10 * SS))
    glow_img = Image.new("RGBA", (W, H), (140, 220, 255, 0))
    glow_img.putalpha(glow.point(lambda v: int(v * 0.35)))
    out = Image.alpha_composite(glow_img, base)
    save(out, "Kill_Plate.png", (900, 176))


# ---------- 2. you tag (white, tinted by player colour) ----------
def you_tag():
    W, H = 280 * SS, 72 * SS
    skew = 22 * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    mask = Image.new("L", (W, H), 0)
    ImageDraw.Draw(mask).polygon(para(W, H, skew), fill=255)
    grad = Image.new("RGBA", (W, H))
    gp = grad.load()
    for y in range(H):
        c = lerp((255, 255, 255), (214, 214, 226), y / (H - 1))
        for x in range(W):
            gp[x, y] = c + (255,)
    img.paste(grad, (0, 0), mask)
    d = ImageDraw.Draw(img)
    d.line(para(W, H, skew, 5 * SS)[:2], fill=(255, 255, 255, 255), width=3 * SS)
    save(img, "Kill_YouTag.png", (280, 72))


# ---------- 3. germ icon with laser slash ----------
def germ_icon():
    S = 180 * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    cx, cy, r = S / 2, S / 2 + 4 * SS, 50 * SS
    body = (168, 108, 255)
    dark = (70, 34, 140)
    # spikes
    for i in range(10):
        a = i / 10 * math.tau + 0.2
        x1, y1 = cx + math.cos(a) * (r + 2 * SS), cy + math.sin(a) * (r + 2 * SS)
        x2, y2 = cx + math.cos(a) * (r + 26 * SS), cy + math.sin(a) * (r + 26 * SS)
        d.line([(x1, y1), (x2, y2)], fill=dark + (255,), width=12 * SS)
        d.ellipse([x2 - 11 * SS, y2 - 11 * SS, x2 + 11 * SS, y2 + 11 * SS], fill=dark + (255,))
        d.line([(x1, y1), (x2, y2)], fill=body + (255,), width=6 * SS)
        d.ellipse([x2 - 7 * SS, y2 - 7 * SS, x2 + 7 * SS, y2 + 7 * SS], fill=PINK + (255,))
    # body with outline + shading
    d.ellipse([cx - r - 6 * SS, cy - r - 6 * SS, cx + r + 6 * SS, cy + r + 6 * SS], fill=dark + (255,))
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=body + (255,))
    d.ellipse([cx - r * 0.75, cy - r * 0.85, cx + r * 0.2, cy - r * 0.15], fill=(206, 170, 255, 255))
    # X eyes
    for ex in (-18, 18):
        x, y = cx + ex * SS, cy - 4 * SS
        k = 8 * SS
        d.line([(x - k, y - k), (x + k, y + k)], fill=NAVY + (255,), width=5 * SS)
        d.line([(x - k, y + k), (x + k, y - k)], fill=NAVY + (255,), width=5 * SS)
    # wobbly mouth
    d.arc([cx - 16 * SS, cy + 10 * SS, cx + 16 * SS, cy + 30 * SS], 200, 340, fill=NAVY + (255,), width=4 * SS)
    # laser slash (cyan with white core), with RGB offset ghost
    slash = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    sd = ImageDraw.Draw(slash)
    p0, p1 = (14 * SS, S - 30 * SS), (S - 14 * SS, 26 * SS)
    sd.line([p0, p1], fill=PINK + (150,), width=20 * SS)
    slash = slash.transform(slash.size, Image.AFFINE, (1, 0, 5 * SS, 0, 1, 0))
    sd = ImageDraw.Draw(slash)
    sd.line([p0, p1], fill=CYAN + (255,), width=16 * SS)
    sd.line([p0, p1], fill=WHITE + (255,), width=6 * SS)
    glow = slash.filter(ImageFilter.GaussianBlur(6 * SS))
    img = Image.alpha_composite(img, glow)
    img = Image.alpha_composite(img, slash)
    save(img, "Kill_GermIcon.png", (180, 180))


# ---------- 4. rounded bar (white, 9-slice) ----------
def bar():
    W, H = 64 * SS, 24 * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(img).rounded_rectangle([0, 0, W - 1, H - 1], radius=11 * SS, fill=WHITE + (255,))
    save(img, "Kill_Bar.png", (64, 24))


# ---------- 5. burst (white rays, tinted at runtime) ----------
def burst():
    S = 512 * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c = S / 2
    rays = 18
    rnd = random.Random(7)
    for i in range(rays):
        a = i / rays * math.tau
        w = math.tau / rays * (0.28 + 0.12 * rnd.random())
        L = S * (0.42 + 0.08 * rnd.random())
        d.polygon([(c, c), (c + math.cos(a - w) * L, c + math.sin(a - w) * L), (c + math.cos(a + w) * L, c + math.sin(a + w) * L)], fill=WHITE + (255,))
    # radial alpha falloff
    alpha = Image.new("L", (S, S), 0)
    ap = alpha.load()
    for y in range(0, S, SS):
        for x in range(0, S, SS):
            dist = math.hypot(x - c, y - c) / (S * 0.5)
            v = int(max(0.0, 1.0 - dist) ** 1.4 * 255)
            for yy in range(SS):
                for xx in range(SS):
                    ap[x + xx, y + yy] = v
    img.putalpha(ImageChops.multiply(img.getchannel("A"), alpha))
    save(img, "Kill_Burst.png", (512, 512))


# ---------- 6. glow ring ----------
def ring():
    S = 256 * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    m = 30 * SS
    d.ellipse([m, m, S - m, S - m], outline=WHITE + (255,), width=10 * SS)
    glow = img.filter(ImageFilter.GaussianBlur(9 * SS))
    img = Image.alpha_composite(glow, img)
    save(img, "Kill_Ring.png", (256, 256))


plate()
you_tag()
germ_icon()
bar()
burst()
ring()
