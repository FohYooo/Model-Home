"""Synthesise every Model Home sound (plan Task 6) into Assets/ModelHome/Audio/.

Run: python3 -I Tools/audio/synth.py
Pure numpy + stdlib `wave`; deterministic (fixed seeds) so re-runs are byte-identical.
Loops are built from FFT-shaped (circular) noise and whole-cycle sines, so they
wrap seamlessly.
"""
import pathlib
import wave

import numpy as np

ROOT = pathlib.Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets" / "ModelHome" / "Audio"
RATE = 48_000


# ---------- helpers ----------

def t_axis(seconds):
    return np.arange(int(round(seconds * RATE))) / RATE


def band_gain(freqs, lo=None, hi=None, order=2):
    g = np.ones_like(freqs)
    f = np.maximum(freqs, 1e-6)
    if lo:
        g /= np.sqrt(1 + (lo / f) ** (2 * order))
    if hi:
        g /= np.sqrt(1 + (f / hi) ** (2 * order))
    return g


def shaped_noise(n, rng, lo=None, hi=None, tilt=0.0, order=2):
    """Circular noise of n samples with a band-pass and a 1/f^tilt spectral slope."""
    spec = np.fft.rfft(rng.standard_normal(n))
    freqs = np.fft.rfftfreq(n, 1 / RATE)
    gain = band_gain(freqs, lo, hi, order)
    if tilt:
        gain *= 1 / np.maximum(freqs, 20.0) ** (tilt / 2)
    gain[0] = 0
    out = np.fft.irfft(spec * gain, n)
    return out / (np.std(out) + 1e-12)


def filt(x, lo=None, hi=None, order=2):
    """Non-circular band filter for one-shots (zero-padded FFT)."""
    n = len(x)
    pad = 1 << int(np.ceil(np.log2(n * 2)))
    spec = np.fft.rfft(x, pad)
    freqs = np.fft.rfftfreq(pad, 1 / RATE)
    return np.fft.irfft(spec * band_gain(freqs, lo, hi, order), pad)[:n]


def env_ad(n, attack, decay):
    t = np.arange(n) / RATE
    a = np.clip(t / max(attack, 1e-4), 0, 1)
    d = np.exp(-np.maximum(t - attack, 0) / decay)
    return a * d


def ring(t, freq, decay, phase=0.0):
    return np.sin(2 * np.pi * freq * t + phase) * np.exp(-t / decay)


def normalise(x, peak):
    return x * (peak / (np.max(np.abs(x)) + 1e-12))


def fade_edges(x, seconds=0.004):
    n = int(seconds * RATE)
    if n and len(x) > 2 * n:
        ramp = np.linspace(0, 1, n)
        x[:n] *= ramp
        x[-n:] *= ramp[::-1]
    return x


