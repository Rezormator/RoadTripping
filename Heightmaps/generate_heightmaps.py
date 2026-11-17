"""RoadTripping heightmap generator.

Builds Unity terrain tiles (TILE x TILE units, RES x RES heightmap) along the
road route from the concept map. The map is centred on the world origin.

Rules:
  * a road segment is SEGMENT_LENGTH units of road (3 tiles on a straight);
  * sector lengths in segments are in SEGMENTS;
  * a tile is generated wherever the road comes closer than EDGE_DISTANCE
    to it, so the road never runs along the edge of the world;
  * in sectors 2..9 the first and the last segment are straight, and the
    ground around the last one (band of LOOP_HALF_WIDTH from the road) is an
    exact copy of the first one, turned by any angle and shifted in height.
    The transform for the teleport is written to loops.json.

Outputs in output/:
  tiles/tile_xNNN_zNNN.raw   16-bit little-endian RES x RES heightmaps, row 0 = south (lowest Z)
  manifest.csv               tile placement, nearest sector and segment
  settings.json              tile size, resolution, terrain height
  loops.json, loops.csv      first/last segment of each looping sector and the teleport
  road.csv                   road centre line (point every ROAD_KNOT_STEP) for a spline
  overview_heightmap_16bit.png, overview_relief*.png   previews

Usage:  python generate_heightmaps.py                 all sectors
        python generate_heightmaps.py --sectors 3,4   only these sectors (no overview)
Needs numpy and Pillow.
"""

import csv
import json
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
OUT = ROOT / "output"
TILES_DIR = OUT / "tiles"

TILE = 200.0
RES = 65
TERRAIN_HEIGHT = 250.0
SEGMENT_LENGTH = 200.0
SEGMENTS = [5, 10, 12, 11, 13, 12, 14, 13, 15, 5]
LOOP_SECTORS = range(1, 9)          # 0-based: sectors 2..9
SEED = 1337

EDGE_DISTANCE = 40.0
ROAD_HALF_WIDTH = 6.0
SHOULDER = 40.0
MAX_GRADE = 0.045
ROAD_SMOOTH_SIGMA = 200.0
ROAD_KNOT_STEP = 20.0
LOOP_HALF_WIDTH = 60.0
LOOP_SIDE_FADE = 60.0
LOOP_END_FADE = 40.0
STRAIGHT_MARGIN = 30.0
STRAIGHT_FADE = 90.0
PARAM_SIGMA = 150.0
ROAD_PARAM_SIGMA = 250.0
STEP = 1.0

# base height, hills amplitude, ridges amplitude, terrace strength
BIOMES = [
    (20.0, 4.0, 0.0, 0.0),
    (50.0, 14.0, 25.0, 0.0),
    (85.0, 18.0, 75.0, 0.0),
    (120.0, 10.0, 0.0, 0.8),
    (118.0, 5.0, 0.0, 0.3),
    (75.0, 12.0, 8.0, 0.0),
    (42.0, 16.0, 0.0, 0.0),
    (24.0, 4.0, 0.0, 0.0),
    (55.0, 18.0, 12.0, 0.0),
    (62.0, 1.5, 0.0, 0.0),
]
BIOME_NAMES = ["Lowland start", "Foothills", "Mountains", "Mesa plateau", "High plateau",
               "Descent", "Rolling hills", "Lake valley", "Rising hills", "Finale plain"]

PATH_SVG = [
    (50, 630, 85, 625, 105, 600, 110, 570),
    (110, 570, 115, 540, 95, 515, 115, 490),
    (115, 490, 135, 465, 170, 470, 180, 440),
    (180, 440, 190, 413, 189, 388, 205, 360),
    (205, 360, 213, 335, 235, 320, 265, 315),
    (265, 315, 295, 311, 320, 292, 360, 302),
    (360, 302, 400, 297, 425, 320, 440, 350),
    (440, 350, 458, 380, 458, 428, 475, 460),
    (475, 460, 490, 510, 525, 575, 575, 580),
    (575, 580, 620, 585, 632, 545, 622, 505),
    (622, 505, 610, 468, 614, 425, 600, 390),
    (600, 390, 594, 355, 565, 335, 560, 300),
    (560, 300, 555, 265, 590, 245, 592, 210),
    (592, 210, 594, 188, 578, 172, 585, 150),
    (585, 150, 582, 110, 595, 60, 620, 52),
    (620, 52, 623.3, 52, 626.7, 52, 630, 52),
]

