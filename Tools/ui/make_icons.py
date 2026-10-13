"""Draw the small HUD icons (spec §7.6): speaker on, speaker crossed out, pause. White on transparent, 128 px,
anti-aliased by 4x4 supersampling. Pure Python (zlib + struct).
Usage: python3 Tools/ui/make_icons.py <out_dir>"""
import math, os, struct, sys, zlib

SIZE, SS = 128, 4
out = sys.argv[1]
os.makedirs(out, exist_ok=True)

def inside_poly(x, y, pts):
    hit = False
    for (x1, y1), (x2, y2) in zip(pts, pts[1:] + pts[:1]):
        if (y1 > y) != (y2 > y) and x < x1 + (y - y1) * (x2 - x1) / (y2 - y1):
            hit = not hit
    return hit

def seg_dist(x, y, a, b):
    (x1, y1), (x2, y2) = a, b
    dx, dy = x2 - x1, y2 - y1
    t = max(0.0, min(1.0, ((x - x1) * dx + (y - y1) * dy) / (dx * dx + dy * dy)))
    return math.hypot(x - x1 - t * dx, y - y1 - t * dy)

SPEAKER = [(14, 48), (38, 48), (66, 22), (66, 106), (38, 80), (14, 80)]

def arc(x, y, r, width):   # right-facing arc around the speaker mouth
    d = math.hypot(x - 66, y - 64)
    return abs(d - r) <= width / 2 and x > 66 and abs(math.atan2(y - 64, x - 66)) < math.radians(50)

def sound_on(x, y):
    return inside_poly(x, y, SPEAKER) or arc(x, y, 22, 9) or arc(x, y, 42, 9)

def sound_off(x, y):
    return inside_poly(x, y, SPEAKER) or seg_dist(x, y, (80, 44), (116, 84)) <= 5 or seg_dist(x, y, (116, 44), (80, 84)) <= 5

def pause(x, y):
    return (30 <= x <= 54 or 74 <= x <= 98) and 22 <= y <= 106

def write(name, shape):
    rows = []
    for py in range(SIZE):
        row = bytearray([0])
        for px in range(SIZE):
            cover = sum(shape(px + (i + 0.5) / SS, py + (j + 0.5) / SS) for i in range(SS) for j in range(SS)) / (SS * SS)
            row += bytes([255, 255, 255, int(round(cover * 255))])
        rows.append(bytes(row))
    def chunk(kind, data):
        return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data) & 0xffffffff)
    png = (b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', SIZE, SIZE, 8, 6, 0, 0, 0))
           + chunk(b'IDAT', zlib.compress(b''.join(rows), 9)) + chunk(b'IEND', b''))
    with open(os.path.join(out, name + '.png'), 'wb') as f:
        f.write(png)
    print(name)

write('sound_on', sound_on)
write('sound_off', sound_off)
write('pause', pause)

# BGM and harmful-item icons use the same monochrome silhouette style; tint them by state/type in the UI.
def music_note(x, y):
    stem = 78 <= x <= 87 and 24 <= y <= 86
    head = ((x - 64) / 14) ** 2 + ((y - 88) / 10) ** 2 <= 1 or ((x - 88) / 14) ** 2 + ((y - 78) / 10) ** 2 <= 1
    flag = inside_poly(x, y, [(82, 24), (107, 32), (107, 43), (83, 36)])
    return stem or head or flag

def music_note_off(x, y):
    return music_note(x, y) or seg_dist(x, y, (24, 24), (104, 104)) <= 5

def bottle(x, y):
    neck = 48 <= x <= 80 and 16 <= y <= 38
    body = inside_poly(x, y, [(45, 34), (83, 34), (83, 52), (99, 69), (99, 109), (29, 109), (29, 69), (45, 52)])
    skull = ((x - 64) ** 2 + (y - 77) ** 2 <= 15 ** 2) and not (x in range(57, 71) and y in range(70, 82))
    eyes = ((x - 58) ** 2 + (y - 73) ** 2 <= 3 ** 2) or ((x - 70) ** 2 + (y - 73) ** 2 <= 3 ** 2)
    return neck or body or skull or eyes

def minus_crystal(x, y):
    crystal = inside_poly(x, y, [(64, 14), (100, 48), (88, 99), (64, 114), (40, 99), (28, 48)])
    minus = 43 <= x <= 85 and 59 <= y <= 69
    return crystal or minus

def magnet(x, y):
    # Thick U-shaped magnet with separated, colorable pole ends.
    d = math.hypot(x - 64, y - 55)
    ring = 28 <= d <= 46 and y >= 48 and x > 18 and x < 110
    tips = (27 <= x <= 43 and 30 <= y <= 58) or (85 <= x <= 101 and 30 <= y <= 58)
    return ring or tips

write('music_on', music_note)
write('music_off', music_note_off)
write('trap_poison', bottle)
write('trap_anti_energy', minus_crystal)
write('trap_magnetic', magnet)

def lock(x, y):   # padlock: shackle arc over a rounded body (locked level in "Chơi tiếp")
    body = 26 <= x <= 102 and 56 <= y <= 112
    d = math.hypot(x - 64, y - 52)
    shackle = 22 <= d <= 34 and y <= 58
    keyhole = math.hypot(x - 64, y - 78) <= 7 or (61 <= x <= 67 and 78 <= y <= 98)
    return (body or shackle) and not keyhole

write('lock', lock)

# Soft dark radial gradient used only behind the menu title/subtitle. Peak alpha is about 76%; edges fade clear.
def write_gradient(name, width=512, height=256):
    rows = []
    for py in range(height):
        row = bytearray([0])
        for px in range(width):
            nx = (px - (width - 1) / 2) / (width * 0.58)
            ny = (py - (height - 1) / 2) / (height * 0.58)
            r = math.sqrt(nx * nx + ny * ny)
            alpha = int(round(194 * max(0.0, min(1.0, (1.0 - r) / 0.24)) ** 1.4))
            row += bytes([8, 16, 24, alpha])
        rows.append(bytes(row))
    def chunk(kind, data):
        return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data) & 0xffffffff)
    png = (b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0))
           + chunk(b'IDAT', zlib.compress(b''.join(rows), 9)) + chunk(b'IEND', b''))
    with open(os.path.join(out, name + '.png'), 'wb') as f:
        f.write(png)

write_gradient('title_gradient')
