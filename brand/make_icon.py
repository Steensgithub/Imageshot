# Tegner ImageShot-ikonet: blå afrundet firkant, hvide søgerhjørner og en markeringsprik.
from PIL import Image, ImageDraw
import sys, os
S = 1024
def icon(size, small=False):
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    pad = 40 if not small else 0
    # verlauf: top #2F7BF0 → bund #1857C1
    top, bot = (0x3B, 0x82, 0xF6), (0x18, 0x57, 0xC1)
    grad = Image.new("RGBA", (S, S))
    gd = ImageDraw.Draw(grad)
    for y in range(S):
        t = y / (S - 1)
        gd.line([(0, y), (S, y)], fill=tuple(int(top[i] + (bot[i] - top[i]) * t) for i in range(3)) + (255,))
    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).rounded_rectangle([pad, pad, S - pad, S - pad], radius=220 if not small else 160, fill=255)
    im.paste(grad, (0, 0), mask)
    # søgerhjørner
    w = 92 if not small else 120     # stregtykkelse (tykkere i små størrelser)
    m = 230 if not small else 190    # afstand fra kant
    L = 230 if not small else 250    # benlængde
    a, b = m, S - m
    white = (255, 255, 255, 255)
    for (x, y, dx, dy) in [(a, a, 1, 1), (b, a, -1, 1), (a, b, 1, -1), (b, b, -1, -1)]:
        d.rounded_rectangle(sorted_box(x, y, x + dx * L, y + dy * w), radius=w // 2, fill=white)
        d.rounded_rectangle(sorted_box(x, y, x + dx * w, y + dy * L), radius=w // 2, fill=white)
    r = 92 if not small else 120
    c = S // 2
    d.ellipse([c - r, c - r, c + r, c + r], fill=white)
    return im.resize((size, size), Image.LANCZOS)
def sorted_box(x0, y0, x1, y1):
    return [min(x0, x1), min(y0, y1), max(x0, x1), max(y0, y1)]
out = sys.argv[1]
os.makedirs(out, exist_ok=True)
sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
imgs = [icon(s, small=s <= 32) for s in sizes]
imgs[-1].save(os.path.join(out, "app.ico"), sizes=[(s, s) for s in sizes], append_images=imgs[:-1])
for s in [16, 32, 48, 128]:
    icon(s, small=s <= 32).save(os.path.join(out, "icon%d.png" % s))
icon(512).save(os.path.join(out, "logo512.png"))
# lille forhåndsvisning på lys og mørk baggrund
pv = Image.new("RGBA", (560, 150), (255, 255, 255, 255))
x = 10
for s in [16, 24, 32, 48, 64, 128]:
    pv.alpha_composite(icon(s, small=s <= 32), (x, 10)); x += s + 14
dark = Image.new("RGBA", (560, 150), (32, 33, 36, 255)); x = 10
for s in [16, 24, 32, 48, 64]:
    dark.alpha_composite(icon(s, small=s <= 32), (x, 10)); x += s + 14
pv2 = Image.new("RGBA", (560, 300)); pv2.paste(pv, (0, 0)); pv2.paste(dark, (0, 150))
pv2.save(os.path.join(out, "preview.png"))