SEG_START = np.cumsum([0] + SEGMENTS)
TOTAL_LENGTH = SEG_START[-1] * SEGMENT_LENGTH


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


# ---------------------------------------------------------------- route

def svg_points(per_curve=800):
    pts = []
    for (x0, y0, x1, y1, x2, y2, x3, y3) in PATH_SVG:
        t = np.linspace(0.0, 1.0, per_curve, endpoint=False)
        u = 1.0 - t
        x = u**3 * x0 + 3 * u * u * t * x1 + 3 * u * t * t * x2 + t**3 * x3
        y = u**3 * y0 + 3 * u * u * t * y1 + 3 * u * t * t * y2 + t**3 * y3
        pts.append(np.stack([x, y], 1))
    pts.append(np.array([[PATH_SVG[-1][6], PATH_SVG[-1][7]]]))
    return np.concatenate(pts)


def arclen(pts):
    return np.concatenate([[0.0], np.cumsum(np.linalg.norm(np.diff(pts, axis=0), axis=1))])


def resample(pts, step):
    cum = arclen(pts)
    s = np.arange(0.0, cum[-1] + 1e-9, step)
    return np.stack([np.interp(s, cum, pts[:, 0]), np.interp(s, cum, pts[:, 1])], 1), s


def at(pts, s_arr, s):
    return np.array([np.interp(s, s_arr, pts[:, 0]), np.interp(s, s_arr, pts[:, 1])])


def straighten(pts, s):
    """Make the road straight around every sector boundary between looping sectors."""
    out = pts.copy()
    for b_seg in SEG_START[1:-1]:
        b = b_seg * SEGMENT_LENGTH
        sa = b - SEGMENT_LENGTH - STRAIGHT_MARGIN
        sb = b + SEGMENT_LENGTH + STRAIGHT_MARGIN
        A, B = at(pts, s, sa), at(pts, s, sb)
        line = A[None] + ((s - sa) / (sb - sa))[:, None] * (B - A)[None]
        w = np.where((s >= sa) & (s <= sb), 1.0,
                     np.where(s < sa, smoothstep(sa - STRAIGHT_FADE, sa, s), 1.0 - smoothstep(sb, sb + STRAIGHT_FADE, s)))
        out = out + w[:, None] * (line - out)
    return out


def build_route():
    svg = svg_points()
    base = np.stack([svg[:, 0] - 30.0, 650.0 - svg[:, 1]], 1)
    k = TOTAL_LENGTH / arclen(base)[-1]
    for _ in range(12):
        pts, s = resample(base * k, STEP)
        pts = straighten(pts, s)
        pts, s = resample(pts, STEP)
        k *= TOTAL_LENGTH / s[-1]
    pts, s = resample(base * k, STEP)
    pts = straighten(pts, s)
    pts, s = resample(pts, STEP)
    pts, s = pts[s <= TOTAL_LENGTH + 0.5], s[s <= TOTAL_LENGTH + 0.5]
    return pts, s, k


# ---------------------------------------------------------------- noise

def _hash(ix, iz, seed):
    h = (ix * 374761393 + iz * 668265263 + seed * 2147483647) & 0xFFFFFFFF
    h = ((h ^ (h >> 13)) * 1274126177) & 0xFFFFFFFF
    h = h ^ (h >> 16)
    return h.astype(np.float64) / 4294967295.0


