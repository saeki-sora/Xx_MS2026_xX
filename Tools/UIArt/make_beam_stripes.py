# Striped toothpaste texture for the laser beam (white body, one red and one blue stripe along the beam).
# The beam is a LineRenderer with textureMode Stretch: U runs along the beam, V runs across its width,
# so the stripes are horizontal bands in V and the texture only needs a few pixels in U.
# Writes <project>/Assets/_Game/Fortress/Effects/Toothpaste/Toothpaste_BeamStripes.png. Needs Python + Pillow.
import math, os
from PIL import Image

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "_Game", "Fortress", "Effects", "Toothpaste", "Toothpaste_BeamStripes.png")
W, H = 8, 256

WHITE = (250, 252, 255)
RED = (232, 52, 66)
BLUE = (38, 112, 232)

# (start, end, colour) across the width, 0 = one edge, 1 = the other edge
BANDS = [
    (0.00, 0.14, WHITE),
    (0.14, 0.32, RED),
    (0.32, 0.68, WHITE),
    (0.68, 0.86, BLUE),
    (0.86, 1.00, WHITE),
]
SOFT = 0.015  # soft border between bands (paste stripes are not razor sharp)


def band_colour(v):
    col = [0.0, 0.0, 0.0]
    weight = 0.0
    for start, end, c in BANDS:
        # smooth membership of v in [start, end]
        a = min(1.0, max(0.0, (v - (start - SOFT)) / (2 * SOFT)))
        b = min(1.0, max(0.0, ((end + SOFT) - v) / (2 * SOFT)))
        w = a * b
        for i in range(3):
            col[i] += c[i] * w
        weight += w
    return [x / max(weight, 1e-6) for x in col]


img = Image.new("RGBA", (W, H))
px = img.load()
for y in range(H):
    v = (y + 0.5) / H
    c = band_colour(v)
    # round tube shading: darker towards both edges, small highlight a bit above the middle
    across = (v - 0.5) * 2.0
    shade = 0.80 + 0.20 * math.sqrt(max(0.0, 1.0 - across * across))
    highlight = 0.10 * math.exp(-((v - 0.60) / 0.06) ** 2)
    rgb = tuple(int(max(0, min(255, ch * shade + 255 * highlight))) for ch in c)
    for x in range(W):
        px[x, H - 1 - y] = rgb + (255,)

img.save(OUT)
print("wrote", os.path.normpath(OUT))
