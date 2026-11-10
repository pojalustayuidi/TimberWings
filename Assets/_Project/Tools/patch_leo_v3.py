#!/usr/bin/env python3
"""
Leo v3 geometry patch (deterministic).  Reads LeoModel.json (v2), writes it with softer body geometry.
Only parts/meshes change.  Nodes (joints, grips, carrier anchors), materials, palette, scale: untouched.
Usage (from the project root): python3 Tools/patch_leo_v3.py [SRC=v2 json] [DST]
Needs numpy. Refuses to run on an already patched file. Not needed inside Unity - reference only.
"""
import json, math, sys
import numpy as np

SRC = sys.argv[1] if len(sys.argv) > 1 else "Editor/TWLeo/LeoModel.json"
DST = sys.argv[2] if len(sys.argv) > 2 else SRC
d = json.load(open(SRC))
if any(m["name"].startswith("L3_") for m in d["meshes"]):
    sys.exit("already patched (L3_ meshes present) - run on the v2 JSON")

parts = {p["name"]: p for p in d["parts"]}
meshes = {m["name"]: m for m in d["meshes"]}
IDQ = [0.0, 0.0, 0.0, 1.0]
R4 = lambda v: [round(float(x), 4) for x in v]

# ------------------------------------------------------------------ mesh helpers
def _normals(V, T):
    V = np.asarray(V, float); N = np.zeros_like(V)
    for a, b, c in T:
        n = np.cross(V[b] - V[a], V[c] - V[a]); N[a] += n; N[b] += n; N[c] += n
    L = np.linalg.norm(N, axis=1, keepdims=True); L[L < 1e-12] = 1
    return N / L

def _merge(groups):
    V, N, T = [], [], []
    for gv, gt in groups:
        gn = _normals(gv, gt); o = len(V)
        V += [list(map(float, v)) for v in gv]; N += gn.tolist(); T += [(a + o, b + o, c + o) for a, b, c in gt]
    return V, N, T

def _volume(V, T):
    V = np.asarray(V, float)
    return sum(np.dot(V[a], np.cross(V[b], V[c])) for a, b, c in T) / 6.0

def add_mesh(name, V, N, T):
    meshes[name] = dict(name=name, vertices=[round(x, 4) for v in V for x in v],
                        normals=[round(float(x), 4) for n in N for x in n], triangles=[int(i) for t in T for i in t])
    d["meshes"].append(meshes[name])
    return name