def pnoise(x, z, seed):
    """Gradient (Perlin style) noise, roughly -1..1."""
    xi = np.floor(x)
    zi = np.floor(z)
    xf = x - xi
    zf = z - zi
    xi = xi.astype(np.int64)
    zi = zi.astype(np.int64)

    def g(ix, iz, fx, fz):
        a = _hash(ix, iz, seed) * (2.0 * math.pi)
        return np.cos(a) * fx + np.sin(a) * fz

    n00 = g(xi, zi, xf, zf)
    n10 = g(xi + 1, zi, xf - 1.0, zf)
    n01 = g(xi, zi + 1, xf, zf - 1.0)
    n11 = g(xi + 1, zi + 1, xf - 1.0, zf - 1.0)
    u = xf * xf * xf * (xf * (xf * 6.0 - 15.0) + 10.0)
    v = zf * zf * zf * (zf * (zf * 6.0 - 15.0) + 10.0)
    a = n00 + (n10 - n00) * u
    b = n01 + (n11 - n01) * u
    return (a + (b - a) * v) * 1.414


def fbm(x, z, octaves, seed):
    total = np.zeros_like(x)
    amp, freq, norm = 1.0, 1.0, 0.0
    for o in range(octaves):
        total += amp * pnoise(x * freq + o * 17.31, z * freq - o * 9.17, seed + o)
        norm += amp
        amp *= 0.5
        freq *= 2.03
    return total / norm


def ridged(x, z, octaves, seed):
    """Ridges with rounded crests: |n| is replaced by a soft absolute value."""
    total = np.zeros_like(x)
    amp, freq, norm = 1.0, 1.0, 0.0
    for o in range(octaves):
        p = pnoise(x * freq + o * 5.7, z * freq + o * 3.3, seed + 100 + o)
        n = (1.0 - np.sqrt(p * p + 0.03)) / (1.0 - math.sqrt(0.03))
        total += amp * n * n
        norm += amp
        amp *= 0.5
        freq *= 2.1
    return total / norm


def base_height(x, z, p):
    base, hills, ridges, terrace = p[..., 0], p[..., 1], p[..., 2], p[..., 3]
    h = base + 10.0 * fbm(x / 1600.0, z / 1600.0, 3, SEED)
    h = h + hills * fbm(x / 260.0, z / 260.0, 3, SEED + 10)
    h = h + ridges * ridged(x / 320.0, z / 320.0, 3, SEED + 20)
    h = h + 0.3 * fbm(x / 60.0, z / 60.0, 2, SEED + 30)
    step = 18.0
    t = h / step
    terr = (np.floor(t) + smoothstep(0.2, 0.8, t - np.floor(t))) * step
    return h + (terr - h) * terrace


def gaussian_smooth(values, step, sigma):
    radius = int(3 * sigma / step)
    kx = np.arange(-radius, radius + 1) * step
    kern = np.exp(-kx**2 / (2 * sigma**2))
    kern /= kern.sum()
    if values.ndim == 1:
        return np.convolve(np.pad(values, radius, mode="edge"), kern, mode="valid")
    pad = np.pad(values, ((radius, radius), (0, 0)), mode="edge")
    return np.stack([np.convolve(pad[:, i], kern, mode="valid") for i in range(values.shape[1])], 1)


def sector_of_s(s):
    seg = np.floor(np.asarray(s) / SEGMENT_LENGTH)
    return np.clip(np.searchsorted(SEG_START, seg, side="right") - 1, 0, len(SEGMENTS) - 1)


# ---------------------------------------------------------------- world

