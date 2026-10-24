# Wave HUD placeholder art (2026-10-24). Writes PNGs into Assets/_Game/UI/Art/Hud/ (overwrite them to reskin;
# the HUD builder only refers to the file names). Needs Python + Pillow: pip install pillow.
#   Wave_Plate.png        WAVE / あと○体 / TIME plate (3 rows), left of the tooth core bar
#   Wave_Bar.png          small rounded bar (9-slice), tinted in Unity (progress back/fill)
#   Wave_Banner.png       wide slanted band behind "WAVE 2 / 目標撃破数 60！" (tinted: normal / final / clear)
#   Wave_Ring.png         ring behind the 3・2・1 countdown
#   Wave_Hand.png         a hand closing and opening (2 frames side by side) for the brushing hint
#   Wave_Brush.png        toothbrush (white, tinted with the player colour)
#   Wave_Stain.png        soft stain blob put on the tooth during the brushing time
#   Wave_Arrow.png        arrow for the screen-edge "enemies come from here" marker and the "YOU" pointer
import math, os
from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "_Game", "UI", "Art", "Hud")
os.makedirs(OUT, exist_ok=True)
SS = 4

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


def notched(w, h, n, inset=0):
    i = inset
    return [(n + i, i), (w - n - i, i), (w - i, n + i), (w - i, h - n - i), (w - n - i, h - i), (n + i, h - i), (i, h - n - i), (i, n + i)]


def gradient_mask_paste(base, poly, c0, c1, horizontal=True):
    w, h = base.size
    grad = Image.new("RGBA", (w, h))
    px = grad.load()
    for a in range(w if horizontal else h):
        t = a / max(1, (w if horizontal else h) - 1)
        c = lerp(c0, c1, t) + (255,)
        for b in range(h if horizontal else w):
            px[(a, b) if horizontal else (b, a)] = c
    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).polygon(poly, fill=255)
    base.paste(grad, (0, 0), mask)


def plate():
    W, H = 300 * SS, 190 * SS
    n = 18 * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    gradient_mask_paste(img, notched(W, H, n), CYAN, PINK)
    d = ImageDraw.Draw(img)
    d.polygon(notched(W, H, n, 5 * SS), fill=NAVY + (235,))
    inner = notched(W, H, n - 4 * SS, 14 * SS)
    d.polygon(inner, fill=(28, 34, 84, 235))
    # two dividers between the three rows
    for k in (1, 2):
        y = int(14 * SS + (H - 28 * SS) * k / 3)
        d.line([(26 * SS, y), (W - 26 * SS, y)], fill=CYAN + (110,), width=2 * SS)
    # small bolts in the corners (like the mockup's little circles)
    for (x, y) in [(n + 8 * SS, 10 * SS), (W - n - 8 * SS, H - 10 * SS)]:
        d.ellipse([x - 5 * SS, y - 5 * SS, x + 5 * SS, y + 5 * SS], fill=WHITE + (220,))
    save(img, "Wave_Plate.png", (300, 190))


