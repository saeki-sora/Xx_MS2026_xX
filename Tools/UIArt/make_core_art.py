# Core HP bar art in the same language as the kill counter (slanted navy plate, cyan->pink frame).
import math, os
from PIL import Image, ImageDraw, ImageFilter

# Writes into <project>/Assets/_Game/UI/Art/Hud wherever it is run from (needs Python + Pillow: pip install pillow).
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "_Game", "UI", "Art", "Hud")
SS = 4
CYAN = (110, 236, 255)
PINK = (255, 128, 214)
LAV = (178, 156, 255)
NAVY = (16, 20, 52)
WHITE = (255, 255, 255)


def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(len(a)))


def save(img, name, size):
    img.resize(size, Image.LANCZOS).save(os.path.join(OUT, name))
    print("wrote", name, size)


def para(w, h, skew, inset=0):
    return [(skew + inset, inset), (w - inset, inset), (w - skew - inset, h - inset), (inset, h - inset)]


def core_plate():
    # mirrored lean compared with the kill plate ('\' instead of '/') so the two read as a pair, top and bottom
    W, H = 860 * SS, 112 * SS
    skew = 30 * SS
    base = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    pts_outer = [(0, 0), (W - skew, 0), (W, H), (skew, H)]
    mask_outer = Image.new("L", (W, H), 0)
    ImageDraw.Draw(mask_outer).polygon(pts_outer, fill=255)
    grad = Image.new("RGBA", (W, H))
    gp = grad.load()
    for x in range(W):
        t = x / (W - 1)
        c = lerp(PINK, LAV, t * 2) if t < 0.5 else lerp(LAV, CYAN, (t - 0.5) * 2)
        for y in range(H):
            gp[x, y] = c + (255,)
    base.paste(grad, (0, 0), mask_outer)
    b = 5 * SS
    pts_inner = [(b + 2 * SS, b), (W - skew - b + 2 * SS, b), (W - b - 2 * SS, H - b), (skew + b - 2 * SS, H - b)]
    mask_inner = Image.new("L", (W, H), 0)
    ImageDraw.Draw(mask_inner).polygon(pts_inner, fill=255)
    fill = Image.new("RGBA", (W, H))
    fp = fill.load()
    for y in range(H):
        t = y / (H - 1)
        c = lerp((44, 40, 96), NAVY, t)
        a = int(232 - 18 * t)
        for x in range(W):
            fp[x, y] = c + (a,)
    base.paste(fill, (0, 0), mask_inner)
    acc = ImageDraw.Draw(base)
    acc.polygon([(b + 14 * SS, b + 9 * SS), (b + 70 * SS, b + 9 * SS), (b + 74 * SS, b + 15 * SS), (b + 18 * SS, b + 15 * SS)], fill=PINK + (255,))
    acc.polygon([(W - b - 80 * SS, H - b - 15 * SS), (W - b - 22 * SS, H - b - 15 * SS), (W - b - 18 * SS, H - b - 9 * SS), (W - b - 76 * SS, H - b - 9 * SS)], fill=CYAN + (255,))
    glow = mask_outer.filter(ImageFilter.GaussianBlur(9 * SS))
    glow_img = Image.new("RGBA", (W, H), (140, 220, 255, 0))
    glow_img.putalpha(glow.point(lambda v: int(v * 0.32)))
    save(Image.alpha_composite(glow_img, base), "Core_Plate.png", (860, 112))


def core_bar():
    # white slanted bar (tinted at runtime); used for back, trail and fill
    W, H = 600 * SS, 34 * SS
    skew = 10 * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    mask = Image.new("L", (W, H), 0)
    ImageDraw.Draw(mask).polygon([(0, 0), (W - skew, 0), (W, H), (skew, H)], fill=255)
    grad = Image.new("RGBA", (W, H))
    gp = grad.load()
    for y in range(H):
        t = y / (H - 1)
        # bright top edge, slightly darker bottom: reads as a solid bar after tinting
        v = 255 if t < 0.18 else int(250 - 40 * (t - 0.18))
        for x in range(W):
            gp[x, y] = (v, v, v, 255)
    img.paste(grad, (0, 0), mask)
    save(img, "Core_Bar.png", (600, 34))


def core_ticks():
    # thin slanted separators every 10%, drawn over the bar so the remaining amount is easy to read from afar
    W, H = 600 * SS, 34 * SS
    skew = 10 * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for i in range(1, 10):
        x = W * i / 10
        w = 3 * SS if i == 5 else 2 * SS
        d.polygon([(x - w / 2, 0), (x + w / 2, 0), (x + w / 2 + skew, H), (x - w / 2 + skew, H)], fill=NAVY + (190 if i == 5 else 140,))
    save(img, "Core_Ticks.png", (600, 34))


def core_icon():
    S = 150 * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    cx, cy = S / 2, S / 2
    top, mid, bot, half = cy - 58 * SS, cy - 16 * SS, cy + 62 * SS, 44 * SS
    outline = (30, 60, 120, 255)
    gem = [(cx, top), (cx + half, mid), (cx, bot), (cx - half, mid)]
    d.polygon([(x + (3 * SS if x > cx else -3 * SS if x < cx else 0), y + (-3 * SS if y < cy else 3 * SS)) for x, y in gem], fill=outline)
    # facets
    d.polygon([(cx, top), (cx - half, mid), (cx, mid + 6 * SS)], fill=(190, 248, 255, 255))
    d.polygon([(cx, top), (cx + half, mid), (cx, mid + 6 * SS)], fill=(120, 220, 250, 255))
    d.polygon([(cx - half, mid), (cx, bot), (cx, mid + 6 * SS)], fill=(90, 190, 240, 255))
    d.polygon([(cx + half, mid), (cx, bot), (cx, mid + 6 * SS)], fill=(60, 150, 220, 255))
    # pink rim light on the right edge
    d.line([(cx + half, mid), (cx, bot)], fill=PINK + (255,), width=4 * SS)
    # sparkle
    sx, sy, r = cx - 18 * SS, top + 26 * SS, 14 * SS
    d.polygon([(sx, sy - r), (sx + 3 * SS, sy - 3 * SS), (sx + r, sy), (sx + 3 * SS, sy + 3 * SS), (sx, sy + r), (sx - 3 * SS, sy + 3 * SS), (sx - r, sy), (sx - 3 * SS, sy - 3 * SS)], fill=WHITE + (255,))
    glow = img.filter(ImageFilter.GaussianBlur(7 * SS))
    glow_t = Image.new("RGBA", img.size, CYAN + (0,))
    glow_t.putalpha(glow.getchannel("A").point(lambda v: int(v * 0.6)))
    save(Image.alpha_composite(glow_t, img), "Core_Icon.png", (150, 150))


core_plate()
core_bar()
core_ticks()
core_icon()