class World:
    def __init__(self, road, s):
        self.road, self.s = road, s
        params = np.array([BIOMES[i] for i in sector_of_s(s)])
        self.road_params = gaussian_smooth(params, STEP, ROAD_PARAM_SIGMA)
        self.sub = road[::10]
        self.sub_params = self.road_params[::10]
        self.seg_a, self.seg_b, self.seg_s = road[:-1:2], road[2::2], s[:-1:2]
        self.loops = []
        self._road_profile()

    def _road_profile(self):
        rh = base_height(self.road[:, 0], self.road[:, 1], self.road_params)
        rh = gaussian_smooth(rh, STEP, ROAD_SMOOTH_SIGMA)
        fixed = np.zeros(len(rh), bool)
        self._clamp(rh, fixed)
        L = SEGMENT_LENGTH
        n = int(round(L / STEP))
        for sec in LOOP_SECTORS:
            s1 = SEG_START[sec] * L
            s2 = SEG_START[sec + 1] * L - L
            i1, i2 = int(round(s1 / STEP)), int(round(s2 / STEP))
            first = rh[i1:i1 + n + 1].copy()
            # the copy must end exactly where the next sector's road starts
            dy = float(rh[i2 + n] - first[-1])
            rh[i2:i2 + n + 1] = first + dy
            fixed[i1:i1 + n + 1] = True
            fixed[i2:i2 + n + 1] = True
            self.loops.append({"sector": sec + 1, "s_first": s1, "s_last": s2, "dy": dy})
        self._clamp(rh, fixed)
        self.rh = rh
        print(f"road profile: max grade {np.abs(np.diff(rh)).max() / STEP * 100:.1f}%")
        for lp in self.loops:
            a1, b1 = at(self.road, self.s, lp["s_first"]), at(self.road, self.s, lp["s_first"] + L)
            a2, b2 = at(self.road, self.s, lp["s_last"]), at(self.road, self.s, lp["s_last"] + L)
            d1 = (b1 - a1) / np.linalg.norm(b1 - a1)
            d2 = (b2 - a2) / np.linalg.norm(b2 - a2)
            lp.update(a1=a1, b1=b1, a2=a2, b2=b2, d1=d1, d2=d2,
                      c1=(a1 + b1) / 2, c2=(a2 + b2) / 2,
                      angle=math.atan2(d2[1], d2[0]) - math.atan2(d1[1], d1[0]))

    @staticmethod
    def _clamp(rh, fixed):
        lim = MAX_GRADE * STEP
        for i in range(1, len(rh)):
            if not fixed[i]:
                rh[i] = min(max(rh[i], rh[i - 1] - lim), rh[i - 1] + lim)
        for i in range(len(rh) - 2, -1, -1):
            if not fixed[i]:
                rh[i] = min(max(rh[i], rh[i + 1] - lim), rh[i + 1] + lim)

    def natural_height(self, X, Z):
        """Ground with the road cut in, before loop copies."""
        shape = X.shape
        P = np.stack([X.ravel(), Z.ravel()], 1)
        lo, hi = P.min(0), P.max(0)

        cut = 3.0 * PARAM_SIGMA
        near = np.all((self.sub > lo - cut) & (self.sub < hi + cut), axis=1)
        R, PR = self.sub[near], self.sub_params[near]
        params = np.empty((len(P), 4))
        for c0 in range(0, len(P), 8192):
            Q = P[c0:c0 + 8192]
            d2 = ((Q[:, None, :] - R[None]) ** 2).sum(-1)
            w = np.exp(-d2 / (2 * PARAM_SIGMA**2))
            w[d2 > cut * cut] = 0.0
            params[c0:c0 + 8192] = (w @ PR) / np.maximum(w.sum(1), 1e-12)[:, None]
        ground = base_height(P[:, 0], P[:, 1], params)

        m = ROAD_HALF_WIDTH + SHOULDER + 10.0
        sel = (np.all((self.seg_a > lo - m) & (self.seg_a < hi + m), axis=1) |
               np.all((self.seg_b > lo - m) & (self.seg_b < hi + m), axis=1))
        A, B, S0 = self.seg_a[sel], self.seg_b[sel], self.seg_s[sel]
        dist = np.full(len(P), np.inf)
        snear = np.zeros(len(P))
        if len(A):
            AB = B - A
            ab2 = np.maximum((AB**2).sum(1), 1e-9)
            for c0 in range(0, len(P), 8192):
                Q = P[c0:c0 + 8192]
                t = np.clip(((Q[:, None, :] - A[None]) * AB[None]).sum(-1) / ab2[None], 0.0, 1.0)
                proj = A[None] + t[..., None] * AB[None]
                dd = ((Q[:, None, :] - proj) ** 2).sum(-1)
                j = dd.argmin(1)
                r = np.arange(len(Q))
                dist[c0:c0 + 8192] = np.sqrt(dd[r, j])
                snear[c0:c0 + 8192] = S0[j] + t[r, j] * np.sqrt(ab2[j])
        road_h = np.interp(snear, self.s, self.rh)
        blend = smoothstep(ROAD_HALF_WIDTH, ROAD_HALF_WIDTH + SHOULDER, dist)
        return (road_h + (ground - road_h) * blend).reshape(shape)

    def height(self, X, Z):
        h = self.natural_height(X, Z)
        for lp in self.loops:
            u = (X - lp["a2"][0]) * lp["d2"][0] + (Z - lp["a2"][1]) * lp["d2"][1]
            v = -(X - lp["a2"][0]) * lp["d2"][1] + (Z - lp["a2"][1]) * lp["d2"][0]
            w = (smoothstep(0.0, LOOP_END_FADE, u) * smoothstep(0.0, LOOP_END_FADE, SEGMENT_LENGTH - u) *
                 (1.0 - smoothstep(LOOP_HALF_WIDTH, LOOP_HALF_WIDTH + LOOP_SIDE_FADE, np.abs(v))))
            mask = w > 0
            if not mask.any():
                continue
            ca, sa = math.cos(-lp["angle"]), math.sin(-lp["angle"])
            dx, dz = X[mask] - lp["c2"][0], Z[mask] - lp["c2"][1]
            tx = ca * dx - sa * dz + lp["c1"][0]
            tz = sa * dx + ca * dz + lp["c1"][1]
            src = self.natural_height(tx, tz) + lp["dy"]
            h[mask] = h[mask] + (src - h[mask]) * w[mask]
        return h


