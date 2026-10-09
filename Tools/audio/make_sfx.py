"""Synthesize the Robo Lac Loi sound set (spec §8.2): short, warm, light electronic cues and two soft ambience beds.
Pure Python (wave + math), 22.05 kHz mono 16-bit, every file normalised to -3 dBFS peak. Deterministic (seeded).
Usage: python3 Tools/audio/make_sfx.py <out_dir>"""
import math, os, random, struct, sys, wave

RATE = 22050
PEAK = 10 ** (-3 / 20)   # -3 dBFS
out = sys.argv[1]
os.makedirs(out, exist_ok=True)
rng = random.Random(7)

def silence(seconds):
    return [0.0] * int(seconds * RATE)

def tone(freq, seconds, attack=0.005, decay=None, shape='sine', bend=0.0, vibrato=0.0):
    n = int(seconds * RATE)
    decay = decay if decay is not None else seconds / 4
    res, phase = [], 0.0
    for i in range(n):
        t = i / RATE
        f = freq * (1 + bend * t / seconds) * (1 + vibrato * math.sin(2 * math.pi * 6 * t))
        phase += 2 * math.pi * f / RATE
        if shape == 'sine':
            v = math.sin(phase)
        else:   # soft triangle-ish: sine plus a little 2nd/3rd harmonic, warm not buzzy
            v = math.sin(phase) + 0.18 * math.sin(2 * phase) + 0.08 * math.sin(3 * phase)
        env = min(1.0, t / attack) * math.exp(-max(0.0, t - attack) / decay)
        res.append(v * env)
    return res

def noise(seconds, cutoff_start, cutoff_end, attack=0.01, decay=None):
    n = int(seconds * RATE)
    decay = decay if decay is not None else seconds / 3
    res, y = [], 0.0
    for i in range(n):
        t = i / RATE
        cutoff = cutoff_start + (cutoff_end - cutoff_start) * t / seconds
        a = 1 - math.exp(-2 * math.pi * cutoff / RATE)   # one-pole low-pass
        y += a * (rng.uniform(-1, 1) - y)
        env = min(1.0, t / attack) * math.exp(-max(0.0, t - attack) / decay)
        res.append(y * env)
    return res

def mix(*tracks, offsets=None):
    offsets = offsets or [0.0] * len(tracks)
    length = max(int(o * RATE) + len(tr) for tr, o in zip(tracks, offsets))
    res = [0.0] * length
    for tr, o in zip(tracks, offsets):
        start = int(o * RATE)
        for i, v in enumerate(tr):
            res[start + i] += v
    return res

def fade(samples, fade_in, fade_out):
    n = len(samples)
    fi, fo = int(fade_in * RATE), int(fade_out * RATE)
    return [v * min(1.0, i / fi if fi else 1.0) * min(1.0, (n - i) / fo if fo else 1.0) for i, v in enumerate(samples)]

def loop(samples, cross):
    # seamless loop: crossfade the tail into the head
    c = int(cross * RATE)
    head, body, tail = samples[:c], samples[c:-c], samples[-c:]
    joined = [tail[i] * (1 - i / c) + head[i] * (i / c) for i in range(c)]
    return joined + body

def write(name, samples):
    peak = max(abs(v) for v in samples) or 1.0
    scale = PEAK / peak
    with wave.open(os.path.join(out, name + '.wav'), 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b''.join(struct.pack('<h', int(max(-1, min(1, v * scale)) * 32767)) for v in samples))
    print(name, round(len(samples) / RATE, 3), 's')

# --- UI / pickups ---
write('ui_click', mix(tone(1800, 0.06, 0.002, 0.012), noise(0.03, 6000, 3000, 0.001, 0.006)))
write('energy_pickup', mix(tone(1318.5, 0.22, 0.003, 0.07, 'warm'), tone(2637, 0.16, 0.003, 0.04)))
write('heal', mix(tone(659.3, 0.18, 0.005, 0.07, 'warm'), tone(987.8, 0.22, 0.005, 0.08, 'warm'), offsets=[0, 0.14]))
write('shield_on', mix(tone(523.3, 0.32, 0.02, 0.12, 'warm', bend=0.5), tone(1046.5, 0.3, 0.03, 0.1, bend=0.5)))
write('shield_block', mix(tone(1568, 0.14, 0.002, 0.04), tone(784, 0.12, 0.002, 0.04, 'warm')))
write('shield_off', tone(880, 0.26, 0.01, 0.09, 'warm', bend=-0.45))
write('overdrive_on', mix(tone(698.5, 0.32, 0.01, 0.12, 'warm', bend=0.8), noise(0.25, 1500, 5000, 0.02, 0.1)))
write('overdrive_off', tone(1174.7, 0.26, 0.01, 0.09, 'warm', bend=-0.5))
# --- hits / enemies ---
write('robot_hit', mix(tone(140, 0.24, 0.002, 0.07, bend=-0.4), noise(0.08, 2500, 800, 0.001, 0.025)))
write('creep_attack', noise(0.24, 600, 3200, 0.03, 0.08))
write('boss_warn', mix(tone(392, 0.2, 0.01, 0.12, 'warm'), tone(523.3, 0.22, 0.01, 0.12, 'warm'), offsets=[0, 0.2]))
write('boss_strike', mix(tone(95, 0.42, 0.003, 0.14, bend=-0.3), noise(0.18, 1500, 400, 0.002, 0.06)))
# --- results ---
write('lose', mix(tone(392, 0.3, 0.01, 0.15, 'warm'), tone(329.6, 0.3, 0.01, 0.15, 'warm'), tone(261.6, 0.42, 0.01, 0.22, 'warm'),
                  offsets=[0, 0.2, 0.4]))
write('level_win', mix(*[tone(f, 0.35, 0.006, 0.16, 'warm') for f in (523.3, 659.3, 784.0, 1046.5)], offsets=[0, 0.14, 0.28, 0.45]))
write('campaign_win', mix(*[tone(f, 0.5, 0.006, 0.25, 'warm') for f in (523.3, 659.3, 784.0, 1046.5, 1318.5, 1568.0)],
                          offsets=[0, 0.16, 0.32, 0.5, 0.7, 0.95]))
# --- ending (beds are as long as their part of the scene, no baked fades: the Timeline eases fade them) ---
write('ending_charge', mix(*[tone(f, 5.0, 0.6, 30.0, 'warm', bend=0.25) for f in (220, 330, 440)]))
write('ending_ignition', fade(mix(tone(110, 0.9, 0.01, 0.3, bend=-0.2), noise(0.9, 800, 2200, 0.05, 0.4)), 0.02, 0.3))
write('ending_engine', mix(noise(9.5, 900, 1400, 0.05, 60.0), [0.25 * v for v in tone(160, 9.5, 0.05, 60.0, 'warm')]))
# --- ambience beds (seamless loops, very soft; no attack, or the crossfade would dip each loop) ---
write('ambience_acid', loop(mix(noise(9.0, 350, 450, 0.001, 60.0),
                                *[[0.25 * v for v in tone(rng.uniform(900, 1500), 0.08, 0.005, 0.02)] for _ in range(10)],
                                offsets=[0.0] + [rng.uniform(0.5, 8.0) for _ in range(10)]), 1.0))
write('ambience_station', loop(mix(noise(9.0, 250, 320, 0.001, 60.0), [0.15 * v for v in tone(220, 9.0, 0.001, 60.0, 'warm', vibrato=0.002)]), 1.0))
print('SFX_OK')
