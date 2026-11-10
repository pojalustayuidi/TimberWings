#!/usr/bin/env python3
"""
Leo carrier patch (детерминированный, можно запускать повторно).
Читает  Editor/TWLeo/LeoModel.json  (исходник), пишет его же с новой переноской:
  * лямка BackStrap прижата к спине (раньше висела в воздухе за спиной);
  * рюкзака нет: на поясе полка-«колыбель» с бортиком и латунными зацепками;
  * брёвна лежат стопкой 2 x 5 вплотную к спине (раньше висели в 0.15-0.3 м позади);
  * две вертикальные стяжки держат пачку и цепляются за зацепки.
Координаты: узел Backpack, мир (0, 1.205, -0.255) в позе покоя.
"""
import json, sys, copy, math, os

SRC = sys.argv[1] if len(sys.argv) > 1 else "Editor/TWLeo/LeoModel.json"
DST = sys.argv[2] if len(sys.argv) > 2 else SRC

BP_WORLD = (0.0, 1.205, -0.255)          # Backpack в мире (поза покоя)
def L(x, y, z):                          # мировые -> локальные Backpack
    return [round(x - BP_WORLD[0], 4), round(y - BP_WORLD[1], 4), round(z - BP_WORLD[2], 4)]

# ---- геометрия переноски (мировые метры модели, Y вверх, спина лицом к -Z) ----
BACK_Z      = -0.142          # плоскость спины куртки
STRAP_Z     = -0.150          # лямка и пояс-крепёж лежат на спине
SHELF_TOP   = 1.070           # верх полки (над ремнём 1.022..1.074)
D           = 0.081           # диаметр бревна в сборке (0.116 * 0.70)
COL0_Z      = -0.157 - D / 2  # ближний к спине столбик
ROW0_Y      = SHELF_TOP + D / 2
LOG_SCALE   = (0.46, 0.70, 0.70)   # вдоль оси, и два диаметра
REAR_Z      = COL0_Z - D - D / 2   # задняя грань пачки

def q_ident(): return [0.0, 0.0, 0.0, 1.0]
def box(name, node, mat, c, size, rot=None):
    return dict(name=name, node=node, mesh="box.001", material=mat,
                position=L(*c) if (node == "Backpack" or node.startswith("FrameT")) else list(c), rotation=rot or q_ident(), scale=[round(s, 4) for s in size])

d = json.load(open(SRC))
nodes = {n["name"]: n for n in d["nodes"]}
by_part = {p["name"]: p for p in d["parts"]}

# ---------- 1. Лямка BackStrap, прижатая к спине ----------
bs = by_part["BackStrap"]
mesh = next(m for m in d["meshes"] if m["name"] == bs["mesh"])
if not bs.get("_hugged"):
    V = [list(mesh["vertices"][i:i + 3]) for i in range(0, len(mesh["vertices"]), 3)]
    assert len(V) == 40, "неожиданный формат BackStrap"
    # Вершины: сечения 0 и 1 идут вперемешку (чётные/нечётные индексы первых 16), далее сечения 2, 3, 4 подряд по 8.
    ring_idx = [list(range(0, 16, 2)), list(range(1, 16, 2)), list(range(16, 24)), list(range(24, 32)), list(range(32, 40))]
    new_centers = [  # координаты узла Chest (мир Chest = (0, 1.3, 0))
        None,                              # 0: над плечом, оставляем
        (0.205, 0.100, STRAP_Z),           # 1: перегиб на спину
        (0.100, 0.052, STRAP_Z),           # 2..4: по спине по диагонали, как спереди
        (0.000, -0.050, STRAP_Z),
        (-0.180, -0.228, STRAP_Z),
    ]
    nv = [v[:] for v in V]; nn = [None] * 40
    for idx, nc in zip(ring_idx, new_centers):
        c = [sum(V[i][k] for i in idx) / 8 for k in range(3)]
        tgt = c if nc is None else list(nc)
        for i in idx:
            nv[i] = [tgt[k] + (V[i][k] - c[k]) for k in range(3)]
        for i in idx:                                   # нормали: от оси сечения наружу
            vv = [nv[i][k] - tgt[k] for k in range(3)]; n = math.sqrt(sum(a * a for a in vv)) or 1
            nn[i] = [a / n for a in vv]
    mesh["vertices"] = [round(x, 5) for v in nv for x in v]
    mesh["normals"] = [round(x, 5) for v in nn for x in v]
    bs["_hugged"] = True

