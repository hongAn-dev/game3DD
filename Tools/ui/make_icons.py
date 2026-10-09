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
    png = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', SIZE, SIZE, 8, 6, 0, 0, 0)) \
        + chunk(b'IDAT', zlib.compress(b''.join(rows), 9)) + chunk(b'IEND', b'')
    with open(os.path.join(out, name + '.png'), 'wb') as f:
        f.write(png)
    print(name)

write('sound_on', sound_on)
write('sound_off', sound_off)
write('pause', pause)
