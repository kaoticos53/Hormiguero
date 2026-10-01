#!/usr/bin/env python3
"""Render an AntSim replay.json as a spatial density map to judge foraging strategy.

Usage:
    python3 tools/replay_map.py replay.json [--grid 80x40] [--out map.png] [--every 4]

Accumulates all ant positions across sampled frames into a grid (optionally every Nth
frame), marks the nest (N) and food piles (F), and prints an ASCII heat map. With
--out, also writes a PNG (requires matplotlib).
"""
import argparse
import base64
import json
import math
import sys


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("replay")
    ap.add_argument("--grid", default="80x40", help="output grid WxH")
    ap.add_argument("--out", default=None, help="write PNG here (needs matplotlib)")
    ap.add_argument("--every", type=int, default=4, help="use every Nth frame")
    args = ap.parse_args()

    with open(args.replay, encoding="utf-8") as f:
        data = json.load(f)

    w, h = (int(v) for v in args.grid.lower().split("x"))
    cfg = data["Config"]
    W, H = float(cfg["Width"]), float(cfg["Height"])
    nest = (W / 2.0, H / 2.0)

    # Torus-aware binning: every point is expressed as its offset from the nest along the
    # shortest toroidal path, so a cluster at the world seam shows up as ONE cluster here
    # instead of four ghost clusters in the corners.
    def unwrap(v, c, size):
        d = (v - c) % size
        if d > size * 0.5:
            d -= size
        return d

    grid = [[0.0] * w for _ in range(h)]
    frames = data["Frames"]
    used = 0
    for i, fr in enumerate(frames):
        if i % args.every != 0:
            continue
        xs, ys = fr["X"], fr["Y"]
        for x, y in zip(xs, ys):
            dx = unwrap(x, nest[0], W)
            dy = unwrap(y, nest[1], H)
            cx = min(w - 1, max(0, int((dx + W / 2) / W * w)))
            cy = min(h - 1, max(0, int((dy + H / 2) / H * h)))
            grid[cy][cx] += 1.0
        used += 1

    # Log-ish normalization so one hot corridor doesn't flatten everything else.
    vmax = max(max(row) for row in grid) or 1.0
    ramp = " .:-=+*#%@"

    def cell_char(v):
        t = math.log1p(v) / math.log1p(vmax)
        return ramp[min(len(ramp) - 1, int(t * (len(ramp) - 1)))]

    lines = []
    for cy in range(h):
        row = []
        for cx in range(w):
            # Cell centre in nest-centred coordinates.
            px = (cx + 0.5) * W / w - W / 2
            py = (cy + 0.5) * H / h - H / 2
            if px * px + py * py <= (cfg["NestRadius"]) ** 2:
                row.append("N")
            else:
                row.append(cell_char(grid[cy][cx]))
        lines.append("".join(row))

    # Mark food piles (torus-aware: nearest representation to the nest).
    for f in data.get("FoodSources", []):
        dx = unwrap(f["X"], nest[0], W)
        dy = unwrap(f["Y"], nest[1], H)
        cx = min(w - 1, max(0, int((dx + W / 2) / W * w)))
        cy = min(h - 1, max(0, int((dy + H / 2) / H * h)))
        row = list(lines[cy])
        row[cx] = "F"
        lines[cy] = "".join(row)

    print(f"Replay: {len(frames)} frames sampled, {used} used | world {W:.0f}x{H:.0f} | "
          f"delivered {data['FoodDelivered']} | species {data.get('SpeciesId')}")
    print(f"grid {w}x{h}, nest-centred (torus unwrapped) | N = nest, F = food pile, denser = brighter")
    print()
    for line in lines:
        print(line)
    print()

    # Delivery curve at 0/25/50/75/100%.
    n = len(frames)
    pts = [0, n // 4, n // 2, 3 * n // 4, n - 1]
    curve = ", ".join(f"t{fr['Tick']}:{fr.get('Delivered', 0)}" for fr in (frames[p] for p in pts))
    print(f"Delivery curve: {curve}")

    if args.out:
        try:
            import matplotlib

            matplotlib.use("Agg")
            import matplotlib.pyplot as plt
            import numpy as np

            arr = np.array(grid)
            fig, ax = plt.subplots(figsize=(w / 12, h / 12), dpi=120)
            ax.imshow(arr, cmap="viridis", origin="upper", interpolation="nearest")
            ax.set_title(f"AntSim replay density — delivered {data['FoodDelivered']}")
            ax.axis("off")
            fig.savefig(args.out, bbox_inches="tight")
            print(f"PNG written: {args.out}")
        except ImportError:
            print("matplotlib not available; skipping PNG.", file=sys.stderr)


if __name__ == "__main__":
    main()