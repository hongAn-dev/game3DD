"""App icon / logo for Robo Lạc Lối: the robot ball (white shell, cyan band) on a pink alien hill under a violet
night sky, with a glowing cyan energy crystal and a ringed planet. Writes app_icon.png (1024, full icon) and the
Android adaptive layers icon_background.png / icon_foreground.png (foreground kept inside the 66 % safe circle).
Pillow only. Usage: python3 Tools/ui/make_app_icon.py <out_dir>"""
import math, os, random, sys
from PIL import Image, ImageDraw, ImageFilter

S = 2048                    # drawn at 2x, saved at 1024
out = sys.argv[1]
os.makedirs(out, exist_ok=True)
CYAN = (46, 230, 230)

def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(len(a)))

def background():
    img = Image.new('RGB', (S, S))
    d = ImageDraw.Draw(img)
    top, bottom = (24, 18, 58), (88, 46, 120)
    for y in range(S):
        d.line([(0, y), (S, y)], fill=lerp(top, bottom, y / S))
    rng = random.Random(4)
    for _ in range(140):   # stars
        x, y, r = rng.randrange(S), rng.randrange(int(S * 0.62)), rng.choice([2, 3, 3, 4, 6])
        d.ellipse([x - r, y - r, x + r, y + r], fill=(255, 255, 255, 255) if r < 6 else (180, 250, 250))
    # ringed planet, top left
    glow = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    g = ImageDraw.Draw(glow)
    cx, cy, r = int(S * 0.24), int(S * 0.24), int(S * 0.12)
    g.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(255, 150, 90, 255))
    g.ellipse([cx - r + 30, cy - r + 10, cx + r - 70, cy + r - 90], fill=(255, 190, 120, 255))
    g.ellipse([cx - int(r * 1.8), cy - int(r * 0.35), cx + int(r * 1.8), cy + int(r * 0.35)], outline=(255, 220, 170, 255), width=18)
    img.paste(glow, (0, 0), glow)
    # pink alien hill (same pink as Level 1 ground)
    d.ellipse([-int(S * 0.4), int(S * 0.70), int(S * 1.4), int(S * 1.9)], fill=(206, 160, 176))
    d.ellipse([-int(S * 0.3), int(S * 0.74), int(S * 1.3), int(S * 1.9)], fill=(226, 184, 196))
    return img

def foreground():
    img = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    # cyan glow under the crystal and the ball
    glow = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    g = ImageDraw.Draw(glow)
    g.ellipse([int(S * 0.56), int(S * 0.36), int(S * 0.86), int(S * 0.70)], fill=CYAN + (150,))
    glow = glow.filter(ImageFilter.GaussianBlur(70))
    img.alpha_composite(glow)
    d = ImageDraw.Draw(img)
    # energy crystal (right)
    cx, cy = int(S * 0.71), int(S * 0.53)
    w, h = int(S * 0.075), int(S * 0.15)
    d.polygon([(cx, cy - h), (cx + w, cy - h * 0.25), (cx + w * 0.6, cy + h * 0.7), (cx - w * 0.6, cy + h * 0.7), (cx - w, cy - h * 0.25)], fill=(120, 245, 245))
    d.polygon([(cx, cy - h), (cx + w, cy - h * 0.25), (cx, cy + h * 0.1)], fill=(210, 255, 255))
    d.polygon([(cx, cy - h), (cx - w, cy - h * 0.25), (cx, cy + h * 0.1)], fill=(70, 210, 220))
    # robot ball (centre-left)
    bx, by, r = int(S * 0.42), int(S * 0.55), int(S * 0.20)
    shadow = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).ellipse([bx - r, by + r - 60, bx + r + 40, by + r + 70], fill=(60, 30, 70, 120))
    img.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(30)))
    ball = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    b = ImageDraw.Draw(ball)
    for i in range(r, 0, -4):   # shaded sphere, light from top left
        t = i / r
        col = lerp((250, 250, 248), (150, 160, 175), t ** 2)
        off = int((1 - t) * r * 0.35)
        b.ellipse([bx - i - off, by - i - off, bx + i - off, by + i - off], fill=col + (255,))
    mask = Image.new('L', (S, S), 0)
    ImageDraw.Draw(mask).ellipse([bx - r, by - r, bx + r, by + r], fill=255)
    band = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    bd = ImageDraw.Draw(band)
    bd.ellipse([bx - r * 1.15, by + r * 0.10, bx + r * 1.15, by + r * 0.60], fill=CYAN + (255,))
    bd.ellipse([bx - r * 1.15, by + r * 0.30, bx + r * 1.15, by + r * 0.82], fill=(0, 0, 0, 0))
    for px, py, pw in [(-0.45, -0.35, 0.16), (0.10, -0.55, 0.12), (0.35, -0.10, 0.14), (-0.20, 0.0, 0.10)]:   # rust patches
        x0, y0 = bx + px * r, by + py * r
        bd.rectangle([x0, y0, x0 + pw * r, y0 + pw * r * 0.7], fill=(150, 95, 70, 255))
    ball.alpha_composite(band)
    eye = (bx + int(r * 0.15), by - int(r * 0.12))   # visor eye
    ImageDraw.Draw(ball).ellipse([eye[0] - 70, eye[1] - 46, eye[0] + 70, eye[1] + 46], fill=(30, 40, 60, 255))
    ImageDraw.Draw(ball).ellipse([eye[0] - 40, eye[1] - 24, eye[0] + 44, eye[1] + 26], fill=CYAN + (255,))
    ball.putalpha(Image.composite(ball.getchannel('A'), Image.new('L', (S, S), 0), mask))
    img.alpha_composite(ball)
    ImageDraw.Draw(img).ellipse([bx - r, by - r, bx + r, by + r], outline=(40, 50, 70, 255), width=10)
    return img

def rounded(img, radius):
    mask = Image.new('L', img.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, img.size[0] - 1, img.size[1] - 1], radius, fill=255)
    img.putalpha(mask)
    return img

bg, fg = background(), foreground()
full = bg.convert('RGBA'); full.alpha_composite(fg)
full.resize((1024, 1024), Image.LANCZOS).save(os.path.join(out, 'app_icon.png'))
bg.resize((1024, 1024), Image.LANCZOS).save(os.path.join(out, 'icon_background.png'))
# adaptive foreground: shrink into the safe zone (launchers crop up to the outer third)
small = fg.resize((int(S * 0.78), int(S * 0.78)), Image.LANCZOS)
layer = Image.new('RGBA', (S, S), (0, 0, 0, 0)); layer.alpha_composite(small, ((S - small.size[0]) // 2, (S - small.size[1]) // 2))
layer.resize((1024, 1024), Image.LANCZOS).save(os.path.join(out, 'icon_foreground.png'))
print('ICON_OK')