def select_tiles(world):
    """Tiles the road comes within EDGE_DISTANCE of, plus the loop bands."""
    tiles = set()

    def mark(p, thr):
        for gx in range(int(math.floor((p[0] - thr) / TILE)), int(math.floor((p[0] + thr) / TILE)) + 1):
            for gz in range(int(math.floor((p[1] - thr) / TILE)), int(math.floor((p[1] + thr) / TILE)) + 1):
                dx = max(gx * TILE - p[0], 0.0, p[0] - (gx + 1) * TILE)
                dz = max(gz * TILE - p[1], 0.0, p[1] - (gz + 1) * TILE)
                if dx * dx + dz * dz < thr * thr:
                    tiles.add((gx, gz))

    for p in world.road[::5]:
        mark(p, EDGE_DISTANCE)
    band = LOOP_HALF_WIDTH + LOOP_SIDE_FADE
    for lp in world.loops:
        for key in ("s_first", "s_last"):
            for ds in np.arange(0.0, SEGMENT_LENGTH + 0.1, 5.0):
                mark(at(world.road, world.s, lp[key] + ds), band)
    return tiles


# ---------------------------------------------------------------- main

def main():
    TILES_DIR.mkdir(parents=True, exist_ok=True)
    for old in list(TILES_DIR.glob("*.raw")) + list(OUT.glob("overview_*.png")) + list(OUT.glob("preview_*.png")):
        old.unlink()

    road, s, k = build_route()
    world = World(road, s)
    tiles = select_tiles(world)

    gx0 = min(t[0] for t in tiles)
    gz0 = min(t[1] for t in tiles)
    shift = np.array([gx0 * TILE, gz0 * TILE])
    world.road = world.road - shift
    world.sub = world.sub - shift
    world.seg_a, world.seg_b = world.seg_a - shift, world.seg_b - shift
    for lp in world.loops:
        for key in ("a1", "b1", "a2", "b2", "c1", "c2"):
            lp[key] = lp[key] - shift
    tiles = sorted({(t[0] - gx0, t[1] - gz0) for t in tiles})

    # order tiles along the road
    centers = np.array([[(t[0] + 0.5) * TILE, (t[1] + 0.5) * TILE] for t in tiles])
    near_idx = np.array([np.argmin(((world.road[::5] - c) ** 2).sum(1)) for c in centers]) * 5
    tile_s = s[near_idx]
    order = np.argsort(tile_s, kind="stable")
    tiles = [tiles[i] for i in order]
    tile_s = tile_s[order]
    print(f"route scale {k:.2f}, road {s[-1] / 1000:.1f} km, {len(tiles)} tiles")
    only = [int(v) for v in sys.argv[sys.argv.index("--sectors") + 1].split(",")] if "--sectors" in sys.argv else None
    if only:
        keep = [i for i in range(len(tiles)) if int(sector_of_s(tile_s[i])) + 1 in only]
        tiles = [tiles[i] for i in keep]
        tile_s = tile_s[keep]
        print(f"only sectors {only}: {len(tiles)} tiles")

    heights = {}
    lin = np.linspace(0.0, TILE, RES)
    for n, (cx, cz) in enumerate(tiles):
        X, Z = np.meshgrid(cx * TILE + lin, cz * TILE + lin)
        heights[(cx, cz)] = world.height(X, Z)
        if (n + 1) % 50 == 0:
            print(f"tiles {n + 1}/{len(tiles)}", flush=True)

    # shared edges and corners identical
    for (cx, cz), h in heights.items():
        r = heights.get((cx + 1, cz))
        if r is not None:
            avg = (h[:, -1] + r[:, 0]) / 2
            h[:, -1] = avg
            r[:, 0] = avg
        u = heights.get((cx, cz + 1))
        if u is not None:
            avg = (h[-1, :] + u[0, :]) / 2
            h[-1, :] = avg
            u[0, :] = avg
    corners = {}
    for (cx, cz), h in heights.items():
        for (dx, dz), (ri, ci) in {(0, 0): (0, 0), (1, 0): (0, -1), (0, 1): (-1, 0), (1, 1): (-1, -1)}.items():
            corners.setdefault((cx + dx, cz + dz), []).append((h, ri, ci))
    for refs in corners.values():
        val = sum(h[ri, ci] for h, ri, ci in refs) / len(refs)
        for h, ri, ci in refs:
            h[ri, ci] = val

    # world origin in the middle of the map, kept on the tile grid
    ox = -((max(t[0] for t in tiles) + 1) // 2) * TILE
    oz = -((max(t[1] for t in tiles) + 1) // 2) * TILE

    # write tiles
    rows = []
    for i, (cx, cz) in enumerate(tiles):
        h = np.clip(heights[(cx, cz)], 0.0, TERRAIN_HEIGHT)
        q = np.round(h / TERRAIN_HEIGHT * 65535.0).astype("<u2")
        name = f"tile_x{cx:03d}_z{cz:03d}"
        (TILES_DIR / f"{name}.raw").write_bytes(q.tobytes())
        seg = int(min(tile_s[i] // SEGMENT_LENGTH, SEG_START[-1] - 1))
        sec = int(sector_of_s(tile_s[i]))
        rows.append([i, name, cx, cz, cx * TILE + ox, cz * TILE + oz, sec + 1, seg + 1, seg - SEG_START[sec] + 1])
    with open(OUT / "manifest.csv", "w", newline="", encoding="utf-8") as f:
        wr = csv.writer(f)
        wr.writerow(["order", "tile", "grid_x", "grid_z", "world_x", "world_z", "sector", "segment", "segment_in_sector"])
        wr.writerows(rows)
    (OUT / "settings.json").write_text(json.dumps({
        "tileSize": TILE, "resolution": RES, "terrainHeight": TERRAIN_HEIGHT,
        "segmentLength": SEGMENT_LENGTH, "segmentsPerSector": SEGMENTS, "biomes": BIOME_NAMES,
        "byteOrder": "little-endian", "firstRow": "south (lowest Z)", "tiles": len(tiles),
        "worldOffset": [ox, oz],
    }, indent=2), encoding="utf-8")

    # loops: world coordinates are Unity X/Z, yaw is degrees clockwise from +Z
    def road_y(sv):
        return float(np.interp(sv, s, world.rh))

    def yaw(d):
        return math.degrees(math.atan2(d[0], d[1]))

    def p3(p, sv):
        return [round(p[0] + ox, 2), round(road_y(sv), 2), round(p[1] + oz, 2)]

    loops_out = []
    loop_rows = []
    for lp in world.loops:
        L = SEGMENT_LENGTH
        first_c, last_c = p3(lp["c1"], lp["s_first"] + L / 2), p3(lp["c2"], lp["s_last"] + L / 2)
        loop_rows.append([lp["sector"], *first_c, round(yaw(lp["d1"]), 3), *last_c, round(yaw(lp["d2"]), 3),
                          round(yaw(lp["d1"]) - yaw(lp["d2"]), 3)])
        loops_out.append({
            "sector": lp["sector"],
            "first": {"start": p3(lp["a1"], lp["s_first"]), "end": p3(lp["b1"], lp["s_first"] + L),
                      "center": first_c, "yaw": round(yaw(lp["d1"]), 3)},
            "last": {"start": p3(lp["a2"], lp["s_last"]), "end": p3(lp["b2"], lp["s_last"] + L),
                     "center": last_c, "yaw": round(yaw(lp["d2"]), 3)},
            "teleportLastToFirst": {
                "yawDelta": round(yaw(lp["d1"]) - yaw(lp["d2"]), 3),
                "heightDelta": round(-lp["dy"], 3),
                "rule": "p_first = Quaternion.Euler(0, yawDelta, 0) * (p_last - last.center) + first.center; "
                        "centers hold the road height, so heightDelta is already included; rotation.y += yawDelta",
            },
            "identicalBand": {"halfWidth": LOOP_HALF_WIDTH, "fadeAtEnds": LOOP_END_FADE},
        })
    (OUT / "loops.json").write_text(json.dumps(loops_out, indent=2), encoding="utf-8")
    with open(OUT / "loops.csv", "w", newline="", encoding="utf-8") as f:
        wr = csv.writer(f)
        wr.writerow(["sector", "first_x", "first_y", "first_z", "first_yaw",
                     "last_x", "last_y", "last_z", "last_yaw", "yaw_delta"])
        wr.writerows(loop_rows)

    # road centre line for the spline: a point every ROAD_KNOT_STEP, on the road surface
    with open(OUT / "road.csv", "w", newline="", encoding="utf-8") as f:
        wr = csv.writer(f)
        wr.writerow(["s", "x", "y", "z", "sector"])
        for sv in np.arange(0.0, s[-1] + 1e-6, ROAD_KNOT_STEP):
            p = at(world.road, s, sv)
            wr.writerow([round(sv, 1), round(p[0] + ox, 3), round(road_y(sv), 3), round(p[1] + oz, 3),
                         int(sector_of_s(min(sv, TOTAL_LENGTH - 1e-6))) + 1])

    # verify loop identity on the written heights
    def sample(x, z):
        cx, cz = int(x // TILE), int(z // TILE)
        h = heights.get((cx, cz))
        if h is None:
            return None
        fx = (x - cx * TILE) / TILE * (RES - 1)
        fz = (z - cz * TILE) / TILE * (RES - 1)
        i, j = min(int(fz), RES - 2), min(int(fx), RES - 2)
        a, b = fz - i, fx - j
        return (h[i, j] * (1 - a) * (1 - b) + h[i, j + 1] * (1 - a) * b + h[i + 1, j] * a * (1 - b) + h[i + 1, j + 1] * a * b)

    worst = 0.0
    for lp in world.loops:
        for uu in np.arange(LOOP_END_FADE, SEGMENT_LENGTH - LOOP_END_FADE, 7.0):
            for vv in np.arange(-LOOP_HALF_WIDTH, LOOP_HALF_WIDTH + 0.1, 7.0):
                p2 = lp["a2"] + uu * lp["d2"] + vv * np.array([-lp["d2"][1], lp["d2"][0]])
                p1 = lp["a1"] + uu * lp["d1"] + vv * np.array([-lp["d1"][1], lp["d1"][0]])
                h2, h1 = sample(*p2), sample(*p1)
                if h1 is not None and h2 is not None:
                    worst = max(worst, abs((h2 - lp["dy"]) - h1))
    print(f"loop copies: max height difference {worst:.3f} m")
    if only:
        return

    # overview images
    PX = 32
    gw = max(t[0] for t in tiles) + 1
    gh = max(t[1] for t in tiles) + 1
    W, H = gw * PX, gh * PX
    mosaic = np.full((H, W), np.nan, dtype=np.float32)
    for (cx, cz), h in heights.items():
        r0 = (gh - 1 - cz) * PX
        step = (RES - 1) // PX
        mosaic[r0:r0 + PX, cx * PX:cx * PX + PX] = h[:-1:step, :-1:step][::-1]
    has = ~np.isnan(mosaic)
    q16 = np.zeros((H, W), dtype=np.uint16)
    q16[has] = np.round(np.clip(mosaic[has], 0, TERRAIN_HEIGHT) / TERRAIN_HEIGHT * 65535).astype(np.uint16)
    Image.fromarray(q16).save(OUT / "overview_heightmap_16bit.png")

    img = relief_image(mosaic, has, TILE / PX)
    dr = ImageDraw.Draw(img)

    def img_xy(p):
        return (p[0] / TILE * PX, H - p[1] / TILE * PX)

    line = [img_xy(p) for p in world.road[::8]]
    dr.line(line, fill=(40, 40, 40), width=5)
    dr.line(line, fill=(255, 214, 74), width=2)
    for lp in world.loops:
        dr.line([img_xy(lp["a1"]), img_xy(lp["b1"])], fill=(80, 220, 120), width=6)
        dr.line([img_xy(lp["a2"]), img_xy(lp["b2"])], fill=(255, 140, 60), width=6)
    try:
        font = ImageFont.truetype("arial.ttf", 48)
    except OSError:
        font = ImageFont.load_default()
    for i in range(len(SEGMENTS)):
        if i > 0:
            bx, by = img_xy(at(world.road, s, SEG_START[i] * SEGMENT_LENGTH))
            dr.ellipse([bx - 12, by - 12, bx + 12, by + 12], fill=(102, 49, 215), outline=(255, 255, 255), width=3)
        mx, my = img_xy(at(world.road, s, (SEG_START[i] + SEGMENTS[i] / 2) * SEGMENT_LENGTH))
        dr.text((mx + 30, my - 30), str(i + 1), fill=(255, 255, 255), font=font, stroke_width=4, stroke_fill=(20, 20, 20))
    img.save(OUT / "overview_relief.png")
    hw, hh = W // 2, H // 2
    for name, box in {"part_1_top_left": (0, 0, hw, hh), "part_2_top_right": (hw, 0, W, hh),
                      "part_3_bottom_left": (0, hh, hw, H), "part_4_bottom_right": (hw, hh, W, H)}.items():
        img.crop(box).save(OUT / f"overview_relief_{name}.png")

    allh = np.concatenate([h.ravel() for h in heights.values()])
    print(f"grid {gw} x {gh}, overview {W} x {H} px, heights {allh.min():.1f} .. {allh.max():.1f} m")


def relief_image(mosaic, has, m_per_px):
    filled = np.where(has, mosaic, np.nanmean(mosaic)).astype(np.float32)
    gzz, gxx = np.gradient(filled, m_per_px)
    nx, nz = -gxx, gzz
    nl = np.sqrt(nx * nx + 1.0 + nz * nz)
    lx, ly, lz = -0.5, 0.7, 0.5
    ll = math.sqrt(lx * lx + ly * ly + lz * lz)
    shade = np.clip((nx * lx + ly + nz * lz) / (nl * ll), 0.0, 1.0)
    stops_h = np.array([0, 30, 60, 100, 140, 200])
    stops_c = np.array([[86, 104, 72], [118, 128, 84], [150, 140, 98], [170, 150, 112], [176, 168, 150], [214, 212, 206]])
    hh = np.clip(filled, 0, 200)
    col = np.stack([np.interp(hh, stops_h, stops_c[:, i]) for i in range(3)], -1).astype(np.float32)
    rgb = col * (0.3 + 0.8 * shade[..., None])
    rgb = np.where(has[..., None], rgb, np.array([26, 26, 26], dtype=np.float32))
    return Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8), "RGB")


if __name__ == "__main__":
    main()
