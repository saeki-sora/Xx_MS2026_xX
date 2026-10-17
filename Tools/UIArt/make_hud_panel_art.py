# HUD panels (2026-10-17): turns the team's HUD pictures in Tools/UIArt/HudSource/ into the sprites the HUD builder uses.
#  - CoreBar.png (tooth icon + navy-framed gradient bar with "100%" baked in) is split into
#    Hud_CoreTooth.png (icon), Hud_CoreFrame.png (navy frame, hole in the middle) and Hud_CoreFill.png (gradient only, the text removed)
#    so the bar can shrink and the percentage can be live text.
#  - PlayerFrame / PlayerGaugeBack / PlayerGaugeFill / OtherIcon are copied as they are (Hud_ prefix).
# Replace a picture in HudSource and run this again to reskin (needs Python + Pillow: pip install pillow).
import os, shutil
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "HudSource")
OUT = os.path.join(HERE, "..", "..", "Assets", "_Game", "UI", "Art", "Hud")

# CoreBar.png layout (640x163): tooth at the left, bar frame x 165..637, y 53..101 (navy, 7 px), inside x 172..630, y 60..94.
TOOTH_BOX = (0, 0, 160, 163)
FRAME_BOX = (162, 48, 640, 106)
INNER = (172, 60, 631, 95)  # left, top, right (exclusive), bottom (exclusive)


def core():
    src = Image.open(os.path.join(SRC, "CoreBar.png")).convert("RGBA")
    src.crop(TOOTH_BOX).save(os.path.join(OUT, "Hud_CoreTooth.png"))

    # gradient: the bar is a left-to-right gradient, so a clean column is one colour from top to bottom.
    # Columns the text (white letters + their darker edge) touches are not, and are interpolated from their clean neighbours.
    l, t, r, b = INNER
    text_cols = set()
    for x in range(l, r):
        column = [src.getpixel((x, y)) for y in range(t, b)]
        if max(abs(p[n] - column[len(column) // 2][n]) for p in column for n in range(3)) > 6:
            text_cols.update(range(x - 3, x + 4))
    cols = [None if x in text_cols else src.getpixel((x, (t + b) // 2)) for x in range(l, r)]
    filled = list(cols)
    i = 0
    while i < len(filled):
        if filled[i] is None:
            j = i
            while j < len(filled) and filled[j] is None:
                j += 1
            a = filled[i - 1] if i > 0 else filled[j]
            c = filled[j] if j < len(filled) else a
            for k in range(i, j):
                f = (k - i + 1) / (j - i + 1)
                filled[k] = tuple(int(round(a[n] + (c[n] - a[n]) * f)) for n in range(4))
            i = j
        i += 1
    fill = Image.new("RGBA", (r - l, b - t))
    for x, colour in enumerate(filled):
        for y in range(b - t):
            fill.putpixel((x, y), colour[:3] + (255,))
    fill.save(os.path.join(OUT, "Hud_CoreFill.png"))

    # frame: the hole is cut out; where the letters stick out over the straight top/bottom border, that column of the border is
    # copied from a clean column (the border is straight there).
    frame = src.crop(FRAME_BOX)
    fl, ft = FRAME_BOX[0], FRAME_BOX[1]
    clean_x = next(x for x in range(l + 20, r) if x not in text_cols)
    for y in range(frame.height):
        for x in range(frame.width):
            sx, sy = x + fl, y + ft
            if l <= sx < r and t <= sy < b:
                frame.putpixel((x, y), (0, 0, 0, 0))
            elif sx in text_cols and l <= sx < r:
                frame.putpixel((x, y), src.getpixel((clean_x, sy)))
    frame.save(os.path.join(OUT, "Hud_CoreFrame.png"))
    print("wrote Hud_CoreTooth / Hud_CoreFill", fill.size, "/ Hud_CoreFrame", frame.size)


def copies():
    # OtherRow (icon + bar) was replaced by OtherIcon (icon only) on 2026-10-17; its source stays in HudSource.
    for name in ("PlayerFrame", "PlayerGaugeBack", "PlayerGaugeFill", "OtherIcon"):
        shutil.copyfile(os.path.join(SRC, name + ".png"), os.path.join(OUT, "Hud_" + name + ".png"))
        print("copied Hud_" + name + ".png")


if __name__ == "__main__":
    core()
    copies()