def loft(name, rings, axis="y", seg=16, caps=(True, True)):
    """Stack of superellipse rings. ring: t (position on axis), a,b (half sizes), c1,c2 (centre offsets), n (squareness)."""
    V = []; ctr = []
    for r in rings:
        e = 2.0 / r.get("n", 2.0)
        for k in range(seg):
            ph = 2 * math.pi * k / seg; c, s = math.cos(ph), math.sin(ph)
            u = r["a"] * math.copysign(abs(c) ** e, c) + r.get("c1", 0)
            w = r["b"] * math.copysign(abs(s) ** e, s) + r.get("c2", 0)
            V.append((u, r["t"], w) if axis == "y" else (u, w, r["t"]))
        ctr.append((r.get("c1", 0), r["t"], r.get("c2", 0)) if axis == "y" else (r.get("c1", 0), r.get("c2", 0), r["t"]))
    T = []
    for i in range(len(rings) - 1):
        for k in range(seg):
            k2 = (k + 1) % seg
            a, b, c, dd = i * seg + k, i * seg + k2, (i + 1) * seg + k, (i + 1) * seg + k2
            T += [(a, c, b), (b, c, dd)]
    # orient sides outward (cross product must point away from the axis)
    Va = np.asarray(V); a, b, c = T[0]
    out = Va[a] - np.asarray(ctr[a // seg])
    if axis == "y": out[1] = 0
    else: out[2] = 0
    if np.dot(np.cross(Va[b] - Va[a], Va[c] - Va[a]), out) < 0:
        T = [(a, c, b) for a, b, c in T]
    groups = [(V, T)]
    ax = np.array([0, 1, 0.0]) if axis == "y" else np.array([0, 0, 1.0])
    for end, on in ((0, caps[0]), (1, caps[1])):
        if not on: continue
        i = 0 if end == 0 else len(rings) - 1
        cv = [V[i * seg + k] for k in range(seg)] + [ctr[i]]
        ct = []
        for k in range(seg):
            tri = (seg, k, (k + 1) % seg)
            n = np.cross(np.asarray(cv[tri[1]]) - np.asarray(cv[tri[0]]), np.asarray(cv[tri[2]]) - np.asarray(cv[tri[0]]))
            want = -ax if end == 0 else ax
            ct.append(tri if np.dot(n, want) >= 0 else (tri[0], tri[2], tri[1]))
        groups.append((cv, ct))
    # smooth normals on the sides, flat on caps
    V2, N2, T2 = [], [], []
    for gi, (gv, gt) in enumerate(groups):
        o = len(V2)
        if gi == 0:
            gn = _normals(gv, gt)
        else:
            gn = np.tile((-ax if (gi == 1 and caps[0]) else ax), (len(gv), 1))
            if gi == 1 and not caps[0]: gn = np.tile(ax, (len(gv), 1))
        V2 += [list(map(float, v)) for v in gv]; N2 += np.asarray(gn).tolist(); T2 += [(a + o, b + o, c + o) for a, b, c in gt]
    return add_mesh(name, V2, N2, T2)

def patch(name, fn, rows, base, top, orient=(0, 0, 1)):
    """Closed shell that follows a surface. fn(u,v)->point; rows: list of (v,[u...]); base/top: offsets along the surface normal."""
    P = np.array([[fn(u, v) for u in us] for v, us in rows]); nr, nc = P.shape[:2]
    Nn = np.zeros_like(P)
    for i in range(nr):
        for j in range(nc):
            du = P[i, min(j + 1, nc - 1)] - P[i, max(j - 1, 0)]; dv = P[min(i + 1, nr - 1), j] - P[max(i - 1, 0), j]
            n = np.cross(du, dv); n /= np.linalg.norm(n) + 1e-12
            Nn[i, j] = n
    # orient normals away from the body centre line (x,z of point) -> use sign of z of point
    for i in range(nr):
        for j in range(nc):
            if np.dot(Nn[i, j], orient) < 0: Nn[i, j] *= -1
    TP = P + Nn * top; BP = P + Nn * base
    idx = lambda i, j: i * nc + j
    quads = [(idx(i, j), idx(i, j + 1), idx(i + 1, j), idx(i + 1, j + 1)) for i in range(nr - 1) for j in range(nc - 1)]
    tT = [t for a, b, c, dd in quads for t in ((a, b, c), (b, dd, c))]
    bT = [(a, c_, b) for a, b, c_ in tT]
    groups = [(TP.reshape(-1, 3), tT), (BP.reshape(-1, 3), bT)]
    per = [(0, j) for j in range(nc - 1)] + [(i, nc - 1) for i in range(nr - 1)] + [(nr - 1, j) for j in range(nc - 1, 0, -1)] + [(i, 0) for i in range(nr - 1, 0, -1)]
    wv = []; wt = []
    for k, (i, j) in enumerate(per):
        i2, j2 = per[(k + 1) % len(per)]
        o = len(wv)
        wv += [TP[i, j], TP[i2, j2], BP[i, j], BP[i2, j2]]
        wt += [(o + 1, o, o + 2), (o + 1, o + 2, o + 3)]
    groups.append((wv, wt))
    # make orientation outward by signed volume
    allV = []; allT = []
    for gv, gt in groups:
        o = len(allV); allV += [list(v) for v in gv]; allT += [(a + o, b + o, c + o) for a, b, c in gt]
    if _volume(allV, allT) < 0:
        groups = [(gv, [(a, c, b) for a, b, c in gt]) for gv, gt in groups]
    V, N, T = _merge(groups)
    return add_mesh(name, V, N, T)

def setpart(name, **kw):
    parts[name].update({k: (R4(v) if isinstance(v, (list, tuple)) else v) for k, v in kw.items()})

def drop(*names):
    for n in names:
        p = parts.pop(n); d["parts"].remove(p)

def addp(name, node, mesh, mat, pos=(0, 0, 0), rot=IDQ, scale=(1, 1, 1)):
    p = dict(name=name, node=node, mesh=mesh, material=mat, position=R4(pos), rotation=R4(rot), scale=R4(scale))
    d["parts"].append(p); parts[name] = p

def lim(spec, length, dx, dz, axis_sign=-1):
    """rings along a bone that runs from 0 to (dx,-length,dz). spec: (y,a,b[,dz_extra[,n]])"""
    rr = []
    for s in spec:
        y, a, b = s[0], s[1], s[2]; ez = s[3] if len(s) > 3 else 0.0; n = s[4] if len(s) > 4 else 2.0
        k = -y / length
        rr.append(dict(t=y, a=a, b=b, c1=dx * k, c2=dz * k + ez, n=n))
    return rr

# ------------------------------------------------------------------ torso surface (Chest local)
TY = [-0.275, -0.24, -0.15, -0.05, 0.02, 0.075, 0.105, 0.128, 0.142, 0.150]
THW = [0.216, 0.218, 0.224, 0.236, 0.248, 0.253, 0.243, 0.205, 0.140, 0.085]
TF = [0.128, 0.131, 0.139, 0.150, 0.157, 0.152, 0.140, 0.126, 0.110, 0.095]
TB = [0.142, 0.1425, 0.1425, 0.1425, 0.1425, 0.1405, 0.136, 0.128, 0.115, 0.100]
TN = [3.3, 3.3, 3.1, 2.9, 2.7, 2.5, 2.4, 2.3, 2.2, 2.1]
OLD_Z = 0.1425
def _t(tab, y): return float(np.interp(y, TY, tab))
def hw(y): return _t(THW, y)
def surf(x, y, side):
    y = min(max(y, TY[0]), TY[-1] - 0.004); h = hw(y); n = _t(TN, y)
    q = min(abs(x) / h, 0.995)
    return (_t(TF, y) if side > 0 else _t(TB, y)) * (1 - q ** n) ** (1 / n)
def smooth(t): t = min(max(t, 0), 1); return t * t * (3 - 2 * t)

torso = [dict(t=TY[i], a=THW[i], b=(TF[i] + TB[i]) / 2, c2=(TF[i] - TB[i]) / 2, n=TN[i]) for i in range(len(TY))]
loft("L3_Torso", torso, seg=20)
drop("JacketBody"); addp("JacketBody", "Chest", "L3_Torso", "Jacket")

def front_fn(side):
    return lambda x, y: (x, y, side * surf(x, y, side))
def rows_front(xin, xout, y0, y1, ny=9, nx=6, sign=1):
    rr = []
    for i in range(ny):
        y = y0 + (y1 - y0) * i / (ny - 1); xo = min(xout, hw(y) - 0.012)
        rr.append((y, [sign * (xin + (xo - xin) * j / (nx - 1)) for j in range(nx)]))
    return rr

for sg, S in (("R", 1), ("L", -1)):
    drop("JacketFront" + sg, "ChestPocket" + sg, "PocketFlap" + sg, "JacketHem" + sg)
    patch("L3_Front" + sg, front_fn(1), rows_front(0.089, 0.239, -0.262, 0.095, 12, 6, S), -0.006, 0.034)
    addp("JacketFront" + sg, "Chest", "L3_Front" + sg, "JacketLight")
    patch("L3_Hem" + sg, front_fn(1), rows_front(0.089, 0.239, -0.266, -0.236, 3, 6, S), -0.006, 0.040)
    addp("JacketHem" + sg, "Chest", "L3_Hem" + sg, "JacketDark")
    patch("L3_Pocket" + sg, front_fn(1), rows_front(0.108, 0.213, -0.039, 0.049, 5, 5, S), 0.02, 0.050)
    addp("ChestPocket" + sg, "Chest", "L3_Pocket" + sg, "JacketDark")
    patch("L3_Flap" + sg, front_fn(1), rows_front(0.1025, 0.2195, 0.0305, 0.0655, 3, 5, S), 0.03, 0.060)
    addp("PocketFlap" + sg, "Chest", "L3_Flap" + sg, "Jacket")
    bx = 0.161 * S
    setpart("PocketButton" + sg, position=[bx, 0.042, surf(0.161, 0.042, 1) + 0.067])
drop("CreamShirt")
patch("L3_Shirt", front_fn(1), [(0.1075 - 0.345 * i / 8, [-0.095 + 0.19 * j / 6 for j in range(7)]) for i in range(9)], -0.006, 0.027)
addp("CreamShirt", "Chest", "L3_Shirt", "Shirt")

# baked detail meshes follow the new torso (vertex displacement, thickness preserved)
def refit(mname, front=True, back=False, lo=-0.06, minw=0.012):
    m = meshes[mname]; V = np.array(m["vertices"]).reshape(-1, 3)
    for i, (x, y, z) in enumerate(V):
        if front and z > 0.02:
            w = smooth((z - 0.02) / 0.1); h = hw(min(y, TY[-1] - 0.02)) - minw
            dz = surf(min(max(x, -h), h), y, 1) - OLD_Z
            V[i, 2] = z + w * max(dz, lo)
        elif back and z < -0.02:
            w = smooth((-z - 0.02) / 0.1); h = hw(min(y, TY[-1] - 0.02)) - minw
            dz = OLD_Z - surf(min(max(x, -h), h), y, -1)
            V[i, 2] = z + w * min(max(dz, 0), 0.03)
    m["vertices"] = [round(float(x), 4) for x in V.reshape(-1)]
for n in ("LapelR.001", "LapelL.001"): refit(n, lo=-0.04, minw=0.0)
refit("ChestStrap", front=True, back=False)
refit("BackStrap", front=False, back=True)
for n in ("StrapBuckle", "StrapBuckleInset"):
    p = parts[n]; x, y, z = p["position"]; p["position"] = R4([x, y, z + surf(x, y, 1) - OLD_Z])

# ------------------------------------------------------------------ neck + head
loft("L3_Neck", [dict(t=-0.09, a=0.098, b=0.085), dict(t=-0.04, a=0.077, b=0.07), dict(t=0.02, a=0.067, b=0.063), dict(t=0.075, a=0.067, b=0.063)], seg=16)
drop("NeckSkin"); addp("NeckSkin", "Neck", "L3_Neck", "Skin")

def ell(y, cy=0.195, ry=0.2525):
    s = math.sqrt(max(1 - ((y - cy) / ry) ** 2, 0)); return s
head = []
for y in (0.446, 0.428, 0.398, 0.358, 0.312, 0.262, 0.222, 0.182):
    s = ell(y); head.append(dict(t=y, a=0.2525 * s, b=0.2225 * s, c2=0.006, n=2.0))
for y, a, f, b in ((0.14, 0.240, 0.222, 0.205), (0.10, 0.228, 0.218, 0.190), (0.06, 0.214, 0.208, 0.172), (0.022, 0.190, 0.186, 0.150),
                   (-0.014, 0.150, 0.152, 0.118), (-0.042, 0.096, 0.105, 0.082), (-0.060, 0.04, 0.055, 0.045)):
    head.append(dict(t=y, a=a, b=(f + b) / 2, c2=(f - b) / 2, n=2.1))
head.sort(key=lambda r: r["t"])
lowr = [r for r in head if r["t"] < 0.15]
for r0, r1 in zip(lowr[:-1], lowr[1:]):          # bulge midpoints so the jaw reads as a curve, not a polyline
    head.append(dict(t=(r0["t"] + r1["t"]) / 2, a=(r0["a"] + r1["a"]) / 2 * 1.012, b=(r0["b"] + r1["b"]) / 2 * 1.012,
                     c2=(r0.get("c2", 0) + r1.get("c2", 0)) / 2, n=2.1))
head.sort(key=lambda r: r["t"])
loft("L3_Head", head, seg=24)
drop("Skull", "JawShape"); addp("Skull", "Head", "L3_Head", "Skin")

# ------------------------------------------------------------------ arms (sleeve, forearm, hand)
for sg, S in (("R", 1), ("L", -1)):
    drop("UpperSleeve" + sg, "ForearmR" if sg == "R" else "ForearmL")
    sl = lim([(0.014, .028, .027), (0.006, .062, .06), (-0.014, .086, .083), (-0.045, .099, .095), (-0.09, .096, .092), (-0.16, .083, .08), (-0.225, .072, .07), (-0.268, .068, .066)], 0.27, 0.015 * S, 0.005)
    loft("L3_Sleeve" + sg, sl, seg=16)
    addp("UpperSleeve" + sg, "UpperArm" + sg, "L3_Sleeve" + sg, "Jacket")
    setpart("RolledCuff" + sg, scale=[0.165, 0.33, 0.158])
    setpart("SleeveFold" + sg, scale=[0.152, 0.075, 0.148])
    fa = lim([(0.03, .058, .056), (0.0, .063, .061), (-0.04, .066, .062), (-0.09, .058, .055), (-0.15, .049, .048), (-0.2, .043, .043), (-0.235, .041, .041)], 0.23, 0.015 * S, 0.01)
    loft("L3_Forearm" + sg, fa, seg=14)
    addp("Forearm" + sg, "Elbow" + sg, "L3_Forearm" + sg, "Skin")
    drop("Palm" + sg)
    palm = [dict(t=0.02, a=.043, b=.034, c2=.006), dict(t=-0.01, a=.052, b=.037, c2=.012), dict(t=-0.04, a=.057, b=.036, c2=.015, n=2.3), dict(t=-0.064, a=.055, b=.031, c2=.017, n=2.3)]
    loft("L3_Palm" + sg, palm, seg=14)
    addp("Palm" + sg, "Hand" + sg, "L3_Palm" + sg, "Skin")
    for k in range(4):
        for suf in ("Base", "End"):
            pn = f"Finger{sg}{k}{suf}"
            if pn in parts: setpart(pn, scale=[parts[pn]["scale"][0] * 1.12, parts[pn]["scale"][1] * 1.04, parts[pn]["scale"][2] * 1.12])

# ------------------------------------------------------------------ pelvis + belt + legs
loft("L3_Pelvis", [dict(t=-0.075, a=.15, b=.105, n=2.4), dict(t=-0.035, a=.205, b=.123, c2=-0.004, n=2.6), dict(t=0.02, a=.207, b=.125, c2=-0.003, n=2.8),
                   dict(t=0.06, a=.192, b=.125, n=3.0), dict(t=0.101, a=.182, b=.123, n=3.0)], seg=22)
drop("Waist"); addp("Waist", "Hips", "L3_Pelvis", "Jeans")
loft("L3_Belt", [dict(t=0.0225, a=.186, b=.126, n=3.4), dict(t=0.0275, a=.193, b=.131, n=3.4), dict(t=0.069, a=.193, b=.131, n=3.4), dict(t=0.0735, a=.186, b=.126, n=3.4)], seg=22)
drop("Belt"); addp("Belt", "Hips", "L3_Belt", "LeatherDark")

for sg, S in (("R", 1), ("L", -1)):
    drop("Thigh" + sg, "TrouserShin" + sg, "TrouserRoll" + sg)
    th = lim([(0.045, .100, .104, -0.006), (0.0, .104, .110, -0.004), (-0.07, .104, .110), (-0.17, .096, .102, 0.002), (-0.27, .084, .090, 0.004), (-0.35, .076, .080), (-0.405, .072, .077)], 0.405, 0.015 * S, 0.015)
    loft("L3_Thigh" + sg, th, seg=18)
    addp("Thigh" + sg, "Hip" + sg, "L3_Thigh" + sg, "Jeans")
    sh = lim([(0.03, .072, .076), (-0.03, .076, .081), (-0.11, .080, .088, -0.010), (-0.19, .073, .079, -0.004), (-0.26, .068, .071), (-0.315, .066, .069)], 0.415, 0.0, 0.03)
    loft("L3_Shin" + sg, sh, seg=16)
    addp("TrouserShin" + sg, "Knee" + sg, "L3_Shin" + sg, "Jeans")
    k = 0.27 / 0.415
    roll = [dict(t=-0.232, a=.08, b=.08, c2=.03 * k + 0.004), dict(t=-0.240, a=.097, b=.096, c2=.03 * k + 0.004, n=2.4), dict(t=-0.302, a=.097, b=.096, c2=.03 * k + 0.004, n=2.4), dict(t=-0.312, a=.084, b=.083, c2=.03 * k + 0.004)]
    loft("L3_Roll" + sg, roll, seg=18)
    addp("TrouserRoll" + sg, "Knee" + sg, "L3_Roll" + sg, "JeansLight")
    setpart("KneeCloth" + sg, scale=[0.168, 0.15, 0.172], position=[0.0, 0.0, 0.004])
    # thigh pockets follow the thigh surface
    drop("SidePocket" + sg, "SidePocketFlap" + sg)
    def thigh_fn(z, y, S=S):
        kk = -y / 0.405; a = np.interp(-y, [-0.045, 0, .07, .17, .27, .35, .405], [.100, .104, .104, .096, .084, .076, .072])
        b = np.interp(-y, [-0.045, 0, .07, .17, .27, .35, .405], [.104, .110, .110, .102, .090, .080, .077])
        cz = .015 * kk + np.interp(-y, [-0.045, 0, .07, .17, .27, .35, .405], [-.006, -.004, 0, .002, .004, 0, 0])
        zz = min(max(z - cz, -b * .98), b * .98); x = a * math.sqrt(1 - (zz / b) ** 2)
        return (S * (.015 * kk + x), y, z)
    patch("L3_SidePocket" + sg, thigh_fn, [(y, [0.0 - 0.043 + 0.086 * j / 4 + 0.018 for j in range(5)]) for y in np.linspace(-0.225, -0.105, 5)], -0.006, 0.018, orient=(S, 0, 0))
    addp("SidePocket" + sg, "Hip" + sg, "L3_SidePocket" + sg, "JeansDark")
    patch("L3_SideFlap" + sg, thigh_fn, [(y, [0.0 - 0.045 + 0.090 * j / 4 + 0.018 for j in range(5)]) for y in np.linspace(-0.134, -0.100, 3)], 0.008, 0.024, orient=(S, 0, 0))
    addp("SidePocketFlap" + sg, "Hip" + sg, "L3_SideFlap" + sg, "JeansLight")
    # trouser seam: lay the baked line on the new thigh surface
    m = meshes["TrouserSeam" + sg]; V = np.array(m["vertices"]).reshape(-1, 3)
    for i, (x, y, z) in enumerate(V):
        p = thigh_fn(z, y); V[i, 0] = S * (abs(p[0]) + 0.0)
    m["vertices"] = [round(float(x), 4) for x in V.reshape(-1)]

    # boots
    drop("BootShaft" + sg, "BootCuff" + sg, "Heel" + sg, "ToeBox" + sg, "ToeSole" + sg)
    loft("L3_BootShaft" + sg, [dict(t=-0.085, a=.086, b=.09, c2=-.005, n=2.5), dict(t=-0.02, a=.082, b=.088, c2=-.007, n=2.4), dict(t=0.05, a=.088, b=.092, c2=-.007, n=2.4), dict(t=0.105, a=.094, b=.096, c2=-.007, n=2.4)], seg=18)
    addp("BootShaft" + sg, "Ankle" + sg, "L3_BootShaft" + sg, "Boot")
    loft("L3_BootCuff" + sg, [dict(t=0.064, a=.092, b=.095, c2=-.007, n=2.5), dict(t=0.072, a=.101, b=.102, c2=-.007, n=2.5), dict(t=0.108, a=.101, b=.102, c2=-.007, n=2.5), dict(t=0.116, a=.093, b=.096, c2=-.007, n=2.5)], seg=18)
    addp("BootCuff" + sg, "Ankle" + sg, "L3_BootCuff" + sg, "BootLight")
    loft("L3_Heel" + sg, [dict(t=-0.1565, a=.082, b=.060, c2=-.062, n=2.3), dict(t=-0.15, a=.092, b=.064, c2=-.06, n=2.3), dict(t=-0.085, a=.094, b=.066, c2=-.06, n=2.4), dict(t=-0.0765, a=.09, b=.063, c2=-.06, n=2.4)], seg=18)
    addp("Heel" + sg, "Ankle" + sg, "L3_Heel" + sg, "Sole")
    loft("L3_ToeBox" + sg, [dict(t=-0.115, a=.0975, b=.07, c2=.017, n=2.7), dict(t=-0.05, a=.0985, b=.071, c2=.017, n=2.5), dict(t=0.03, a=.097, b=.066, c2=.012, n=2.4),
                           dict(t=0.08, a=.088, b=.056, c2=.004, n=2.2), dict(t=0.113, a=.066, b=.042, c2=-.006, n=2.1), dict(t=0.128, a=.03, b=.02, c2=-.016, n=2.0)], axis="z", seg=18)
    addp("ToeBox" + sg, "Toe" + sg, "L3_ToeBox" + sg, "Boot")
    loft("L3_ToeSole" + sg, [dict(t=-0.125, a=.1, b=.0225, c2=-.047, n=3.2), dict(t=0.04, a=.102, b=.0225, c2=-.047, n=3.2), dict(t=0.095, a=.092, b=.0225, c2=-.047, n=3.0),
                            dict(t=0.118, a=.065, b=.0225, c2=-.047, n=2.6), dict(t=0.1285, a=.03, b=.0215, c2=-.047, n=2.2)], axis="z", seg=18)
    addp("ToeSole" + sg, "Toe" + sg, "L3_ToeSole" + sg, "Sole")


# ------------------------------------------------------------------ hair locks: light smoothing (curl untouched)
def smooth_mesh(name, lam=0.4, iters=2):
    m = meshes[name]; V = np.array(m["vertices"]).reshape(-1, 3); T = np.array(m["triangles"]).reshape(-1, 3)
    key = {}; gid = np.zeros(len(V), int)
    for i, v in enumerate(V): gid[i] = key.setdefault(tuple(np.round(v, 3)), len(key))
    G = len(key); nb = [set() for _ in range(G)]
    for a, b, c in T:
        for p, q in ((a, b), (b, c), (c, a)): nb[gid[p]].add(gid[q]); nb[gid[q]].add(gid[p])
    P = np.zeros((G, 3)); cnt = np.zeros(G)
    for i, v in enumerate(V): P[gid[i]] += v; cnt[gid[i]] += 1
    P /= cnt[:, None]
    for _ in range(iters):
        Q = P.copy()
        for g in range(G):
            if nb[g]: Q[g] = P[g] + lam * (np.mean([P[h] for h in nb[g]], axis=0) - P[g])
        P = Q
    V2 = P[gid]; N = np.zeros_like(V2)
    for a, b, c in T:
        n = np.cross(V2[b] - V2[a], V2[c] - V2[a])
        for p in (a, b, c): N[gid[p]] += n
    N = N[gid]; L = np.linalg.norm(N, axis=1, keepdims=True); L[L < 1e-12] = 1
    m["vertices"] = [round(float(x), 4) for x in V2.reshape(-1)]; m["normals"] = [round(float(x), 4) for x in (N / L).reshape(-1)]
for n in ("BangMiddle", "BangSideR", "BangSideL", "SideLockR", "SideLockL", "TopLock", "BackLock0", "BackLock1", "BackLock2", "BackLock3", "BackLock4", "BackLock5", "BackLock6"):
    smooth_mesh(n)

# ------------------------------------------------------------------ prune unused meshes, bookkeeping
used = {p["mesh"] for p in d["parts"]}
d["meshes"] = [m for m in d["meshes"] if m["name"] in used or m["name"] in ("sphere.001", "sphereLow.001", "box.001", "ring.001", "cylinder.001")]
d["authorNote"] += " | Leo v3: softer lofted body geometry (L3_* meshes); joints and nodes unchanged."
json.dump(d, open(DST, "w", encoding="utf-8"), ensure_ascii=False, separators=(",", ":"))
print("parts", len(d["parts"]), "meshes", len(d["meshes"]))
