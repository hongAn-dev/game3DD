"""Draw the HUD frame sprites (9-sliced in Unity, border 24px).
hud_fill.png:     white chamfered panel, tinted by the Image / Button colors.
hud_brackets.png: corner brackets + faint edge line, transparent center, tinted by the Image color.
Usage: python make_hud_sprites.py <out_dir>   (needs Pillow)"""
import sys
from PIL import Image, ImageDraw

out = sys.argv[1]
S, C = 96, 10  # size, chamfer

fill = Image.new('RGBA', (S, S), (0, 0, 0, 0))
d = ImageDraw.Draw(fill)
d.polygon([(C, 0), (S - 1, 0), (S - 1, S - 1 - C), (S - 1 - C, S - 1), (0, S - 1), (0, C)], fill=(255, 255, 255, 255))
fill.save(f'{out}/hud_fill.png')

br = Image.new('RGBA', (S, S), (0, 0, 0, 0))
d = ImageDraw.Draw(br)
edge = (255, 255, 255, 90)
d.line([(C, 0), (S - 1, 0), (S - 1, S - 1 - C), (S - 1 - C, S - 1), (0, S - 1), (0, C), (C, 0)], fill=edge, width=2)
L, T = 22, 4  # bracket arm length, thickness
for (x, y, dx, dy) in [(0, 0, 1, 1), (S - 1, 0, -1, 1), (0, S - 1, 1, -1), (S - 1, S - 1, -1, -1)]:
    d.line([(x, y), (x + dx * L, y)], fill=(255, 255, 255, 255), width=T)
    d.line([(x, y), (x, y + dy * L)], fill=(255, 255, 255, 255), width=T)
br.save(f'{out}/hud_brackets.png')
print('HUD_SPRITES_OK', out)
