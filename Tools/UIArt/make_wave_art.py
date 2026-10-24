# Wave / brushing-time HUD art (2026-10-25). Writes PNGs into Assets/_Game/UI/Art/Hud/.
# Look follows the project's own art (title logo, character select, HUD bar/frames) and the team's concept art:
# pastel cyan / pink / lavender, thick white + navy outlines, a slight magenta/cyan colour split (glitch),
# 4-point sparkles, soap bubbles, and a faint "0101" digital texture. No text is baked in (Unity draws text).
# Overwrite any PNG with the same name to reskin; the HUD builder only refers to file names.
# Needs Python + Pillow: pip install pillow
import math, os, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageChops

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "_Game", "UI", "Art", "Hud")
os.makedirs(OUT, exist_ok=True)
SS = 4
random.seed(20261025)

CYAN = (110, 236, 255)
SKY = (150, 215, 255)
PINK = (255, 128, 214)
HOT = (255, 70, 170)
LAV = (178, 156, 255)
NAVY = (22, 26, 78)
WHITE = (255, 255, 255)
MAGENTA = (255, 40, 200)
GLITCH_CYAN = (0, 220, 255)


# ───────── helpers ─────────

def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(len(a)))


def ramp(stops, t):
    t = min(1.0, max(0.0, t))
    for i in range(len(stops) - 1):
        t0, c0 = stops[i]
        t1, c1 = stops[i + 1]
        if t <= t1:
            return lerp(c0, c1, (t - t0) / max(1e-6, t1 - t0))
    return stops[-1][1]


def colorize(t, stops):
    """t: float array 0..1 -> RGBA image using colour stops (numpy, fast)."""
    t = np.clip(t, 0.0, 1.0)
    xs = [s[0] for s in stops]
    rgb = np.stack([np.interp(t, xs, [s[1][c] for s in stops]) for c in range(3)], axis=-1)
    a = np.full(t.shape + (1,), 255.0)
    return Image.fromarray(np.concatenate([rgb, a], axis=-1).astype(np.uint8), "RGBA")


def gradient(w, h, stops, angle_deg=0.0):
    """linear gradient along angle (0 = left->right, 90 = top->bottom)."""
    a = math.radians(angle_deg)
    dx, dy = math.cos(a), math.sin(a)
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    span = abs(dx) * w + abs(dy) * h
    ox = 0 if dx >= 0 else w
    oy = 0 if dy >= 0 else h
    return colorize(np.abs(((xs - ox) * dx + (ys - oy) * dy) / span), stops)


def radial(w, h, stops, cx=0.5, cy=0.5):
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    r = math.hypot(max(cx, 1 - cx) * w, max(cy, 1 - cy) * h)
    return colorize(np.hypot(xs - cx * w, ys - cy * h) / r, stops)


def angular(w, h, stops):
    """colour around the centre (0 at the top, clockwise)."""
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    a = (np.arctan2(xs - w / 2, -(ys - h / 2)) / (2 * math.pi)) % 1.0
    return colorize(a, stops)


def fill(mask, color_or_img):
    """paint a mask with a colour or an image."""
    if isinstance(color_or_img, Image.Image):
        layer = color_or_img.copy()
    else:
        layer = Image.new("RGBA", mask.size, tuple(color_or_img[:3]) + (255,))
    alpha = layer.getchannel("A")
    layer.putalpha(ImageChops.multiply(alpha, mask))
    return layer


def grow(mask, px):
    return mask.filter(ImageFilter.MaxFilter(px * 2 + 1)) if px > 0 else mask


def stack(*layers):
    base = Image.new("RGBA", layers[0].size, (0, 0, 0, 0))
    for layer in layers:
        base.alpha_composite(layer)
    return base


