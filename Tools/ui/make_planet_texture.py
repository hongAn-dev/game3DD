"""Generate a seamless 1K equirectangular low-poly planet surface for the Ending sphere.
Pure Python (math + zlib + struct); deterministic, no external packs or network."""
import math, os, struct, zlib, sys

W, H = 1024, 512
out = sys.argv[1]
os.makedirs(out, exist_ok=True)

def hash01(x, y, seed):
    n = (x * 374761393 + y * 668265263 + seed * 1442695041) & 0xffffffff
    n = ((n ^ (n >> 13)) * 1274126177) & 0xffffffff
    n ^= n >> 16
    return n / 4294967295.0

def smooth(t):
    return t * t * (3.0 - 2.0 * t)

def value_noise(u, v, nx, ny, seed):
    x = u * nx
    y = max(0.0, min(1.0, v)) * ny
    ix, iy = math.floor(x), math.floor(y)
    fx, fy = smooth(x - ix), smooth(y - iy)
    x1 = (ix + 1) % nx
    y1 = min(iy + 1, ny)
    a = hash01(ix % nx, iy, seed)
    b = hash01(x1, iy, seed)
    c = hash01(ix % nx, y1, seed)
    d = hash01(x1, y1, seed)
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy

def fbm(u, v, cells_x, cells_y, seed, octaves=4):
    total, weight, norm = 0.0, 0.56, 0.0
    for i in range(octaves):
        total += value_noise(u, v, cells_x << i, cells_y << i, seed + i * 19) * weight
        norm += weight
        weight *= 0.5
    return total / norm

def clamp(x):
    return max(0, min(255, int(round(x))))

rows = []
for y in range(H):
    row = bytearray([0])
    v = y / (H - 1)
    for x in range(W):
        u = x / W
        continents = fbm(u, v, 5, 3, 13)
        relief = fbm(u, v, 10, 5, 71)
        detail = fbm(u, v, 24, 12, 111)
        ridges = fbm(u, v, 16, 8, 257)
        # Longitude converges at the poles; fade all noise toward a shared polar cap.
        pole = abs(v - 0.5) * 2.0
        pole_blend = pole ** 6
        continents = continents * (1.0 - pole_blend) + 0.5 * pole_blend
        relief = relief * (1.0 - pole_blend) + 0.5 * pole_blend
        detail = detail * (1.0 - pole_blend) + 0.5 * pole_blend
        ridges = ridges * (1.0 - pole_blend) + 0.5 * pole_blend
        land = continents > 0.47
        variation = (relief - 0.5) * 48 + (detail - 0.5) * 25
        if land:
            # Broken coastlines and distinct dry rock plateaus, in muted plum/stone.
            coast = max(0.0, min(1.0, (continents - 0.47) / 0.07))
            r, g, b = 76 + variation, 62 + variation * 0.85, 74 + variation * 0.9
            r += (1 - coast) * 18
            g += (1 - coast) * 15
            b += (1 - coast) * 14
            if ridges > 0.55 and relief > 0.50:
                ridge = min(1.0, (ridges - 0.55) * 4)
                r, g, b = r * (1-ridge) + 144 * ridge, g * (1-ridge) + 122 * ridge, b * (1-ridge) + 108 * ridge
                # Sparse warm lava cuts across high terrain; never a full-surface emission.
                lava = fbm(u + 0.13, v + 0.07, 9, 5, 911, 3) > 0.57 and detail > 0.40
                if lava:
                    r, g, b = 210 + detail * 35, 68 + detail * 35, 35 + detail * 20
        else:
            # Olive acid seas with broad tonal variation, not a repeated high-frequency texture.
            r, g, b = 63 + variation * 0.45, 67 + variation * 0.5, 43 + variation * 0.35
            if continents > 0.44:
                r += 15
                g += 13
                b += 8
        row += bytes([clamp(r), clamp(g), clamp(b), 255])
    rows.append(bytes(row))

def chunk(kind, data):
    return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data) & 0xffffffff)

png = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', W, H, 8, 6, 0, 0, 0)) \
    + chunk(b'IDAT', zlib.compress(b''.join(rows), 8)) + chunk(b'IEND', b'')
with open(os.path.join(out, 'planet_surface.png'), 'wb') as f:
    f.write(png)
print('planet_surface.png', W, 'x', H)