def bar():
    W, H = 64 * SS, 20 * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(img).rounded_rectangle([0, 0, W - 1, H - 1], radius=H // 2, fill=WHITE + (255,))
    save(img, "Wave_Bar.png", (64, 20))


def banner():
    W, H = 1500 * SS, 300 * SS
    skew = 70 * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    outer = [(skew, 0), (W, 0), (W - skew, H), (0, H)]
    gradient_mask_paste(img, outer, CYAN, PINK)
    d = ImageDraw.Draw(img)
    b = 8 * SS
    inner = [(skew + b * 1.4, b), (W - b, b), (W - skew - b * 1.4, H - b), (b, H - b)]
    d.polygon(inner, fill=WHITE + (235,))
    # speed lines
    for i in range(14):
        y = int(H * (0.12 + 0.76 * i / 13))
        x0 = int(W * (0.02 + 0.03 * (i % 3)))
        d.line([(x0, y), (x0 + int(W * 0.08), y)], fill=LAV + (90,), width=3 * SS)
        d.line([(W - x0 - int(W * 0.08), y), (W - x0, y)], fill=LAV + (90,), width=3 * SS)
    save(img, "Wave_Banner.png", (1500, 300))


def ring():
    S = 300 * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse([10 * SS, 10 * SS, S - 10 * SS, S - 10 * SS], outline=WHITE + (255,), width=16 * SS)
    for k in range(12):
        a = k / 12 * math.tau
        r0, r1 = S * 0.36, S * 0.42
        c = S / 2
        d.line([(c + math.cos(a) * r0, c + math.sin(a) * r0), (c + math.cos(a) * r1, c + math.sin(a) * r1)], fill=WHITE + (200,), width=6 * SS)
    save(img, "Wave_Ring.png", (300, 300))


def hand_frame(d, ox, closed):
    # simple mitten-like hand: palm + 4 fingers (curled when closed) + thumb
    s = SS
    d.rounded_rectangle([ox + 60 * s, 110 * s, ox + 190 * s, 230 * s], radius=36 * s, fill=WHITE + (255,), outline=NAVY + (255,), width=6 * s)
    for i in range(4):
        x = ox + (66 + i * 30) * s
        if closed:
            d.rounded_rectangle([x, 92 * s, x + 28 * s, 140 * s], radius=13 * s, fill=WHITE + (255,), outline=NAVY + (255,), width=5 * s)
        else:
            d.rounded_rectangle([x, 22 * s, x + 28 * s, 132 * s], radius=13 * s, fill=WHITE + (255,), outline=NAVY + (255,), width=5 * s)
    if closed:
        d.rounded_rectangle([ox + 26 * s, 140 * s, ox + 100 * s, 176 * s], radius=17 * s, fill=WHITE + (255,), outline=NAVY + (255,), width=5 * s)
    else:
        d.rounded_rectangle([ox + 10 * s, 120 * s, ox + 76 * s, 160 * s], radius=18 * s, fill=WHITE + (255,), outline=NAVY + (255,), width=5 * s)


def hand():
    W, H = 512 * SS, 256 * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    hand_frame(d, 0, False)
    hand_frame(d, 256 * SS, True)
    save(img, "Wave_Hand.png", (512, 256))


def brush():
    W, H = 256 * SS, 96 * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([0, 52 * SS, 190 * SS, 74 * SS], radius=11 * SS, fill=WHITE + (255,))
    d.rounded_rectangle([170 * SS, 46 * SS, 250 * SS, 80 * SS], radius=10 * SS, fill=WHITE + (255,))
    for i in range(9):
        x = (176 + i * 8) * SS
        d.rectangle([x, 8 * SS, x + 5 * SS, 48 * SS], fill=(235, 250, 255, 255))
    save(img, "Wave_Brush.png", (256, 96))


def stain():
    S = 160 * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c = S / 2
    for k in range(7):
        a = k / 7 * math.tau
        r = S * 0.2
        x, y = c + math.cos(a) * r * 0.9, c + math.sin(a) * r * 0.7
        rr = S * (0.17 + 0.05 * ((k * 37) % 3))
        d.ellipse([x - rr, y - rr, x + rr, y + rr], fill=(255, 255, 255, 255))
    img = img.filter(ImageFilter.GaussianBlur(5 * SS))
    save(img, "Wave_Stain.png", (160, 160))


def arrow():
    W, H = 128 * SS, 128 * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    pts = [(W * 0.5, H * 0.06), (W * 0.94, H * 0.62), (W * 0.64, H * 0.62), (W * 0.64, H * 0.94), (W * 0.36, H * 0.94), (W * 0.36, H * 0.62), (W * 0.06, H * 0.62)]
    d.polygon(pts, fill=WHITE + (255,), outline=NAVY + (255,))
    d.line(pts + [pts[0]], fill=NAVY + (255,), width=7 * SS)
    save(img, "Wave_Arrow.png", (128, 128))


if __name__ == "__main__":
    plate()
    bar()
    banner()
    ring()
    hand()
    brush()
    stain()
    arrow()