def glitch(img, dx):
    """magenta copy shifted left and cyan copy shifted right under the image (the project's colour split)."""
    a = img.getchannel("A")
    left = Image.new("RGBA", img.size, (0, 0, 0, 0))
    right = Image.new("RGBA", img.size, (0, 0, 0, 0))
    m = fill(a, MAGENTA)
    c = fill(a, GLITCH_CYAN)
    left.alpha_composite(m, (-dx, 0)) if dx < img.width else None
    right.alpha_composite(c, (0, 0))
    shifted_c = Image.new("RGBA", img.size, (0, 0, 0, 0))
    shifted_c.paste(c, (dx, 0), c)
    shifted_m = Image.new("RGBA", img.size, (0, 0, 0, 0))
    shifted_m.paste(m, (-dx, 0), m)
    for layer in (shifted_m, shifted_c):
        layer.putalpha(layer.getchannel("A").point(lambda v: int(v * 0.75)))
    return stack(shifted_m, shifted_c, img)


def glow(mask, radius, color, strength=1.0):
    g = fill(grow(mask, radius // 3), color)
    g = g.filter(ImageFilter.GaussianBlur(radius))
    g.putalpha(g.getchannel("A").point(lambda v: int(min(255, v * strength))))
    return g


def sparkle(draw, cx, cy, r, color, thin=0.22):
    pts = []
    for k in range(8):
        ang = k * math.pi / 4 - math.pi / 2
        rr = r if k % 2 == 0 else r * thin
        pts.append((cx + math.cos(ang) * rr, cy + math.sin(ang) * rr))
    draw.polygon(pts, fill=color)


def bubble(draw, cx, cy, r, alpha=230):
    draw.ellipse([cx - r, cy - r, cx + r, cy + r], outline=WHITE + (alpha,), width=max(2, int(r * 0.16)))
    hr = r * 0.32
    draw.ellipse([cx - r * 0.55 - hr * 0.5, cy - r * 0.55 - hr * 0.5, cx - r * 0.55 + hr * 0.5, cy - r * 0.55 + hr * 0.5], fill=WHITE + (alpha,))


def digits(w, h, alpha=40, size=10):
    """faint 0/1 digital texture (drawn with rectangles, no font)."""
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    cw, ch = size * SS, int(size * 1.6 * SS)
    for y in range(0, h, int(ch * 1.4)):
        for x in range(random.randint(0, cw), w, int(cw * 1.5)):
            if random.random() < 0.45:
                continue
            col = WHITE + (alpha,)
            if random.random() < 0.5:  # "1"
                d.rectangle([x + cw * 0.4, y, x + cw * 0.6, y + ch], fill=col)
            else:  # "0"
                d.rounded_rectangle([x, y, x + cw, y + ch], radius=cw // 2, outline=col, width=max(1, cw // 5))
    return img


def save(img, name, size):
    img = img.resize(size, Image.LANCZOS)
    img.save(os.path.join(OUT, name))
    print("wrote", name, size)


def poly_mask(w, h, pts):
    m = Image.new("L", (w, h), 0)
    ImageDraw.Draw(m).polygon(pts, fill=255)
    return m


def rrect_mask(w, h, box, r):
    m = Image.new("L", (w, h), 0)
    ImageDraw.Draw(m).rounded_rectangle(box, radius=r, fill=255)
    return m


# ───────── ribbon / banner (title of the brushing time, WAVE n announce, WAVE CLEAR) ─────────

def ribbon(name, size, stops, pad=26):
    W, H = size[0] * SS, size[1] * SS
    p = pad * SS
    skew = 60 * SS
    body = poly_mask(W, H, [(p + skew, p), (W - p, p), (W - p - skew, H - p), (p, H - p)])
    navy = fill(grow(body, 9 * SS), NAVY)
    white = fill(grow(body, 5 * SS), WHITE)
    grad = fill(body, gradient(W, H, stops, 0))
    # light band on the upper half, digital texture, speed lines
    shine = fill(poly_mask(W, H, [(p + skew, p), (W - p, p), (W - p - skew * 0.45, H * 0.46), (p + skew * 0.55, H * 0.46)]), (255, 255, 255))
    shine.putalpha(shine.getchannel("A").point(lambda v: int(v * 0.22)))
    tex = fill(body, digits(W, H, 34, 9))
    lines = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(lines)
    for i in range(10):
        y = random.randint(p + 20 * SS, H - p - 20 * SS)
        x0 = random.randint(p, int(W * 0.22))
        d.line([(x0, y), (x0 + random.randint(60, 160) * SS, y)], fill=WHITE + (110,), width=3 * SS)
        x1 = random.randint(int(W * 0.7), W - p - 160 * SS)
        d.line([(x1, y), (x1 + random.randint(60, 160) * SS, y)], fill=WHITE + (110,), width=3 * SS)
    lines = fill(body, lines)
    core = stack(navy, white, grad, shine, tex, lines)
    img = glitch(core, 7 * SS)
    d = ImageDraw.Draw(img)
    for (cx, cy, r) in [(p + skew * 0.4, p + 6 * SS, 26), (W - p - 30 * SS, H - p - 4 * SS, 22), (W - p - 70 * SS, p + 2 * SS, 14), (p + 70 * SS, H - p, 12)]:
        sparkle(d, cx, cy, r * SS, WHITE + (255,))
    save(stack(glow(body, 18 * SS, PINK, 0.6), img), name, size)


# ───────── frosted panels ─────────

def glass_panel(name, size, radius, border_stops, glow_color=None, notch=False):
    """white frosted panel with a gradient border (cards, guide, chips). Drawn so it can also be 9-sliced."""
    W, H = size[0] * SS, size[1] * SS
    p = 10 * SS
    r = radius * SS
    outer = rrect_mask(W, H, [p, p, W - p, H - p], r)
    inner = rrect_mask(W, H, [p + 7 * SS, p + 7 * SS, W - p - 7 * SS, H - p - 7 * SS], max(1, r - 7 * SS))
    border = fill(outer, gradient(W, H, border_stops, 20))
    navy = fill(grow(outer, 3 * SS), NAVY)
    face = fill(inner, gradient(W, H, [(0, (255, 255, 255)), (1, (232, 244, 255))], 90))
    face.putalpha(face.getchannel("A").point(lambda v: int(v * 0.94)))
    tex = fill(inner, digits(W, H, 18, 7))
    img = stack(navy, border, face, tex)
    if glow_color:
        img = stack(glow(outer, 14 * SS, glow_color, 0.5), img)
    save(img, name, size)


def navy_plate(name, size, radius, border_stops):
    """dark plate like the HUD's core bar frame (WAVE / あと / TIME)."""
    W, H = size[0] * SS, size[1] * SS
    p = 12 * SS
    n = radius * SS
    pts = [(p + n, p), (W - p, p), (W - p, H - p - n), (W - p - n, H - p), (p, H - p), (p, p + n)]
    outer = poly_mask(W, H, pts)
    inner = poly_mask(W, H, [(x + (6 * SS if x < W / 2 else -6 * SS), y + (6 * SS if y < H / 2 else -6 * SS)) for x, y in pts])
    border = fill(outer, gradient(W, H, border_stops, 0))
    face = fill(inner, gradient(W, H, [(0, (34, 40, 108)), (1, (18, 20, 62))], 90))
    face.putalpha(face.getchannel("A").point(lambda v: int(v * 0.93)))
    tex = fill(inner, digits(W, H, 12, 9))
    img = glitch(stack(fill(grow(outer, 3 * SS), WHITE), border, face, tex), 4 * SS)
    d = ImageDraw.Draw(img)
    for k in (1, 2):
        y = int(p + (H - 2 * p) * k / 3)
        d.line([(p + 26 * SS, y), (W - p - 26 * SS, y)], fill=CYAN + (90,), width=2 * SS)
    sparkle(d, W - p - 16 * SS, p + 18 * SS, 12 * SS, WHITE + (255,))
    save(img, name, size)


# ───────── tooth, germs are the title's own germ (Title_Germ.png) ─────────

def tooth_mask(W, H):
    m = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(m)
    # crown (two bumps) + two roots, soft
    d.ellipse([W * 0.06, H * 0.02, W * 0.56, H * 0.46], fill=255)
    d.ellipse([W * 0.44, H * 0.02, W * 0.94, H * 0.46], fill=255)
    d.rounded_rectangle([W * 0.08, H * 0.2, W * 0.92, H * 0.6], radius=int(W * 0.18), fill=255)
    d.polygon([(W * 0.1, H * 0.5), (W * 0.48, H * 0.55), (W * 0.36, H * 0.97), (W * 0.2, H * 0.97)], fill=255)
    d.polygon([(W * 0.52, H * 0.55), (W * 0.9, H * 0.5), (W * 0.8, H * 0.97), (W * 0.64, H * 0.97)], fill=255)
    d.ellipse([W * 0.18, H * 0.86, W * 0.38, H * 0.99], fill=255)
    d.ellipse([W * 0.62, H * 0.86, W * 0.82, H * 0.99], fill=255)
    # the gap between the roots
    d.ellipse([W * 0.4, H * 0.62, W * 0.6, H * 0.86], fill=0)
    return m.filter(ImageFilter.GaussianBlur(6 * SS)).point(lambda v: 255 if v > 128 else 0)


def tooth(name, size):
    W, H = size[0] * SS, size[1] * SS
    pad = 26 * SS
    m = Image.new("L", (W, H), 0)
    m.paste(tooth_mask(W - pad * 2, H - pad * 2), (pad, pad))
    navy = fill(grow(m, 10 * SS), NAVY)
    lav = fill(grow(m, 6 * SS), LAV)
    body = fill(m, gradient(W, H, [(0, (255, 255, 255)), (0.55, (250, 252, 255)), (1, (214, 240, 255))], 90))
    # cyan shade along the right edge (inner shadow), small curved highlight on the left crown
    edge = ImageChops.subtract(m, m.filter(ImageFilter.MinFilter(41)).filter(ImageFilter.GaussianBlur(10 * SS)))
    right = Image.fromarray((np.clip((np.mgrid[0:H, 0:W][1] / W - 0.35) * 1.6, 0, 1) * 255).astype(np.uint8), "L")
    shade = fill(ImageChops.multiply(edge, right), (130, 205, 255))
    shade.putalpha(shade.getchannel("A").point(lambda v: int(v * 0.8)))
    hl = Image.new("L", (W, H), 0)
    ImageDraw.Draw(hl).arc([W * 0.16, H * 0.1, W * 0.5, H * 0.5], 190, 250, fill=255, width=int(W * 0.045))
    hl = fill(ImageChops.multiply(hl.filter(ImageFilter.GaussianBlur(2 * SS)), m), WHITE)
    sp = Image.new("L", (W, H), 0)
    sparkle(ImageDraw.Draw(sp), W * 0.74, H * 0.2, W * 0.07, 255)
    hl = stack(hl, fill(sp, WHITE))
    img = glitch(stack(navy, lav, body, shade, hl), 5 * SS)
    save(stack(glow(m, 22 * SS, CYAN, 0.55), img), name, size)


# ───────── round stage behind the tooth ─────────

def stage(name, size):
    W, H = size[0] * SS, size[1] * SS
    p = 24 * SS
    m = Image.new("L", (W, H), 0)
    ImageDraw.Draw(m).ellipse([p, p, W - p, H - p], fill=255)
    inner = Image.new("L", (W, H), 0)
    ImageDraw.Draw(inner).ellipse([p + 12 * SS, p + 12 * SS, W - p - 12 * SS, H - p - 12 * SS], fill=255)
    ring = fill(m, gradient(W, H, [(0, CYAN), (0.5, LAV), (1, PINK)], 45))
    face = fill(inner, radial(W, H, [(0, (255, 255, 255)), (0.55, (226, 246, 255)), (1, (176, 222, 255))]))
    face.putalpha(face.getchannel("A").point(lambda v: int(v * 0.92)))
    tex = fill(inner, digits(W, H, 26, 9))
    img = stack(glow(m, 26 * SS, SKY, 0.7), fill(grow(m, 4 * SS), NAVY), ring, face, tex)
    d = ImageDraw.Draw(img)
    for k in range(14):
        a = random.uniform(0, math.tau)
        rr = random.uniform(0.36, 0.47) * W
        bubble(d, W / 2 + math.cos(a) * rr, H / 2 + math.sin(a) * rr, random.uniform(8, 22) * SS, 210)
    for (fx, fy, r) in [(0.18, 0.2, 26), (0.86, 0.3, 18), (0.8, 0.84, 24), (0.12, 0.76, 14)]:
        sparkle(d, W * fx, H * fy, r * SS, WHITE + (255,))
    save(img, name, size)


# ───────── toothbrush (handle tinted with the player colour, head stays white) ─────────

def brush(size):
    W, H = size[0] * SS, size[1] * SS
    handle = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(handle)
    d.rounded_rectangle([W * 0.03, H * 0.56, W * 0.7, H * 0.82], radius=int(H * 0.13), fill=255)
    d.polygon([(W * 0.62, H * 0.56), (W * 0.72, H * 0.6), (W * 0.72, H * 0.78), (W * 0.62, H * 0.82)], fill=255)
    d.rounded_rectangle([W * 0.68, H * 0.58, W * 0.97, H * 0.8], radius=int(H * 0.1), fill=255)
    tuft = Image.new("L", (W, H), 0)
    ImageDraw.Draw(tuft).rounded_rectangle([W * 0.7, H * 0.16, W * 0.95, H * 0.6], radius=int(H * 0.08), fill=255)
    silhouette = ImageChops.lighter(handle, tuft)
    outline = fill(grow(silhouette, 6 * SS), NAVY)
    handle_img = fill(handle, gradient(W, H, [(0, (255, 255, 255)), (1, (226, 232, 255))], 90))
    tuft_img = fill(tuft, gradient(W, H, [(0, (255, 255, 255)), (1, (170, 232, 255))], 90))
    lines = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ld = ImageDraw.Draw(lines)
    for i in range(1, 6):
        x = W * (0.7 + i * 0.25 / 6)
        ld.line([(x, H * 0.2), (x, H * 0.56)], fill=(120, 190, 240, 200), width=2 * SS)
    lines = fill(tuft, lines)
    foam = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    fd = ImageDraw.Draw(foam)
    for (fx, fy, r) in [(0.74, 0.14, 0.06), (0.83, 0.1, 0.075), (0.92, 0.15, 0.055)]:
        fd.ellipse([W * fx - H * r, H * fy - H * r, W * fx + H * r, H * fy + H * r], fill=WHITE + (255,), outline=NAVY + (255,), width=2 * SS)
    save(stack(outline, handle_img, tuft_img, lines, foam), "Brush_Brush.png", size)
    stripe = Image.new("L", (W, H), 0)
    ImageDraw.Draw(stripe).rounded_rectangle([W * 0.08, H * 0.62, W * 0.6, H * 0.76], radius=int(H * 0.07), fill=255)
    save(fill(stripe, WHITE), "Brush_BrushStripe.png", size)


# ───────── grip guide hand (open | closed) ─────────

def hand(size):
    W, H = size[0] * SS, size[1] * SS
    frame = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    half = W // 2

    def one(ox, closed):
        m = Image.new("L", (W, H), 0)
        d = ImageDraw.Draw(m)
        cx = ox + half * 0.5
        palm = [cx - half * 0.26, H * 0.42, cx + half * 0.26, H * 0.86]
        d.rounded_rectangle(palm, radius=int(half * 0.16), fill=255)
        for i in range(4):
            fx = cx - half * 0.24 + i * half * 0.125
            if closed:
                d.rounded_rectangle([fx, H * 0.33, fx + half * 0.115, H * 0.56], radius=int(half * 0.06), fill=255)
            else:
                top = H * (0.08 + (0.04 if i in (0, 3) else 0))
                d.rounded_rectangle([fx, top, fx + half * 0.115, H * 0.56], radius=int(half * 0.06), fill=255)
        if closed:
            d.rounded_rectangle([cx - half * 0.4, H * 0.5, cx - half * 0.06, H * 0.64], radius=int(half * 0.07), fill=255)
        else:
            d.rounded_rectangle([cx - half * 0.46, H * 0.4, cx - half * 0.18, H * 0.56], radius=int(half * 0.07), fill=255)
        cuff = Image.new("L", (W, H), 0)
        ImageDraw.Draw(cuff).rounded_rectangle([cx - half * 0.28, H * 0.82, cx + half * 0.28, H * 0.97], radius=int(half * 0.06), fill=255)
        return m, cuff

    for ox, closed in ((0, False), (half, True)):
        m, cuff = one(ox, closed)
        both = ImageChops.lighter(m, cuff)
        layer = stack(fill(grow(both, 7 * SS), NAVY), fill(m, gradient(W, H, [(0, WHITE), (1, (220, 240, 255))], 90)), fill(cuff, gradient(W, H, [(0, PINK), (1, LAV)], 0)))
        frame.alpha_composite(layer)
        if closed:
            d = ImageDraw.Draw(frame)
            cx = half * 1.5
            for k in (-1, 1):  # motion marks
                d.line([(cx + k * half * 0.4, H * 0.2), (cx + k * half * 0.47, H * 0.12)], fill=HOT + (255,), width=6 * SS)
                d.line([(cx + k * half * 0.44, H * 0.3), (cx + k * half * 0.54, H * 0.26)], fill=HOT + (255,), width=6 * SS)
    save(glitch(frame, 3 * SS), "Brush_Hand.png", size)


# ───────── timer ring (track + radial fill) ─────────

def timer(size):
    W, H = size[0] * SS, size[1] * SS
    p = 16 * SS
    disk = Image.new("L", (W, H), 0)
    ImageDraw.Draw(disk).ellipse([p, p, W - p, H - p], fill=255)
    ring = Image.new("L", (W, H), 0)
    rd = ImageDraw.Draw(ring)
    rd.ellipse([p + 10 * SS, p + 10 * SS, W - p - 10 * SS, H - p - 10 * SS], fill=255)
    rd.ellipse([p + 44 * SS, p + 44 * SS, W - p - 44 * SS, H - p - 44 * SS], fill=0)
    back = stack(glow(disk, 14 * SS, PINK, 0.4), fill(grow(disk, 4 * SS), WHITE), fill(disk, radial(W, H, [(0, (40, 46, 120)), (1, (18, 20, 62))])),
                 fill(ring, (60, 66, 140)))
    save(glitch(back, 4 * SS), "Brush_TimerBack.png", size)
    # radial fill: colour around the circle (pink at the top going round to cyan), Unity fills it Radial360
    save(fill(ring, angular(W, H, [(0, HOT), (0.5, LAV), (1, CYAN)])), "Brush_TimerFill.png", size)


# ───────── sparkle burst, bubble, sparkle ─────────

def burst(size):
    W, H = size[0] * SS, size[1] * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    cx, cy = W / 2, H / 2
    for k in range(18):
        a = k / 18 * math.tau
        w = 0.07
        col = [PINK, CYAN, LAV][k % 3] + (150,)
        r = W * (0.5 if k % 2 == 0 else 0.4)
        d.polygon([(cx, cy), (cx + math.cos(a - w) * r, cy + math.sin(a - w) * r), (cx + math.cos(a + w) * r, cy + math.sin(a + w) * r)], fill=col)
    img = img.filter(ImageFilter.GaussianBlur(3 * SS))
    d = ImageDraw.Draw(img)
    for k in range(10):
        a = random.uniform(0, math.tau)
        rr = random.uniform(0.18, 0.44) * W
        sparkle(d, cx + math.cos(a) * rr, cy + math.sin(a) * rr, random.uniform(10, 26) * SS, WHITE + (255,))
    save(img, "Brush_Burst.png", size)


def small_bits():
    S = 128 * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    r = S * 0.4
    d.ellipse([S / 2 - r, S / 2 - r, S / 2 + r, S / 2 + r], fill=(220, 245, 255, 70), outline=WHITE + (240,), width=int(S * 0.06))
    d.arc([S / 2 - r * 0.7, S / 2 - r * 0.7, S / 2 + r * 0.7, S / 2 + r * 0.7], 200, 260, fill=WHITE + (255,), width=int(S * 0.07))
    d.ellipse([S * 0.3, S * 0.28, S * 0.4, S * 0.38], fill=WHITE + (255,))
    save(img, "Brush_Bubble.png", (128, 128))

    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    mask = Image.new("L", (S, S), 0)
    sparkle(ImageDraw.Draw(mask), S / 2, S / 2, S * 0.46, 255, 0.2)
    save(stack(glow(mask, 8 * SS, CYAN, 0.9), fill(mask, WHITE)), "Brush_Sparkle.png", (128, 128))


# ───────── WAVE HUD (same look) ─────────

def wave_parts():
    navy_plate("Wave_Plate.png", (300, 190), 22, [(0, CYAN), (0.5, LAV), (1, PINK)])
    ribbon("Wave_Banner.png", (1500, 300), [(0, PINK), (0.5, LAV), (1, CYAN)])
    glass_panel("Wave_Panel.png", (240, 120), 30, [(0, PINK), (0.5, LAV), (1, CYAN)])

    # countdown ring
    S = 300 * SS
    ring = Image.new("L", (S, S), 0)
    rd = ImageDraw.Draw(ring)
    rd.ellipse([18 * SS, 18 * SS, S - 18 * SS, S - 18 * SS], fill=255)
    rd.ellipse([46 * SS, 46 * SS, S - 46 * SS, S - 46 * SS], fill=0)
    img = stack(glow(ring, 16 * SS, CYAN, 0.6), fill(grow(ring, 4 * SS), NAVY), fill(ring, gradient(S, S, [(0, PINK), (0.5, LAV), (1, CYAN)], 45)))
    d = ImageDraw.Draw(img)
    for k in range(12):
        a = k / 12 * math.tau
        sparkle(d, S / 2 + math.cos(a) * S * 0.47, S / 2 + math.sin(a) * S * 0.47, (10 if k % 3 == 0 else 5) * SS, WHITE + (255,))
    save(glitch(img, 4 * SS), "Wave_Ring.png", (300, 300))

    # arrow (screen-edge marker / YOU pointer)
    S = 128 * SS
    pts = [(S * 0.5, S * 0.06), (S * 0.92, S * 0.6), (S * 0.65, S * 0.6), (S * 0.65, S * 0.94), (S * 0.35, S * 0.94), (S * 0.35, S * 0.6), (S * 0.08, S * 0.6)]
    m = poly_mask(S, S, pts)
    img = stack(fill(grow(m, 7 * SS), NAVY), fill(grow(m, 3 * SS), WHITE), fill(m, WHITE))
    save(img, "Wave_Arrow.png", (128, 128))

    # small rounded bar (9-slice, tinted in Unity)
    W, H = 64 * SS, 20 * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(img).rounded_rectangle([0, 0, W - 1, H - 1], radius=H // 2, fill=WHITE + (255,))
    save(img, "Wave_Bar.png", (64, 20))


if __name__ == "__main__":
    wave_parts()
    ribbon("Brush_Title.png", (1300, 230), [(0, PINK), (0.45, LAV), (1, CYAN)])
    glass_panel("Brush_Card.png", (360, 420), 40, [(0, PINK), (0.5, LAV), (1, CYAN)], glow_color=PINK)
    glass_panel("Brush_Chip.png", (260, 84), 38, [(0, CYAN), (1, LAV)])
    stage("Brush_Stage.png", (640, 640))
    tooth("Brush_Tooth.png", (420, 440))
    brush((300, 110))
    hand((512, 256))
    timer((300, 300))
    burst((640, 640))
    small_bits()