# ---------- 2. Узлы: корень брёвен, стяжки, слоты ----------
croot = nodes["CargoRoot"]
croot["position"] = L(0.0, ROW0_Y, COL0_Z)
ties = nodes["CargoTies"]
ties["position"] = [0.0, 0.0, round(REAR_Z - 0.006 - COL0_Z, 4)]      # задняя грань пачки (в системе CargoRoot)
def slot_xy(i): return (0.0, D * (i // 2), -D * (i % 2))
for i in range(12):
    name = f"Cargo{i}"
    if name not in nodes:                           # слоты 6..9 раньше клонировал билдер: теперь они в данных
        src = nodes[f"Cargo{i - 6}"]
        n = copy.deepcopy(src); n["name"] = name
        d["nodes"].append(n); nodes[name] = n
        for p in [p for p in d["parts"] if p["node"] == f"Cargo{i - 6}"]:
            q = copy.deepcopy(p)
            q["name"] = p["name"] + f"_c{i}"
            q["node"] = name
            d["parts"].append(q)
    x, y, z = slot_xy(i)
    nodes[name]["position"] = [x, round(y, 4), round(z, 4)]
    nodes[name]["rotation"] = q_ident()
    nodes[name]["scale"] = list(LOG_SCALE)
    nodes[name]["active"] = False

# ---------- 3. Убираем плавающие детали старой переноски ----------
d["parts"] = [p for p in d["parts"] if not (p["node"] in ("CargoTies",) or p["name"] in
              ("CargoTieL", "CargoTieR", "LogBandL", "LogBandR"))]
d["parts"] = [p for p in d["parts"] if not p["name"].startswith(("F1_", "F2_", "F3_", "T2_", "Carrier", "Shelf", "BeltLoop", "HookLug", "HookEye", "TieStrap", "TieEdge", "TieClip", "Rivet"))]

# ---------- 4. Новая переноска ----------
P = []
shelf_cz = (-0.140 + (REAR_Z - 0.012)) / 2
shelf_len = abs(-0.140 - (REAR_Z - 0.012))
P.append(box("ShelfPlate", "Backpack", "Leather",      (0, SHELF_TOP - 0.008, shelf_cz), (0.330, 0.016, shelf_len)))
P.append(box("ShelfEdge",  "Backpack", "LeatherDark",  (0, SHELF_TOP - 0.020, shelf_cz), (0.344, 0.010, shelf_len + 0.012)))
P.append(box("ShelfLip",   "Backpack", "LeatherDark",  (0, SHELF_TOP + 0.010, REAR_Z - 0.006 - 0.004), (0.330, 0.028, 0.012)))
for s, sx in (("L", -1), ("R", 1)):
    x = sx * 0.095
    P.append(box(f"HookLug{s}",   "Backpack", "Brass",     (x, SHELF_TOP + 0.014, REAR_Z - 0.019), (0.034, 0.052, 0.014)))
    P.append(box(f"HookEye{s}",   "Backpack", "SteelDark", (x, SHELF_TOP + 0.020, REAR_Z - 0.028), (0.016, 0.022, 0.006)))
    P.append(box(f"BeltLoop{s}",  "Backpack", "Leather",   (x, SHELF_TOP - 0.020, STRAP_Z + 0.004), (0.046, 0.086, 0.012)))
    P.append(box(f"BeltLoopStitch{s}", "Backpack", "Stitch", (x, SHELF_TOP - 0.020, STRAP_Z - 0.003), (0.030, 0.060, 0.003)))
    P.append(box(f"Rivet{s}0",    "Backpack", "BrassLight",(x - 0.017, SHELF_TOP - 0.048, STRAP_Z - 0.003), (0.008, 0.008, 0.006)))
    P.append(box(f"Rivet{s}1",    "Backpack", "BrassLight",(x + 0.017, SHELF_TOP - 0.048, STRAP_Z - 0.003), (0.008, 0.008, 0.006)))
# стяжки (CargoTies в системе CargoRoot; высота .455 нормирована, ShowCargo её масштабирует)
for s, sx in (("L", -1), ("R", 1)):
    x = sx * 0.095
    P.append(box(f"TieStrap{s}", "CargoTies", "LeatherDark", [x, 0.0, 0.0],    (0.030, 0.455, 0.010)))
    P.append(box(f"TieEdge{s}",  "CargoTies", "Leather",     [x, 0.0, 0.004],  (0.012, 0.455, 0.004)))
    P.append(box(f"TieClip{s}",  "CargoTies", "Brass",       [x, 0.2275, -0.001], (0.040, 0.016, 0.016)))
d["parts"] += P

# ---------- 5. Рама по ступеням вместимости (остаётся навсегда) и вторая стяжка (32+ брёвен) ----------
for n in ("FrameT1", "FrameT2", "FrameT3"):
    if n not in nodes:
        nd = {"name": n, "parent": "Backpack", "active": False, "position": [0.0, 0.0, 0.0], "rotation": q_ident()}
        d["nodes"].append(nd); nodes[n] = nd
if "CargoTies2" not in nodes:
    nd = {"name": "CargoTies2", "parent": "CargoRoot", "active": False,
          "position": list(nodes["CargoTies"]["position"]), "rotation": q_ident()}
    d["nodes"].append(nd); nodes["CargoTies2"] = nd

def post(name, node, mat, x, y0, y1, z, w=0.034, t=0.030):
    return box(name, node, mat, (x, (y0 + y1) / 2, z), (w, y1 - y0, t))
FZ, RZ = -0.172, REAR_Z - 0.026          # передние стойки у спины, задние за пачкой
TOP = 1.448
F = []
# ступень 1 (вместимость 16+): две стойки и верхняя перекладина
for s_, sx in (("L", -1), ("R", 1)):
    x = sx * 0.172
    F.append(post(f"F1_Post{s_}", "FrameT1", "Wood", x, SHELF_TOP, TOP + 0.013, FZ))
    F.append(box(f"F1_PostEdge{s_}", "FrameT1", "WoodLight", (x - sx * 0.010, (SHELF_TOP + TOP) / 2, FZ - 0.016), (0.008, TOP - SHELF_TOP, 0.004)))
    F.append(box(f"F1_Cap{s_}", "FrameT1", "BrassLight", (x, TOP + 0.016, FZ), (0.020, 0.008, 0.020)))
F.append(box("F1_TopBar", "FrameT1", "WoodLight", (0, TOP, FZ), (0.380, 0.026, 0.030)))
# ступень 2 (32+): задние стойки, боковые поручни, стальные угольники, нижняя перекладина
for s_, sx in (("L", -1), ("R", 1)):
    x = sx * 0.172
    F.append(post(f"F2_RearPost{s_}", "FrameT2", "Wood", x, SHELF_TOP, TOP + 0.013, RZ))
    F.append(box(f"F2_SideTop{s_}", "FrameT2", "WoodLight", (x, TOP, (FZ + RZ) / 2), (0.028, 0.026, abs(FZ - RZ))))
    F.append(box(f"F2_SideMid{s_}", "FrameT2", "Wood", (x, 1.255, (FZ + RZ) / 2), (0.026, 0.022, abs(FZ - RZ))))
    for nm, zz in (("A", FZ), ("B", RZ)):
        F.append(box(f"F2_Bracket{s_}{nm}", "FrameT2", "SteelDark", (x + sx * 0.002, TOP, zz), (0.044, 0.044, 0.044)))
        F.append(box(f"F2_Rivet{s_}{nm}", "FrameT2", "BrassLight", (x + sx * 0.026, TOP, zz), (0.006, 0.014, 0.014)))
F.append(box("F2_RearBar", "FrameT2", "WoodLight", (0, TOP, RZ), (0.380, 0.026, 0.030)))
F.append(box("F2_LowBar", "FrameT2", "Wood", (0, SHELF_TOP + 0.030, FZ), (0.340, 0.024, 0.030)))
# ступень 3 (64+): стойки выше на ряд, верхняя перекладина, усиленная полка
HI = ROW0_Y + 5 * D + D / 2 + 0.020      # верх шестого ряда
for s_, sx in (("L", -1), ("R", 1)):
    x = sx * 0.172
    for nm, zz in (("F", FZ), ("R", RZ)):
        F.append(post(f"F3_Ext{nm}{s_}", "FrameT3", "Wood", x, TOP + 0.013, HI, zz))
        F.append(box(f"F3_Collar{nm}{s_}", "FrameT3", "SteelDark", (x, TOP + 0.020, zz), (0.044, 0.030, 0.044)))
    F.append(box(f"F3_SideTop{s_}", "FrameT3", "WoodLight", (x, HI, (FZ + RZ) / 2), (0.028, 0.026, abs(FZ - RZ))))
    F.append(box(f"F3_Cap{s_}", "FrameT3", "BrassLight", (x, HI + 0.016, FZ), (0.024, 0.010, 0.024)))
F.append(box("F3_TopBar", "FrameT3", "WoodLight", (0, HI, FZ), (0.380, 0.026, 0.030)))
F.append(box("F3_ShelfBrace", "FrameT3", "SteelDark", (0, SHELF_TOP - 0.032, shelf_cz), (0.300, 0.012, shelf_len - 0.020)))
# вторая стяжка (на 32+ брёвнах): горизонтальный ремень с пряжкой по задней грани пачки
F.append(box("T2_Band", "CargoTies2", "LeatherDark", [0.0, 0.0, -0.008], (0.300, 0.026, 0.010)))
F.append(box("T2_BandEdge", "CargoTies2", "Leather", [0.0, 0.0, -0.012], (0.300, 0.010, 0.004)))
F.append(box("T2_Buckle", "CargoTies2", "Brass", [0.0, 0.0, -0.014], (0.046, 0.040, 0.010)))
F.append(box("T2_BuckleInset", "CargoTies2", "LeatherDark", [0.0, 0.0, -0.021], (0.026, 0.020, 0.004)))
d["parts"] += F

d["authorNote"] = d.get("authorNote", "") + " | Carrier v2: belt shelf + hook lugs + hugging strap; no backpack."
json.dump(d, open(DST, "w"), separators=(",", ":"))
print("OK: parts", len(d["parts"]), "nodes", len(d["nodes"]), "->", DST)
