# Wave header + kill meter (2026-11-11): turns the team's pictures in Tools/UIArt/HudSource/ into the sprites the HUD uses
# (Assets/_Game/UI/Editor/Core/HudHeaderBuilder.cs).
#  - ingame_header.png (808x70, "WAVE", germ icon, progress bar, clock baked in) -> Hud_WaveHeader.png with the bar's light
#    fill painted out (the slot stays navy), so the bar can be a live gauge, and Hud_WaveBarFill.png (the light pill, 9-slice
#    left/right 8 px) drawn from the colours of the original fill.
#  - ingame_killmeter.png (620x220, "クリーン完了", team card + own card) -> Hud_KillMeter.png as it is.
#  - teethBar_frame.png (576x118, tooth + bar frame, the empty dark slot baked in; core HP, 2026-11-11) -> Hud_CoreBarFrame.png as it is,
#    plus Hud_CoreBarFill.png (the pink->cyan gradient of Hud_CoreFill.png as a round-ended pill that fits the slot) and
#    Hud_CoreBarTrail.png (the same pill in white, for the "just lost" trail). Both are drawn over the frame.
# The numbers are live text placed by the builder. Replace a picture in HudSource and run this again to reskin
# (needs Python + Pillow: pip install pillow). Writes next to the project regardless of the current folder.
import os, shutil
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "HudSource")
OUT = os.path.join(HERE, "..", "..", "Assets", "_Game", "UI", "Art", "Hud")

# ingame_header.png: navy bar slot x 226..531, y 27..47 (rounded ends); light fill (pill) x 230..459, y 30..44.
SLOT = (226, 27, 532, 48)       # left, top, right (exclusive), bottom (exclusive)
NAVY = (15, 15, 95, 255)
FILL_DARK = (136, 145, 207)     # left end of the original fill
FILL_LIGHT = (186, 197, 246)    # right end
FILL_EDGE = (233, 215, 228)     # 1 px light rim
FILL_SIZE = (64, 15)
SS = 4                          # supersampling for smooth round ends


def rounded_mask(size, inset=0):
    w, h = size
    big = Image.new("L", (w * SS, h * SS), 0)
    r = (h - 2 * inset) * SS / 2
    ImageDraw.Draw(big).rounded_rectangle((inset * SS, inset * SS, (w - inset) * SS - 1, (h - inset) * SS - 1), radius=r, fill=255)
    return big.resize(size, Image.LANCZOS)


def header():
    src = Image.open(os.path.join(SRC, "ingame_header.png")).convert("RGBA")
    l, t, r, b = SLOT
    # paint the inside of the slot (1 px in from its anti-aliased edge) navy: removes the baked fill.
    mask = rounded_mask((r - l, b - t), inset=1)
    src.paste(Image.new("RGBA", mask.size, NAVY), (l, t), mask)
    src.save(os.path.join(OUT, "Hud_WaveHeader.png"))


def bar_fill():
    w, h = FILL_SIZE
    big = Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))
    grad = Image.new("RGBA", big.size)
    for x in range(big.width):
        k = x / (big.width - 1)
        c = tuple(int(FILL_DARK[n] + (FILL_LIGHT[n] - FILL_DARK[n]) * k) for n in range(3)) + (255,)
        for y in range(big.height):
            grad.putpixel((x, y), c)
    outer = Image.new("L", big.size, 0)
    ImageDraw.Draw(outer).rounded_rectangle((0, 0, big.width - 1, big.height - 1), radius=big.height / 2, fill=255)
    inner = Image.new("L", big.size, 0)
    e = SS  # 1 px rim
    ImageDraw.Draw(inner).rounded_rectangle((e, e, big.width - 1 - e, big.height - 1 - e), radius=(big.height - 2 * e) / 2, fill=255)
    rim = Image.new("RGBA", big.size, FILL_EDGE + (255,))
    big.paste(rim, (0, 0), outer)
    big.paste(grad, (0, 0), inner)
    big.resize(FILL_SIZE, Image.LANCZOS).save(os.path.join(OUT, "Hud_WaveBarFill.png"))


# teethBar_frame.png: dark slot x 108..563, y 42..72 (round ends). The gauge sits 3 px inside it.
CORE_GAUGE = (450, 25)


def pill(size, colour_at):
    w, h = size
    big = Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))
    paint = Image.new("RGBA", big.size)
    for x in range(big.width):
        c = colour_at(x / (big.width - 1))
        for y in range(big.height):
            paint.putpixel((x, y), c)
    mask = Image.new("L", big.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, big.width - 1, big.height - 1), radius=big.height / 2, fill=255)
    big.paste(paint, (0, 0), mask)
    return big.resize(size, Image.LANCZOS)


def core_bar():
    shutil.copyfile(os.path.join(SRC, "teethBar_frame.png"), os.path.join(OUT, "Hud_CoreBarFrame.png"))
    grad = Image.open(os.path.join(OUT, "Hud_CoreFill.png")).convert("RGBA")
    gw, gh = grad.size
    pill(CORE_GAUGE, lambda k: grad.getpixel((int(k * (gw - 1)), gh // 2))).save(os.path.join(OUT, "Hud_CoreBarFill.png"))
    pill(CORE_GAUGE, lambda k: (255, 255, 255, 255)).save(os.path.join(OUT, "Hud_CoreBarTrail.png"))


def kill_meter():
    shutil.copyfile(os.path.join(SRC, "ingame_killmeter.png"), os.path.join(OUT, "Hud_KillMeter.png"))


if __name__ == "__main__":
    header()
    bar_fill()
    kill_meter()
    core_bar()
    print("wrote Hud_WaveHeader.png, Hud_WaveBarFill.png, Hud_KillMeter.png, Hud_CoreBarFrame.png, Hud_CoreBarFill.png, Hud_CoreBarTrail.png to",
          os.path.normpath(OUT))
