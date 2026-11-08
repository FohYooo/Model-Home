"""Contract test for the synthesised Model Home audio (plan Task 6).

Run: python3 -I Tools/audio/test_synth.py
Exits non-zero and lists every failure if any file breaks the contract.
"""
import itertools
import pathlib
import sys
import wave

import numpy as np

ROOT = pathlib.Path(__file__).resolve().parents[2]
AUDIO = ROOT / "Assets" / "ModelHome" / "Audio"
RATE = 48_000

# name -> expected duration in seconds (None = short one-shot, must be < 1 s)
FILES = {
    "hum_roomtone": 10.0,
    "fridge_buzz": 6.0,
    "clock_tick": 2.0,
    "inhale": 1.6,
    "breath_slow": 4.5,
    "drone_ending": 12.0,
    "click_flashlight": None,
    "click_switch": None,
    "latch": None,
    "lock_clunk": None,
    **{f"step_wood_{i:02d}": None for i in range(1, 5)},
    **{f"step_carpet_{i:02d}": None for i in range(1, 5)},
}
LOOPS = {"hum_roomtone", "fridge_buzz", "clock_tick", "breath_slow"}


def load(name):
    with wave.open(str(AUDIO / f"{name}.wav"), "rb") as w:
        assert_eq = (w.getnchannels(), w.getframerate(), w.getsampwidth())
        frames = w.readframes(w.getnframes())
    data = np.frombuffer(frames, dtype="<i2").astype(np.float64) / 32768.0
    return assert_eq, data


def main():
    failures = []
    loaded = {}
    for name, dur in FILES.items():
        path = AUDIO / f"{name}.wav"
        if not path.exists():
            failures.append(f"{name}: missing")
            continue
        (channels, rate, width), data = load(name)
        loaded[name] = data
        if channels != 1:
            failures.append(f"{name}: {channels} channels, want mono")
        if rate != RATE:
            failures.append(f"{name}: {rate} Hz, want {RATE}")
        if width != 2:
            failures.append(f"{name}: {width * 8}-bit, want 16-bit")
        seconds = len(data) / RATE
        if dur is None:
            if not 0.05 <= seconds < 1.0:
                failures.append(f"{name}: one-shot is {seconds:.2f}s, want 0.05-1.0s")
        elif abs(seconds - dur) > dur * 0.02:
            failures.append(f"{name}: {seconds:.2f}s, want {dur}s +-2%")
        peak = float(np.max(np.abs(data))) if len(data) else 0.0
        if peak > 0.9:
            failures.append(f"{name}: peak {peak:.3f} > 0.9 FS")
        if peak < 0.01:
            failures.append(f"{name}: silent (peak {peak:.4f})")
        if name in LOOPS and len(data) and abs(data[0] - data[-1]) >= 0.01:
            failures.append(f"{name}: loop seam |first-last| = {abs(data[0] - data[-1]):.4f}")

    for kind in ("wood", "carpet"):
        steps = [loaded.get(f"step_{kind}_{i:02d}") for i in range(1, 5)]
        if any(s is None for s in steps):
            continue
        for (i, a), (j, b) in itertools.combinations(enumerate(steps, 1), 2):
            n = min(len(a), len(b))
            corr = float(np.corrcoef(a[:n], b[:n])[0, 1])
            if corr >= 0.9:
                failures.append(f"step_{kind}_{i:02d} vs _{j:02d}: correlation {corr:.3f} >= 0.9")

    if failures:
        print(f"FAIL ({len(failures)} problems)")
        for f in failures:
            print("  -", f)
        sys.exit(1)
    print(f"PASS ({len(FILES)} files)")


if __name__ == "__main__":
    main()