def write(name, x):
    OUT.mkdir(parents=True, exist_ok=True)
    pcm = np.clip(np.round(x * 32767), -32768, 32767).astype("<i2")
    with wave.open(str(OUT / f"{name}.wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(pcm.tobytes())


# ---------- loops ----------

def hum_roomtone():
    rng = np.random.default_rng(1)
    t = t_axis(10.0)
    room = shaped_noise(len(t), rng, lo=25, hi=700, tilt=1.6)
    mains = sum(a * np.sin(2 * np.pi * f * t) for f, a in ((50, 1.0), (100, 0.6), (150, 0.35)))
    x = room * 0.12 + mains * 0.12 * 0.0316  # harmonics ~ -30 dB under the room tone
    return normalise(x, 0.30)


def fridge_buzz():
    rng = np.random.default_rng(2)
    t = t_axis(6.0)
    # phase k*pi puts the sawtooth's jump mid-loop, so the loop seam sits on the gentle ramp
    buzz = sum(np.sin(2 * np.pi * 120 * k * t + k * np.pi) / k for k in range(1, 13))
    am = 0.75 + 0.25 * np.sin(2 * np.pi * 0.5 * t)  # 3 whole cycles in 6 s
    rumble = shaped_noise(len(t), rng, lo=30, hi=200)
    x = buzz * am * 0.5 + rumble * 0.25
    return normalise(x, 0.40)


def tick(rng, freq, n):
    t = np.arange(n) / RATE
    burst = filt(rng.standard_normal(n), lo=2500, hi=7000) * env_ad(n, 0.0005, 0.006)
    return burst * 0.6 + ring(t, freq, 0.015) * 0.5 + ring(t, freq * 2.7, 0.006) * 0.2


def clock_tick():
    rng = np.random.default_rng(3)
    x = np.zeros(int(2.0 * RATE))
    n = int(0.08 * RATE)
    start = 16  # keep sample 0 at silence for a clean loop seam
    x[start:start + n] += tick(rng, 2200, n)
    x[RATE + start:RATE + start + n] += tick(rng, 1600, n) * 0.85
    return normalise(x, 0.50)


def breath(rng, seconds, brightness, shape="in"):
    n = int(seconds * RATE)
    noise = filt(rng.standard_normal(n), lo=350 * brightness, hi=2600 * brightness, order=2)
    formant = filt(rng.standard_normal(n), lo=900, hi=1400, order=3) * 0.5
    ph = np.linspace(0, 1, n)
    if shape == "in":
        env = np.sin(np.pi * np.clip(ph / 0.9, 0, 1) / 2) ** 2 * np.clip((1 - ph) / 0.1, 0, 1)
    else:
        env = np.clip(ph / 0.08, 0, 1) * (1 - ph) ** 1.6
    return (noise + formant) * env


def breath_slow():
    rng = np.random.default_rng(4)
    inhale = breath(rng, 1.8, 1.1, "in")
    exhale = breath(rng, 2.2, 0.8, "out")
    x = np.concatenate([inhale, exhale, np.zeros(int(0.5 * RATE))])
    x = fade_edges(x, 0.01)
    return normalise(x, 0.45)


# ---------- one-shots ----------

def inhale():
    rng = np.random.default_rng(5)
    return normalise(fade_edges(breath(rng, 1.6, 1.15, "in"), 0.01), 0.50)


def step(seed, carpet):
    rng = np.random.default_rng(seed)
    dur = 0.30 + rng.uniform(0, 0.08)
    n = int(dur * RATE)
    t = np.arange(n) / RATE
    if carpet:
        body = filt(rng.standard_normal(n), lo=60, hi=900) * env_ad(n, 0.004, 0.06 + rng.uniform(0, 0.03))
        thump = ring(t, 70 + rng.uniform(0, 25), 0.035) * 0.6
        scuff = filt(rng.standard_normal(n), lo=200, hi=700) * env_ad(n, 0.03, 0.05) * 0.3
        x = body + thump + np.roll(scuff, int(rng.uniform(0.04, 0.09) * RATE))
        peak = 0.35
    else:
        body = filt(rng.standard_normal(n), lo=150, hi=4000) * env_ad(n, 0.002, 0.04 + rng.uniform(0, 0.03))
        thump = ring(t, 95 + rng.uniform(0, 30), 0.04) * 0.8
        creak = ring(t, rng.uniform(300, 520), 0.05) * env_ad(n, 0.01, 0.05) * rng.uniform(0.05, 0.25)
        heel = np.zeros(n)
        off = int(rng.uniform(0.05, 0.11) * RATE)
        heel[off:] = (filt(rng.standard_normal(n - off), lo=300, hi=3000) * env_ad(n - off, 0.001, 0.02))
        x = body + thump + creak + heel * 0.6
        peak = 0.55
    return normalise(fade_edges(x), peak)


def click_flashlight():
    rng = np.random.default_rng(6)
    n = int(0.15 * RATE)
    x = np.zeros(n)
    for start, f in ((0.002, 3100), (0.032, 1250)):
        m = int(0.03 * RATE)
        s = int(start * RATE)
        tt = np.arange(m) / RATE
        x[s:s + m] += filt(rng.standard_normal(m), lo=1500, hi=9000) * env_ad(m, 0.0003, 0.003) + ring(tt, f, 0.006) * 0.5
    return normalise(fade_edges(x), 0.55)


def click_switch():
    rng = np.random.default_rng(7)
    n = int(0.12 * RATE)
    t = np.arange(n) / RATE
    x = filt(rng.standard_normal(n), lo=1200, hi=10000) * env_ad(n, 0.0002, 0.004) + ring(t, 180, 0.02) * 0.4 + ring(t, 2600, 0.008) * 0.3
    return normalise(fade_edges(x), 0.60)


def latch():
    rng = np.random.default_rng(8)
    n = int(0.30 * RATE)
    t = np.arange(n) / RATE
    metal = sum(ring(t, f, 0.08, p) * a for f, a, p in ((2800, 0.5, 0.0), (4100, 0.35, 1.1), (6300, 0.2, 2.3)))
    x = filt(rng.standard_normal(n), lo=1000, hi=8000) * env_ad(n, 0.0003, 0.005) + metal * env_ad(n, 0.0005, 0.08)
    return normalise(fade_edges(x), 0.55)


def lock_clunk():
    rng = np.random.default_rng(9)
    n = int(0.50 * RATE)
    t = np.arange(n) / RATE
    scrape = filt(rng.standard_normal(n), lo=600, hi=3500) * np.clip(t / 0.01, 0, 1) * (t < 0.07) * 0.35
    hit_at = int(0.07 * RATE)
    th = np.arange(n - hit_at) / RATE
    hit = np.zeros(n)
    hit[hit_at:] = ring(th, 140, 0.06) * 0.9 + ring(th, 1800, 0.03) * 0.35 + filt(rng.standard_normal(n - hit_at), lo=200, hi=5000) * env_ad(n - hit_at, 0.0005, 0.01) * 0.5
    return normalise(fade_edges(scrape + hit), 0.70)


def drone_ending():
    rng = np.random.default_rng(10)
    t = t_axis(12.0)
    tones = sum(a * np.sin(2 * np.pi * f * t + p) for f, a, p in (
        (41.0, 1.0, 0.0), (43.5, 0.9, 1.3), (55.0, 0.7, 2.1), (82.0, 0.35, 0.4), (110.0, 0.2, 2.9)))
    dark = shaped_noise(len(t), rng, lo=40, hi=250)
    bright = shaped_noise(len(t), rng, lo=400, hi=2500)
    ramp = (t / t[-1]) ** 2
    swell = dark * (0.4 + 0.3 * ramp) + bright * 0.5 * ramp
    env = np.clip(t / 3.0, 0, 1)
    x = (tones * 0.6 + swell * 0.35) * env
    return normalise(fade_edges(x, 0.01), 0.80)


def main():
    sounds = {
        "hum_roomtone": hum_roomtone(),
        "fridge_buzz": fridge_buzz(),
        "clock_tick": clock_tick(),
        "breath_slow": breath_slow(),
        "inhale": inhale(),
        "click_flashlight": click_flashlight(),
        "click_switch": click_switch(),
        "latch": latch(),
        "lock_clunk": lock_clunk(),
        "drone_ending": drone_ending(),
    }
    for i in range(1, 5):
        sounds[f"step_wood_{i:02d}"] = step(100 + i, carpet=False)
        sounds[f"step_carpet_{i:02d}"] = step(200 + i, carpet=True)
    for name, data in sounds.items():
        write(name, data)
    print(f"wrote {len(sounds)} files to {OUT}")


if __name__ == "__main__":
    main()
