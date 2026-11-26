"""
Körperbaupläne: quad (Vierbeiner/Drache), biped (Zweibeiner), bird (Vogel), floater (Schwebewesen).
Alle teilen Kopf-Anbauteile (Augen, Ohren, Hörner, Nase/Schnabel, Büschel, Akzente).
"""
import math
from gen import *

sin = lambda t: math.sin(2 * math.pi * t)


class M:
    """Materialien einer Kreatur."""
    def __init__(self, s):
        self.body = mat("Fell", hexc(s["body"]), 0.75)
        self.belly = mat("Bauch", hexc(s["belly"]), 0.8)
        self.dark = mat("Dunkel", hexc(s.get("dark", "#2b2230")), 0.6)
        self.acc = mat("Akzent", hexc(s.get("accent", s["belly"])), 0.6)
        self.nose = mat("Nase", hexc(s.get("nose", "#2a1d24")), 0.3)
        self.glow = mat("Glow", hexc(s.get("glow", "#ffaa33")), 0.4, emit=4.0)
        self.horn = mat("Horn", hexc(s.get("horn", "#f1e6cf")), 0.35)
        self.beak = mat("Schnabel", hexc(s.get("beak", "#f2b134")), 0.4)


def basis(o, length_dir, normal_hint, loc):
    """Teil so drehen, dass +Z = length_dir und die Flächennormale (+Y) möglichst normal_hint ist."""
    z = V(length_dir).normalized(); n = V(normal_hint)
    y = (n - z * n.dot(z)).normalized(); x = y.cross(z)
    mtx = Matrix((x, y, z)).transposed()
    o.rotation_mode = 'QUATERNION'; o.rotation_quaternion = mtx.to_quaternion(); o.location = V(loc)
    return o


def orient(o, direction, loc, spin=0.0):
    """Teil (Spitze +Z) in Richtung direction drehen und an loc setzen."""
    q = V((0, 0, 1)).rotation_difference(V(direction).normalized())
    if spin: q = q @ Quaternion((0, 0, 1), spin)
    o.rotation_mode = 'QUATERNION'; o.rotation_quaternion = q; o.location = V(loc)
    return o


def head_parts(body, s, m, head, head_r, snout_tip=None, snout_r=0.08, chest=None, tr=0.2, beak=False):
    parts = []
    # Augen
    eye_s = head_r * s.get("eye", 0.36)
    if s.get("cyclops"):
        ec, n = surface(body, head, V((0, -1, s.get("eye_up", 0.15))), embed=eye_s * 0.35)
        parts += anime_eye("Auge0", ec, n, eye_s * 1.6, hexc(s.get("iris", "#3a7bd5")))
    else:
        for sx in (-1, 1):
            ec, n = surface(body, head, V((sx * s.get("eye_spread", 0.55), -1, s.get("eye_up", 0.12))), embed=eye_s * 0.22)
            parts += anime_eye(f"Auge{sx}", ec, n, eye_s, hexc(s.get("iris", "#3a7bd5")))
    # Nase / Schnabel / Mund
    if beak:
        bl = s.get("beak_len", head_r * 0.7)
        bp, bn = surface(body, head, V((0, -1, -0.15)), embed=0.03)
        bk = horn("Schnabel", bl, head_r * 0.3, s.get("beak_curve", 0.35), segs=12, material=m.beak)
        orient(bk, V((0, -1, -0.35)), bp); parts.append(bk)
    elif snout_tip is not None:
        np_, _ = surface(body, snout_tip + V((0, snout_r, 0)), V((0, -1, 0.35)))
        nose = sphere("Nase", 1, m.nose); place(nose, np_, scale=(snout_r * 0.5, snout_r * 0.38, snout_r * 0.36)); parts.append(nose)
        mp, _ = surface(body, snout_tip + V((0, snout_r * 0.8, -snout_r * 0.1)), V((0, -0.7, -1)), embed=0.004)
        mo = sphere("Mund", 1, m.nose, 12, 8); place(mo, mp, scale=(snout_r * 0.5, snout_r * 0.18, snout_r * 0.08)); parts.append(mo)
    else:
        mp, _ = surface(body, head, V((0, -1, -0.45)), embed=0.004)
        mo = sphere("Mund", 1, m.nose, 12, 8); place(mo, mp, scale=(head_r * 0.16, head_r * 0.05, head_r * 0.035)); parts.append(mo)
    # Ohren
    et = s.get("ears", "pointy")
    if et != "none":
        el, ew = s.get("ear_len", head_r * 1.1), s.get("ear_w", head_r * 0.7)
        for sx in (-1, 1):
            if et == "round":
                e = sphere(f"Ohr{sx}", 1, m.body); ep, en = surface(body, head, V((sx * 0.8, 0.15, 0.85)), embed=0.02)
                place(e, ep, scale=(ew * 0.5, ew * 0.25, ew * 0.5)); parts.append(e)
                ei = sphere(f"OhrI{sx}", 1, m.dark if s.get("ear_inner_dark") else m.belly); place(ei, ep + V((0, -ew * 0.12, 0)), scale=(ew * 0.32, ew * 0.18, ew * 0.32)); parts.append(ei)
                continue
            long_ = et == "long"
            e = leaf(f"Ohr{sx}", el * (1.5 if long_ else 1), ew * (0.75 if long_ else 1), 0.035, 0.25, m.body,
                     inner=m.dark if s.get("ear_inner_dark") else m.belly, tip_bend=s.get("ear_bend", 0.05))
            ep, _ = surface(body, head, V((sx * 0.75, 0.1, 1)), embed=0.03)
            tilt = s.get("ear_tilt", 22) + (25 if et == "droopy" else 0)
            place(e, ep, rot=(math.radians(-12 + (40 if et == "droopy" else 0)), math.radians(sx * tilt), math.radians(sx * -15)))
            parts.append(e)
    # Hörner
    if s.get("horns"):
        hl, hr_ = s["horns"]
        for sx in (-1, 1):
            hn = horn(f"Horn{sx}", hl, hr_, s.get("horn_curve", -0.35), material=m.horn)
            hp_, _ = surface(body, head, V((sx * 0.45, 0.2, 1)), embed=0.02)
            place(hn, hp_, rot=(math.radians(-25), math.radians(sx * s.get("horn_spread", 25)), 0)); parts.append(hn)
    if s.get("unihorn"):
        hn = horn("Einhorn", s["unihorn"], head_r * 0.12, -0.1, material=m.glow if s.get("unihorn_glow") else m.horn)
        hp_, _ = surface(body, head, V((0, -0.35, 1)), embed=0.02)
        place(hn, hp_, rot=(math.radians(-30), 0, 0)); parts.append(hn)
    # Wangenbüschel
    if s.get("tufts", True):
        for sx in (-1, 1):
            for k in range(3):
                tp, _ = surface(body, head + V((0, 0, -head_r * 0.1)), V((sx, 0.05 + k * 0.25, -0.25 - k * 0.12)), embed=0.035)
                t = leaf(f"Wange{sx}{k}", head_r * (0.5 - k * 0.08), head_r * 0.5, 0.05, 0.02, m.body, tip_bend=-0.05)
                orient(t, V((sx * 1.0, 0.7 + k * 0.25, -0.55)), tp); parts.append(t)
    if chest is not None and s.get("chest_tufts", True):
        for k in range(4):
            tp, _ = surface(body, chest + V((0, 0, -tr * 0.1)), V(((k - 1.5) * 0.45, -1, -0.35)), embed=0.04)
            t = leaf(f"Brust{k}", tr * 0.6, tr * 0.7, 0.05, 0.02, m.belly, tip_bend=-0.1)
            orient(t, V(((k - 1.5) * 0.3, -0.12, -1)), tp); parts.append(t)
    # Kopf-Akzente
    acc = s.get("head_accent")
    if acc == "flames":
        for k, (x, sc) in enumerate([(0, 1.0), (0.07, 0.7), (-0.07, 0.7)]):
            f = leaf(f"Flamme{k}", head_r * 0.9 * sc, head_r * 0.55 * sc, 0.03, 0.15, m.glow, tip_bend=0.25)
            fp, _ = surface(body, head, V((x * 4, 0.15, 1)), embed=0.03)
            place(f, fp, rot=(math.radians(-35), 0, 0)); parts.append(f)
    elif acc == "leaves":
        for k in range(3):
            lf = leaf(f"Blatt{k}", head_r * 0.9, head_r * 0.5, 0.02, 0.2, m.acc)
            lp, _ = surface(body, head, V(((k - 1) * 0.35, 0.1, 1)), embed=0.02)
            place(lf, lp, rot=(math.radians(-20), math.radians((k - 1) * 35), 0)); parts.append(lf)
    elif acc == "crest":
        for k in range(4):
            f = leaf(f"Kamm{k}", head_r * (0.9 - k * 0.12), head_r * 0.4, 0.03, 0.05, m.acc, tip_bend=0.2)
            fp, _ = surface(body, head, V((0, 0.2 + k * 0.45, 1)), embed=0.03)
            place(f, fp, rot=(math.radians(-30 - k * 18), 0, 0)); parts.append(f)
    elif acc == "cap":
        cp, _ = surface(body, head, V((0, 0.05, 1)), embed=head_r * 0.25)
        cap = sphere("Hut", 1, m.acc, 32, 16); place(cap, cp + V((0, 0.03, head_r * 0.02)), scale=(head_r * 1.5, head_r * 1.4, head_r * 0.55)); parts.append(cap)
        for k in range(6):
            a = k / 6 * math.tau
            dot = sphere(f"Tupfen{k}", 1, m.belly, 12, 8)
            place(dot, cp + V((math.cos(a) * head_r * 0.9, math.sin(a) * head_r * 0.85, head_r * 0.24)), scale=(head_r * 0.2, head_r * 0.2, head_r * 0.1)); parts.append(dot)
    elif acc == "antenna":
        for sx in (-1, 1):
            an = horn(f"Fuehler{sx}", head_r * 0.9, head_r * 0.06, -0.3, segs=6, material=m.dark)
            ap, _ = surface(body, head, V((sx * 0.4, 0.1, 1)), embed=0.01)
            place(an, ap, rot=(math.radians(-15), math.radians(sx * 25), 0)); parts.append(an)
            tip = sphere(f"FuehlerKopf{sx}", 1, m.glow, 12, 8)
            place(tip, ap + V((sx * head_r * 0.38, head_r * 0.1, head_r * 0.82)), scale=(head_r * 0.13,) * 3); parts.append(tip)
    elif acc == "crystal":
        for k in range(3):
            cr = horn(f"KopfKristall{k}", head_r * (0.8 if k == 1 else 0.5), head_r * 0.13, 0.0, segs=6, material=m.glow, rings=2)
            cp, _ = surface(body, head, V(((k - 1) * 0.5, 0.1, 1)), embed=0.02)
            place(cr, cp, rot=(math.radians(-10), math.radians((k - 1) * 30), 0)); parts.append(cr)
    return parts


def body_accents(body, s, m, center, back_dir=V((0, 0, 1)), span=0.3, count=4):
    """Rückenstacheln, Kristalle, Flammen entlang des Rückens."""
    parts = []
    kind = s.get("back")
    if not kind: return parts
    for k in range(count):
        t = (k / max(1, count - 1)) - 0.5
        org = center + V((0, t * span, 0))
        p, n = surface(body, org, back_dir + V((0, t * 0.4, 0)), embed=0.02)
        if kind == "spikes":
            h = horn(f"Stachel{k}", 0.09 + 0.05 * (1 - abs(t) * 2), 0.04, 0.2, segs=8, material=m.horn)
        elif kind == "crystals":
            h = horn(f"Kristall{k}", 0.12 + 0.07 * (k % 2), 0.045, 0.0, segs=6, material=m.glow, rings=2)
        elif kind == "flames":
            h = leaf(f"RFlamme{k}", 0.2 + 0.08 * (k % 2), 0.12, 0.03, 0.12, m.glow, tip_bend=0.3)
        elif kind == "leaves":
            h = leaf(f"RBlatt{k}", 0.2, 0.12, 0.02, 0.2, m.acc)
        elif kind == "shell":
            h = sphere(f"Panzer{k}", 1, m.acc); h.scale = (0.13, 0.12, 0.06)
        else:
            continue
        orient(h, n.lerp(V((0, 0.6, 1)), 0.4), p)
        parts.append(h)
    return parts


def wings(body, s, m, root, span, chord, kind="feather"):
    parts = []
    for sx in (-1, 1):
        if kind == "bat":
            w = leaf(f"Fluegel{sx}", span, chord, 0.02, 0.1, m.acc if s.get("wing_acc", True) else m.body, tip_bend=0.05)
        else:
            w = leaf(f"Fluegel{sx}", span, chord, 0.05, 0.15, m.body, inner=m.acc, tip_bend=0.1)
        p, _ = surface(body, root, V((sx, 0.3, 0.5)), embed=0.03)
        basis(w, V((sx * 1.0, 0.55, 0.45)), V((sx * 0.25, 0.55, -0.8)), p)
        w["side"] = sx
        parts.append(w)
    return parts


# ===========================================================================
# Vierbeiner / Drache
# ===========================================================================

def build_quad(s):
    L = s.get("body_len", 0.5); H = s.get("body_h", 0.4)
    tr = s.get("chest_r", 0.2); hr = s.get("hip_r", 0.17)
    head_r = s.get("head_r", 0.24); snout = s.get("snout", 0.13); snout_r = s.get("snout_r", 0.085)
    leg_r = s.get("leg_r", 0.075); legw = s.get("leg_w", 0.11); neck = s.get("neck", 0.18)
    tail_len = s.get("tail_len", 0.5); tail_r = s.get("tail_r", 0.11); tail_up = s.get("tail_up", 0.6)
    m = M(s)
    chest = V((0, -L / 2, H + 0.02)); hips = V((0, L / 2, H))
    neck_top = chest + V((0, -neck * 0.45, neck))
    head = neck_top + V((0, -head_r * 0.35, head_r * 0.55))
    snout_tip = head + V((0, -head_r - snout, -head_r * 0.25))

    b = Blob("Koerper")
    b.ball(hips, hr, (1.0, 1.15, 0.95))
    b.ball((hips + chest) / 2 + V((0, 0, 0.01)), (hr + tr) / 2 * 0.92, (0.95, 1.2, 0.9))
    b.ball(chest, tr, (1.0, 1.0, 1.05))
    b.ball(chest + V((0, -tr * 0.35, -tr * 0.2)), tr * 0.75, (0.9, 0.8, 1.0))
    b.capsule(chest + V((0, -0.02, 0.05)), neck_top, tr * 0.55)
    b.ball(head, head_r, (1.08, 0.98, 0.95))
    for sx in (-1, 1):
        b.ball(head + V((sx * head_r * 0.62, -head_r * 0.1, -head_r * 0.38)), head_r * 0.45, (1.1, 0.8, 0.8))
    if snout > 0.01:
        b.capsule(head + V((0, -head_r * 0.6, -head_r * 0.2)), snout_tip, snout_r)
    legs = {}
    for side, sx in (("L", 1), ("R", -1)):
        for end, y0, r in (("V", chest.y + 0.05, leg_r), ("H", hips.y - 0.03, leg_r * 1.1)):
            top = V((sx * legw, y0, H - 0.02))
            knee = V((sx * legw * 1.05, y0 + (0.02 if end == "V" else -0.05), H * 0.45))
            ankle = V((sx * legw * 1.05, y0 + (0.0 if end == "V" else 0.03), 0.07))
            toe = ankle + V((0, -0.09, -0.04))
            b.ball(top + V((0, 0, -0.04)), r * (2.0 if end == "H" else 1.6), (0.9, 1.1, 1.2))
            b.chain([top, knee, ankle], [r * 1.3, r * 1.0, r * 0.9])
            b.ball(ankle + V((0, -0.035, -0.025)), r * 1.25, (1.0, 1.35, 0.7))
            legs[side + end] = (top, knee, ankle, toe)
    tb = hips + V((0, hr * 0.9, hr * 0.2))
    tail_pts = [tb] + [tb + V((0, tail_len * (k / 4), tail_len * tail_up * math.sin((k / 4) * 1.3))) for k in range(1, 5)]
    if s.get("tail_fluffy", True):
        radii = [tail_r * 0.45, tail_r * 0.9, tail_r * 1.2, tail_r * 1.05, tail_r * 0.55]
    else:
        radii = [tail_r * 0.7, tail_r * 0.6, tail_r * 0.45, tail_r * 0.33, tail_r * 0.22]
    b.chain(tail_pts, radii)
    body = b.to_mesh(voxel=s.get("voxel", 0.016))

    zones = [
        (m.belly, (0, chest.y - tr * 0.3, H - tr * 0.4), (tr * 0.75, tr * 0.8, tr * 0.9)),
        (m.belly, (0, (chest.y + hips.y) / 2, H - tr * 0.75), (tr * 0.6, L * 0.6, tr * 0.35)),
        (m.belly, tuple(head + V((0, -head_r * 0.45, -head_r * 0.55))), (head_r * 0.7, head_r * 0.5, head_r * 0.35)),
    ]
    if snout > 0.01:
        zones.append((m.belly, tuple(snout_tip + V((0, head_r * 0.25, -snout_r * 0.4))), (snout_r * 1.4, snout * 1.2, snout_r * 1.1)))
    if s.get("socks", True):
        for (top, knee, ankle, toe) in legs.values():
            zones.append((m.dark, tuple(ankle + V((0, -0.02, 0.0))), (leg_r * 2.2, leg_r * 3, 0.11)))
    if s.get("tail_tip", True):
        zones.append((m.glow if s.get("tail_tip_glow") else m.acc, tuple(tail_pts[-1] + V((0, 0.02, 0.02))), (tail_r * 1.5, tail_len * 0.28, tail_r * 1.6)))
    if s.get("stripes"):
        for k in range(3):
            zones.append((m.dark, (0, hips.y - k * L * 0.28, H + tr * 0.85), (tr * 1.3, 0.035, tr * 0.4)))
    if s.get("mask"):
        zones.append((m.dark, tuple(head + V((0, -head_r * 0.6, head_r * 0.15))), (head_r * 0.95, head_r * 0.4, head_r * 0.28)))
    vcol_zones(body, m.body, zones, soft=s.get("soft", 0.3))

    parts = head_parts(body, s, m, head, head_r, snout_tip if snout > 0.01 else None, snout_r, chest, tr, beak=s.get("beak_quad", False))
    parts += body_accents(body, s, m, (hips + chest) / 2 + V((0, 0, 0)), span=L * 1.1, count=s.get("back_n", 4))
    wing_parts = []
    if s.get("wings"):
        wing_parts = wings(body, s, m, chest + V((0, 0.05, tr * 0.5)), s.get("wing_span", 0.55), s.get("wing_chord", 0.35), s.get("wing_kind", "bat"))
    apply_tf(parts + wing_parts)

    rig = Rig()
    rig.bone("Root", (0, 0, 0), (0, 0, 0.25), deform=False)
    rig.bone("Hips", hips, (hips + chest) / 2, "Root")
    rig.bone("Chest", (hips + chest) / 2, chest, "Hips")
    rig.bone("Neck", chest, neck_top, "Chest")
    rig.bone("Head", neck_top, head + V((0, -head_r * 0.45, head_r * 0.1)), "Neck")
    if snout > 0.01:
        rig.bone("Snout", head + V((0, -head_r * 0.6, -head_r * 0.2)), snout_tip, "Head")
    for k, (top, knee, ankle, toe) in legs.items():
        par = "Chest" if k.endswith("V") else "Hips"
        rig.bone(f"Leg{k}1", top, knee, par); rig.bone(f"Leg{k}2", knee, ankle, f"Leg{k}1"); rig.bone(f"Leg{k}3", ankle, toe, f"Leg{k}2")
    for i in range(4):
        rig.bone(f"Tail{i}", tail_pts[i], tail_pts[i + 1], "Hips" if i == 0 else f"Tail{i - 1}")
    for w in wing_parts:
        sx = w["side"]; nm = "WingL" if sx > 0 else "WingR"
        base = chest + V((sx * tr * 0.6, 0.05, tr * 0.6))
        rig.bone(nm, base, base + V((0, -0.15, 0)), "Chest")
    rig.build()
    skin(body, rig, bones=[b_[0] for b_ in rig.bones if b_[4] and not b_[0].startswith("Wing")])
    for p in parts:
        bone = "Head"
        if p.name.startswith(("Stachel", "Kristall", "RFlamme", "RBlatt", "Panzer")): bone = "Chest"
        if p.name.startswith("Brust"): bone = "Chest"
        skin(p, rig, rigid=bone)
    for w in wing_parts:
        skin(w, rig, rigid="WingL" if w["side"] > 0 else "WingR")
    quad_anims(rig.obj, s)
    return rig, [finalize([body] + parts + wing_parts, rig)]


def wing_flap(a, n, amp=35, cycles=2):
    a.curve("WingL", lambda t: ((0, amp * sin(t * cycles), 0), None, None))
    a.curve("WingR", lambda t: ((0, -amp * sin(t * cycles), 0), None, None))


def quad_anims(r, s):
    legs = ["LV", "RV", "LH", "RH"]
    ph = {"LV": 0.0, "RH": 0.0, "RV": 0.5, "LH": 0.5}
    has_w = "WingL" in r.pose.bones
    a = Anim(r, "Idle", 60)
    a.curve("Hips", lambda t: (None, (0, 0, 0.012 * sin(t)), None), rot=False, loc=True)
    a.curve("Chest", lambda t: ((2 * sin(t), 0, 0), None, (1 + 0.03 * sin(t),) * 3), scl=True)
    a.curve("Head", lambda t: ((4 * sin(t + 0.25), 0, 3 * sin(t + 0.4)), None, None))
    for i, amp in enumerate((18, 14, 12)):
        a.curve(f"Tail{i}", lambda t, i=i, amp=amp: ((0, 0, amp * sin(t - 0.1 * i)), None, None))
    if has_w: wing_flap(a, 60, 12, 1)
    a.done()
    a = Anim(r, "Walk", 24)
    for lg in legs:
        p = ph[lg]
        a.curve(f"Leg{lg}1", lambda t, p=p: ((28 * sin(t + p), 0, 0), None, None))
        a.curve(f"Leg{lg}2", lambda t, p=p: ((-22 * max(0, sin(t + p + 0.25)), 0, 0), None, None))
    a.curve("Hips", lambda t: ((0, 0, 0), (0, 0, 0.025 * abs(sin(t))), None), loc=True)
    a.curve("Head", lambda t: ((5 * sin(2 * t), 0, 0), None, None))
    a.curve("Tail0", lambda t: ((0, 0, 15 * sin(t)), None, None))
    if has_w: wing_flap(a, 24, 20, 1)
    a.done()
    a = Anim(r, "Attack", 22)
    a.key("Root", 0, loc=(0, 0, 0)); a.key("Root", 6, loc=(0, 0.03, -0.12)); a.key("Root", 11, loc=(0, 0.06, 0.35)); a.key("Root", 22, loc=(0, 0, 0))
    a.key("Hips", 0, rot=(0, 0, 0)); a.key("Hips", 6, rot=(-12, 0, 0)); a.key("Hips", 11, rot=(10, 0, 0)); a.key("Hips", 22, rot=(0, 0, 0))
    a.key("Head", 0, rot=(0, 0, 0)); a.key("Head", 8, rot=(-25, 0, 0)); a.key("Head", 12, rot=(25, 0, 0)); a.key("Head", 22, rot=(0, 0, 0))
    for lg in legs:
        f = 1 if lg.endswith("V") else -1
        a.key(f"Leg{lg}1", 0, rot=(0, 0, 0)); a.key(f"Leg{lg}1", 10, rot=(f * 35, 0, 0)); a.key(f"Leg{lg}1", 22, rot=(0, 0, 0))
    if has_w: wing_flap(a, 22, 30, 1)
    a.done()
    a = Anim(r, "Punch", 34)
    a.key("Hips", 0, rot=(0, 0, 0)); a.key("Hips", 12, rot=(-38, 0, 0)); a.key("Hips", 18, rot=(-38, 0, 0)); a.key("Hips", 23, rot=(8, 0, 0)); a.key("Hips", 34, rot=(0, 0, 0))
    a.key("Head", 0, rot=(0, 0, 0)); a.key("Head", 12, rot=(-20, 0, 0)); a.key("Head", 23, rot=(20, 0, 0)); a.key("Head", 34, rot=(0, 0, 0))
    for lg in ("LV", "RV"):
        a.key(f"Leg{lg}1", 0, rot=(0, 0, 0)); a.key(f"Leg{lg}1", 12, rot=(55, 0, 0)); a.key(f"Leg{lg}1", 23, rot=(-10, 0, 0)); a.key(f"Leg{lg}1", 34, rot=(0, 0, 0))
        a.key(f"Leg{lg}2", 0, rot=(0, 0, 0)); a.key(f"Leg{lg}2", 12, rot=(-50, 0, 0)); a.key(f"Leg{lg}2", 23, rot=(0, 0, 0))
    a.key("Root", 0, loc=(0, 0, 0)); a.key("Root", 23, loc=(0, -0.03, 0.05)); a.key("Root", 34, loc=(0, 0, 0))
    if has_w: wing_flap(a, 34, 45, 2)
    a.done()
    hit_die_dance(r, has_w, legs=legs, tail=True)


def hit_die_dance(r, has_w, legs=(), tail=False, fall_axis=2):
    a = Anim(r, "HitRecieve", 16)
    a.key("Root", 0, loc=(0, 0, 0)); a.key("Root", 4, loc=(0, 0.02, -0.12)); a.key("Root", 16, loc=(0, 0, 0))
    hb = "Hips" if "Hips" in r.pose.bones else "Body"
    a.key(hb, 0, rot=(0, 0, 0)); a.key(hb, 4, rot=(8, 0, 6)); a.key(hb, 16, rot=(0, 0, 0))
    a.key("Head", 0, rot=(0, 0, 0)); a.key("Head", 4, rot=(20, 0, -10)); a.key("Head", 16, rot=(0, 0, 0))
    a.done()
    a = Anim(r, "Death", 36)
    rot_end = [0, 0, 0]; rot_end[fall_axis] = 90
    a.key("Root", 0, rot=(0, 0, 0), loc=(0, 0, 0)); a.key("Root", 10, rot=(0, 0, 0), loc=(0, 0.08, 0))
    a.key("Root", 24, rot=tuple(v * 0.97 for v in rot_end), loc=(0, 0, 0)); a.key("Root", 36, rot=tuple(rot_end), loc=(0, 0, 0))
    a.key("Head", 0, rot=(0, 0, 0)); a.key("Head", 24, rot=(25, 0, 0)); a.key("Head", 36, rot=(30, 0, 0))
    for lg in legs:
        a.key(f"Leg{lg}1", 0, rot=(0, 0, 0)); a.key(f"Leg{lg}1", 30, rot=(15, 0, 0))
    if has_w:
        a.key("WingL", 0, rot=(0, 0, 0)); a.key("WingL", 30, rot=(0, -30, 0)); a.key("WingR", 0, rot=(0, 0, 0)); a.key("WingR", 30, rot=(0, 30, 0))
    a.done()
    a = Anim(r, "Dance", 30)
    a.curve("Root", lambda t: ((0, 0, 0), (0, 0.22 * abs(math.sin(math.pi * 2 * t)), 0), None), loc=True)
    a.curve("Head", lambda t: ((-10 * abs(sin(t)), 0, 15 * sin(t)), None, None))
    if tail and "Tail0" in r.pose.bones: a.curve("Tail0", lambda t: ((0, 0, 30 * sin(2 * t)), None, None))
    if has_w: wing_flap(r and a, 30, 40, 2)
    a.done()
    r.animation_data.action = bpy.data.actions["Idle"]


# ===========================================================================
# Zweibeiner (Bär, Kobold, Golem, Hasenkrieger …)
# ===========================================================================

def build_biped(s):
    m = M(s)
    leg_len = s.get("leg_len", 0.22); hipz = leg_len + 0.06
    br = s.get("belly_r", 0.22); cr = s.get("chest_r", 0.2)
    head_r = s.get("head_r", 0.27); arm_r = s.get("arm_r", 0.06); arm_len = s.get("arm_len", 0.32)
    sw = s.get("shoulder_w", cr * 1.05); snout = s.get("snout", 0.0); snout_r = s.get("snout_r", 0.08)
    hips = V((0, 0, hipz)); belly = V((0, -0.02, hipz + br * 0.55)); chest = V((0, 0, hipz + br + cr * 0.55))
    neck_top = chest + V((0, -0.02, cr * 0.75))
    head = neck_top + V((0, -0.02, head_r * 0.85))
    snout_tip = head + V((0, -head_r - snout, -head_r * 0.3))
    b = Blob("Koerper")
    b.ball(belly, br, (1.0, 0.95, 1.05)); b.ball(chest, cr, (1.1, 0.9, 0.9)); b.ball(hips, br * 0.8, (1.1, 0.95, 0.7))
    b.capsule(chest, neck_top, cr * 0.5)
    b.ball(head, head_r, (1.05, 0.98, 0.95))
    for sx in (-1, 1):
        b.ball(head + V((sx * head_r * 0.62, -head_r * 0.1, -head_r * 0.38)), head_r * 0.42, (1.1, 0.8, 0.8))
    if snout > 0.01:
        b.capsule(head + V((0, -head_r * 0.6, -head_r * 0.25)), snout_tip, snout_r)
    arms = {}; legs = {}
    for side, sx in (("L", 1), ("R", -1)):
        sh = chest + V((sx * sw, 0, cr * 0.25))
        el = sh + V((sx * 0.06, -0.02, -arm_len * 0.5))
        hand = sh + V((sx * 0.09, -0.06, -arm_len))
        b.ball(sh, arm_r * 1.7, (1, 1, 1))
        b.chain([sh, el, hand], [arm_r * 1.25, arm_r, arm_r * 0.95])
        b.ball(hand, arm_r * s.get("hand", 1.5), (1, 1, 1))
        arms[side] = (sh, el, hand)
        top = hips + V((sx * br * 0.5, 0, 0))
        knee = V((sx * br * 0.55, -0.02, leg_len * 0.5 + 0.03)); ankle = V((sx * br * 0.55, 0, 0.06))
        b.chain([top, knee, ankle], [arm_r * 1.6, arm_r * 1.3, arm_r * 1.2])
        b.ball(ankle + V((0, -0.05, -0.02)), arm_r * 1.4, (1.0, 1.5, 0.6))
        legs[side] = (top, knee, ankle, ankle + V((0, -0.1, -0.03)))
    tail_pts = []
    if s.get("tail_len", 0) > 0:
        tl = s["tail_len"]; tb = hips + V((0, br * 0.7, 0.0))
        tail_pts = [tb + V((0, tl * k / 3, tl * 0.5 * math.sin(k / 3 * 1.4))) for k in range(4)]
        tr_ = s.get("tail_r", 0.08)
        b.chain(tail_pts, [tr_ * 0.6, tr_, tr_ * 1.1, tr_ * 0.6] if s.get("tail_fluffy", True) else [tr_, tr_ * 0.75, tr_ * 0.5, tr_ * 0.3])
    body = b.to_mesh(voxel=s.get("voxel", 0.016))
    zones = [(m.belly, tuple(belly + V((0, -br * 0.6, 0))), (br * 0.75, br * 0.5, br * 0.95)),
             (m.belly, tuple(head + V((0, -head_r * 0.55, -head_r * 0.45))), (head_r * 0.7, head_r * 0.5, head_r * 0.4))]
    for side in ("L", "R"):
        zones.append((m.dark, tuple(arms[side][2]), (arm_r * 2.2,) * 3))
        zones.append((m.dark, tuple(legs[side][2] + V((0, -0.03, 0))), (arm_r * 2.3, arm_r * 3, 0.08)))
    if s.get("mask"):
        zones.append((m.dark, tuple(head + V((0, -head_r * 0.6, head_r * 0.15))), (head_r * 0.95, head_r * 0.4, head_r * 0.28)))
    if tail_pts and s.get("tail_tip", True):
        zones.append((m.glow if s.get("tail_tip_glow") else m.acc, tuple(tail_pts[-1]), (0.12, 0.12, 0.12)))
    vcol_zones(body, m.body, zones, soft=0.3)
    parts = head_parts(body, s, m, head, head_r, snout_tip if snout > 0.01 else None, snout_r, chest, cr)
    parts += body_accents(body, s, m, chest + V((0, cr * 0.2, 0)), back_dir=V((0, 1, 0.4)), span=0.25, count=s.get("back_n", 3))
    if s.get("claws"):
        for side, sx in (("L", 1), ("R", -1)):
            for k in range(3):
                c = horn(f"Kralle{side}{k}", arm_r * 1.2, arm_r * 0.35, 0.3, segs=6, material=m.horn)
                cp, _ = surface(body, arms[side][2], V(((k - 1) * 0.5, -1, -0.6)), embed=0.01)
                orient(c, V(((k - 1) * 0.3, -1, -0.8)), cp); c["arm"] = side; parts.append(c)
    wing_parts = wings(body, s, m, chest + V((0, cr * 0.5, cr * 0.3)), s.get("wing_span", 0.5), s.get("wing_chord", 0.32), s.get("wing_kind", "feather")) if s.get("wings") else []
    apply_tf(parts + wing_parts)
    rig = Rig()
    rig.bone("Root", (0, 0, 0), (0, 0, 0.25), deform=False)
    rig.bone("Hips", hips, belly, "Root"); rig.bone("Chest", belly, chest + V((0, 0, cr * 0.4)), "Hips")
    rig.bone("Head", neck_top, head + V((0, -head_r * 0.3, head_r * 0.2)), "Chest")
    for side in ("L", "R"):
        sh, el, hand = arms[side]
        rig.bone(f"Arm{side}1", sh, el, "Chest"); rig.bone(f"Arm{side}2", el, hand, f"Arm{side}1")
        top, knee, ankle, toe = legs[side]
        rig.bone(f"Leg{side}1", top, knee, "Hips"); rig.bone(f"Leg{side}2", knee, ankle, f"Leg{side}1"); rig.bone(f"Leg{side}3", ankle, toe, f"Leg{side}2")
    for i in range(len(tail_pts) - 1):
        rig.bone(f"Tail{i}", tail_pts[i], tail_pts[i + 1], "Hips" if i == 0 else f"Tail{i - 1}")
    for w in wing_parts:
        sx = w["side"]; base = chest + V((sx * cr * 0.5, cr * 0.4, cr * 0.3))
        rig.bone("WingL" if sx > 0 else "WingR", base, base + V((0, -0.15, 0)), "Chest")
    rig.build()
    skin(body, rig, bones=[x[0] for x in rig.bones if x[4] and not x[0].startswith("Wing")])
    for p in parts:
        bone = "Head"
        if p.name.startswith(("Stachel", "Kristall", "RFlamme", "RBlatt", "Panzer", "Brust")): bone = "Chest"
        if p.name.startswith("Kralle"): bone = f"Arm{p['arm']}2"
        skin(p, rig, rigid=bone)
    for w in wing_parts: skin(w, rig, rigid="WingL" if w["side"] > 0 else "WingR")
    biped_anims(rig.obj, bool(wing_parts))
    return rig, [finalize([body] + parts + wing_parts, rig)]


def biped_anims(r, has_w):
    a = Anim(r, "Idle", 60)
    a.curve("Hips", lambda t: (None, (0, 0.012 * sin(t), 0), None), rot=False, loc=True)
    a.curve("Chest", lambda t: ((2 * sin(t), 0, 0), None, (1 + 0.025 * sin(t),) * 3), scl=True)
    a.curve("Head", lambda t: ((3 * sin(t + 0.3), 0, 4 * sin(t * 0.5 + 0.1)), None, None))
    a.curve("ArmL1", lambda t: ((5 * sin(t), 0, -4), None, None)); a.curve("ArmR1", lambda t: ((5 * sin(t), 0, 4), None, None))
    if "Tail0" in r.pose.bones: a.curve("Tail0", lambda t: ((0, 0, 15 * sin(t)), None, None))
    if has_w: wing_flap(a, 60, 10, 1)
    a.done()
    a = Anim(r, "Walk", 24)
    a.curve("LegL1", lambda t: ((30 * sin(t), 0, 0), None, None)); a.curve("LegR1", lambda t: ((-30 * sin(t), 0, 0), None, None))
    a.curve("LegL2", lambda t: ((-25 * max(0, sin(t + 0.25)), 0, 0), None, None)); a.curve("LegR2", lambda t: ((-25 * max(0, -sin(t + 0.25)), 0, 0), None, None))
    a.curve("ArmL1", lambda t: ((-25 * sin(t), 0, 0), None, None)); a.curve("ArmR1", lambda t: ((25 * sin(t), 0, 0), None, None))
    a.curve("Hips", lambda t: ((0, 0, 6 * sin(t)), (0, 0.02 * abs(sin(t)), 0), None), loc=True)
    a.done()
    a = Anim(r, "Attack", 22)  # Schlag mit rechts
    a.key("Root", 0, loc=(0, 0, 0)); a.key("Root", 10, loc=(0, 0, 0.25)); a.key("Root", 22, loc=(0, 0, 0))
    a.key("Chest", 0, rot=(0, 0, 0)); a.key("Chest", 7, rot=(0, 25, 0)); a.key("Chest", 11, rot=(15, -20, 0)); a.key("Chest", 22, rot=(0, 0, 0))
    a.key("ArmR1", 0, rot=(0, 0, 0)); a.key("ArmR1", 7, rot=(-60, 0, 20)); a.key("ArmR1", 11, rot=(80, 0, 0)); a.key("ArmR1", 22, rot=(0, 0, 0))
    a.key("ArmR2", 0, rot=(0, 0, 0)); a.key("ArmR2", 7, rot=(-70, 0, 0)); a.key("ArmR2", 11, rot=(0, 0, 0))
    a.done()
    a = Anim(r, "Punch", 34)  # beide Arme hoch und auf den Boden schlagen
    for side, sg in (("L", -1), ("R", 1)):
        a.key(f"Arm{side}1", 0, rot=(0, 0, 0)); a.key(f"Arm{side}1", 12, rot=(-150, 0, 0)); a.key(f"Arm{side}1", 20, rot=(60, 0, 0)); a.key(f"Arm{side}1", 34, rot=(0, 0, 0))
    a.key("Chest", 0, rot=(0, 0, 0)); a.key("Chest", 12, rot=(-15, 0, 0)); a.key("Chest", 20, rot=(30, 0, 0)); a.key("Chest", 34, rot=(0, 0, 0))
    a.key("Root", 0, loc=(0, 0, 0)); a.key("Root", 12, loc=(0, 0.12, 0)); a.key("Root", 20, loc=(0, -0.03, 0.05)); a.key("Root", 34, loc=(0, 0, 0))
    if has_w: wing_flap(a, 34, 45, 2)
    a.done()
    hit_die_dance(r, has_w, legs=(), fall_axis=0)


# ===========================================================================
# Vogel
# ===========================================================================

def build_bird(s):
    m = M(s)
    br = s.get("body_r", 0.24); head_r = s.get("head_r", 0.22); H = s.get("body_h", 0.38)
    body_c = V((0, 0.02, H)); head = body_c + V((0, -br * 0.55, br * 1.05))
    b = Blob("Koerper")
    b.ball(body_c, br, (0.95, 1.15, 1.0)); b.ball(body_c + V((0, -br * 0.4, br * 0.2)), br * 0.75, (0.95, 0.9, 1.0))
    b.capsule(body_c + V((0, -br * 0.3, br * 0.4)), head, br * 0.45)
    b.ball(head, head_r, (1.0, 1.0, 1.0))
    legs = {}
    for side, sx in (("L", 1), ("R", -1)):
        top = body_c + V((sx * br * 0.4, 0.02, -br * 0.6)); ank = V((sx * br * 0.42, 0.0, 0.05))
        b.chain([top, ank], [0.04, 0.03])
        legs[side] = (top, ank)
    body = b.to_mesh(voxel=s.get("voxel", 0.016))
    zones = [(m.belly, tuple(body_c + V((0, -br * 0.65, -br * 0.1))), (br * 0.7, br * 0.5, br * 0.85))]
    for side in ("L", "R"): zones.append((m.beak, tuple(legs[side][1] + V((0, 0, 0.05))), (0.06, 0.06, br * 0.55)))
    if s.get("mask"): zones.append((m.dark, tuple(head + V((0, -head_r * 0.6, head_r * 0.15))), (head_r * 0.95, head_r * 0.4, head_r * 0.28)))
    vcol_zones(body, m.body, zones, soft=0.3)
    s2 = dict(s); s2.setdefault("ears", "none"); s2.setdefault("tufts", False); s2.setdefault("eye", 0.4)
    parts = head_parts(body, s2, m, head, head_r, None, 0.08, None, br, beak=True)
    feet = []
    for side in ("L", "R"):
        for k in range(3):
            t = horn(f"Zeh{side}{k}", 0.09, 0.02, 0.1, segs=6, material=m.beak)
            orient(t, V(((k - 1) * 0.5, -1, -0.1)), legs[side][1] + V((0, 0, -0.03))); t["leg"] = side; feet.append(t)
    tail = []
    for k in range(3):
        t = leaf(f"Feder{k}", br * 1.1, br * 0.45, 0.03, 0.1, m.acc, tip_bend=0.1)
        tp, _ = surface(body, body_c, V(((k - 1) * 0.3, 1, -0.1)), embed=0.04)
        orient(t, V(((k - 1) * 0.35, 1, 0.25)), tp); tail.append(t)
    wing_parts = wings(body, s, m, body_c + V((0, -0.05, br * 0.3)), s.get("wing_span", 0.5), s.get("wing_chord", 0.36), "feather")
    apply_tf(parts + feet + tail + wing_parts)
    rig = Rig()
    rig.bone("Root", (0, 0, 0), (0, 0, 0.25), deform=False)
    rig.bone("Body", body_c + V((0, br * 0.6, 0)), body_c + V((0, -br * 0.4, br * 0.4)), "Root")
    rig.bone("Head", body_c + V((0, -br * 0.4, br * 0.6)), head + V((0, -head_r * 0.4, head_r * 0.3)), "Body")
    for side in ("L", "R"):
        top, ank = legs[side]
        rig.bone(f"Leg{side}1", top, ank, "Body")
    rig.bone("Tail0", body_c + V((0, br * 0.6, 0)), body_c + V((0, br * 1.4, br * 0.3)), "Body")
    for w in wing_parts:
        sx = w["side"]; base = body_c + V((sx * br * 0.7, -0.03, br * 0.35))
        rig.bone("WingL" if sx > 0 else "WingR", base, base + V((0, -0.15, 0)), "Body")
    rig.build()
    skin(body, rig, bones=["Body", "Head", "LegL1", "LegR1"])
    for p in parts: skin(p, rig, rigid="Head")
    for f in feet: skin(f, rig, rigid=f"Leg{f['leg']}1")
    for t in tail: skin(t, rig, rigid="Tail0")
    for w in wing_parts: skin(w, rig, rigid="WingL" if w["side"] > 0 else "WingR")
    bird_anims(rig.obj, s)
    return rig, [finalize([body] + parts + feet + tail + wing_parts, rig)]


def bird_anims(r, s):
    hover = s.get("hover", 0.0)
    a = Anim(r, "Idle", 40)
    a.curve("Root", lambda t: ((0, 0, 0), (0, hover + 0.03 * sin(t) * (1 if hover else 0.3), 0), None), loc=True)
    a.curve("Body", lambda t: ((3 * sin(t), 0, 0), None, (1 + 0.03 * sin(t),) * 3), scl=True)
    a.curve("Head", lambda t: ((4 * sin(t * 2), 0, 10 * sin(t)), None, None))
    if hover: wing_flap(a, 40, 40, 2)
    else: wing_flap(a, 40, 6, 1)
    a.curve("Tail0", lambda t: ((6 * sin(t), 0, 0), None, None))
    a.done()
    a = Anim(r, "Walk", 20)
    a.curve("Root", lambda t: ((0, 0, 0), (0, hover + 0.05 * abs(sin(t)), 0), None), loc=True)
    a.curve("LegL1", lambda t: ((25 * sin(t), 0, 0), None, None)); a.curve("LegR1", lambda t: ((-25 * sin(t), 0, 0), None, None))
    wing_flap(a, 20, 30 if hover else 12, 2)
    a.done()
    a = Anim(r, "Attack", 22)  # Pick-Sturzangriff
    a.key("Root", 0, loc=(0, hover, 0)); a.key("Root", 7, loc=(0, hover + 0.15, -0.1)); a.key("Root", 12, loc=(0, hover * 0.3, 0.35)); a.key("Root", 22, loc=(0, hover, 0))
    a.key("Body", 0, rot=(0, 0, 0)); a.key("Body", 7, rot=(-15, 0, 0)); a.key("Body", 12, rot=(30, 0, 0)); a.key("Body", 22, rot=(0, 0, 0))
    a.key("Head", 0, rot=(0, 0, 0)); a.key("Head", 12, rot=(35, 0, 0)); a.key("Head", 22, rot=(0, 0, 0))
    wing_flap(a, 22, 50, 2)
    a.done()
    a = Anim(r, "Punch", 34)  # großer Flügelschlag
    a.key("Root", 0, loc=(0, hover, 0)); a.key("Root", 14, loc=(0, hover + 0.3, 0.05)); a.key("Root", 34, loc=(0, hover, 0))
    a.key("WingL", 0, rot=(0, 0, 0)); a.key("WingL", 12, rot=(0, -60, 0)); a.key("WingL", 18, rot=(0, 55, 0)); a.key("WingL", 34, rot=(0, 0, 0))
    a.key("WingR", 0, rot=(0, 0, 0)); a.key("WingR", 12, rot=(0, 60, 0)); a.key("WingR", 18, rot=(0, -55, 0)); a.key("WingR", 34, rot=(0, 0, 0))
    a.key("Body", 0, rot=(0, 0, 0)); a.key("Body", 12, rot=(-20, 0, 0)); a.key("Body", 18, rot=(15, 0, 0)); a.key("Body", 34, rot=(0, 0, 0))
    a.done()
    hit_die_dance(r, True, legs=(), fall_axis=2)


# ===========================================================================
# Schwebewesen (Geist, Irrlicht, Qualle, Elementar)
# ===========================================================================

def build_floater(s):
    m = M(s)
    br = s.get("body_r", 0.26); head_r = s.get("head_r", 0.0); H = s.get("hover_h", 0.55)
    c = V((0, 0, H))
    shape = s.get("shape", "ghost")
    if shape == "slime":
        H = br * 0.62; c = V((0, 0, H))
    b = Blob("Koerper")
    b.ball(c, br, (1.35, 1.25, 0.78) if shape == "slime" else (1.0, 0.95, 1.1))
    head = c + V((0, -0.02, br * 0.6)); hr = head_r if head_r > 0 else br * 0.85
    if head_r > 0: b.ball(head, head_r, (1, 1, 1))
    if shape == "slime":
        b.ball(c + V((0, 0.05, br * 0.75)), br * 0.35, (1, 1, 1.2))  # Tropfenspitze
        tail_pts = [c + V((0, 0.02, -br * 0.2)), c + V((0, 0.06, -br * 0.35)), c + V((0, 0.1, -br * 0.45)), c + V((0, 0.14, -br * 0.5))]
    else:
        tail_pts = [c + V((0, 0.03, -br * 0.4)), c + V((0, 0.12, -br * 1.0)), c + V((0, 0.24, -br * 1.45)), c + V((0, 0.34, -br * 1.7))]
        if s.get("tentacles"):
            n = s["tentacles"]
            for k in range(n):
                ang = k / n * math.tau
                d = V((math.cos(ang), math.sin(ang), 0))
                pts = [c + d * br * 0.5 + V((0, 0, -br * 0.4)), c + d * br * 0.8 + V((0, 0, -br * 0.9)), c + d * br * 1.05 + V((0, 0, -br * 1.35)), c + d * br * 1.25 + V((0, 0, -br * 1.55))]
                b.chain(pts, [br * 0.3, br * 0.22, br * 0.15, br * 0.08])
        else:
            b.chain(tail_pts, [br * 0.75, br * 0.45, br * 0.25, br * 0.1])
    arms = {}
    for side, sx in (("L", 1), ("R", -1)):
        a0 = c + V((sx * br * 0.85, -0.03, -br * 0.05)); a1 = a0 + V((sx * br * 0.45, -0.05, -br * 0.3))
        if s.get("arms", True) and shape != "slime":
            b.chain([a0, a1], [br * 0.28, br * 0.18])
        arms[side] = (a0, a1)
    body = b.to_mesh(voxel=s.get("voxel", 0.016))
    zones = [(m.belly, tuple(c + V((0, -br * 0.7, -br * 0.15))), (br * 0.65, br * 0.45, br * 0.6)),
             (m.glow if s.get("tail_glow", True) else m.acc, tuple(tail_pts[-1]), (br * 0.6, br * 0.9, br * 0.9))]
    vcol_zones(body, m.body, zones, soft=0.45)
    s2 = dict(s); s2.setdefault("ears", "none"); s2.setdefault("tufts", False); s2.setdefault("eye", 0.42)
    parts = head_parts(body, s2, m, head if head_r > 0 else c + V((0, 0, br * 0.25)), hr, None, 0.08, None, br)
    parts += body_accents(body, s2, m, c + V((0, br * 0.3, br * 0.4)), back_dir=V((0, 0.6, 1)), span=br * 0.9, count=s.get("back_n", 3))
    wing_parts = wings(body, s, m, c + V((0, br * 0.3, br * 0.3)), s.get("wing_span", br * 1.8), s.get("wing_chord", br * 1.2), s.get("wing_kind", "bat")) if s.get("wings") else []
    apply_tf(parts + wing_parts)
    rig = Rig()
    rig.bone("Root", (0, 0, 0), (0, 0, 0.25), deform=False)
    rig.bone("Body", c + V((0, 0, -br * 0.3)), c + V((0, 0, br * 0.5)), "Root")
    rig.bone("Head", c + V((0, 0, br * 0.3)), c + V((0, -br * 0.3, br * 1.1)), "Body")
    for side in ("L", "R"):
        rig.bone(f"Arm{side}1", *arms[side], "Body")
    for i in range(3):
        rig.bone(f"Tail{i}", tail_pts[i], tail_pts[i + 1], "Body" if i == 0 else f"Tail{i - 1}")
    for w in wing_parts:
        sx = w["side"]; base = c + V((sx * br * 0.6, br * 0.3, br * 0.3))
        rig.bone("WingL" if sx > 0 else "WingR", base, base + V((0, -0.15, 0)), "Body")
    rig.build()
    skin(body, rig, bones=[x[0] for x in rig.bones if x[4] and not x[0].startswith("Wing")])
    for w in wing_parts: skin(w, rig, rigid="WingL" if w["side"] > 0 else "WingR")
    for p in parts:
        skin(p, rig, rigid="Body" if p.name.startswith(("Stachel", "Kristall", "RFlamme", "RBlatt", "Panzer")) else "Head")
    a = Anim(rig.obj, "Idle", 50)
    a.curve("Root", lambda t: ((0, 0, 0), (0, 0.05 * sin(t), 0), None), loc=True)
    a.curve("Body", lambda t: ((0, 0, 4 * sin(t)), None, (1 + 0.04 * sin(t), 1 + 0.04 * sin(t), 1 - 0.03 * sin(t))), scl=True)
    for i in range(3): a.curve(f"Tail{i}", lambda t, i=i: ((0, 0, 20 * sin(t - 0.15 * i)), None, None))
    a.curve("ArmL1", lambda t: ((0, 15 * sin(t), 0), None, None)); a.curve("ArmR1", lambda t: ((0, -15 * sin(t), 0), None, None))
    if wing_parts: wing_flap(a, 50, 35, 4)
    a.done()
    a = Anim(rig.obj, "Walk", 30)
    a.curve("Root", lambda t: ((0, 0, 0), (0, 0.06 * sin(t), 0), None), loc=True)
    a.curve("Body", lambda t: ((12, 0, 0), None, None))
    for i in range(3): a.curve(f"Tail{i}", lambda t, i=i: ((10, 0, 25 * sin(t - 0.15 * i)), None, None))
    a.done()
    a = Anim(rig.obj, "Attack", 22)
    a.key("Root", 0, loc=(0, 0, 0)); a.key("Root", 7, loc=(0, 0.05, -0.12)); a.key("Root", 12, loc=(0, -0.05, 0.4)); a.key("Root", 22, loc=(0, 0, 0))
    a.key("Body", 0, scl=(1, 1, 1), rot=(0, 0, 0)); a.key("Body", 7, scl=(1.15, 1.15, 0.85), rot=(-10, 0, 0)); a.key("Body", 12, scl=(0.9, 0.9, 1.15), rot=(20, 0, 0)); a.key("Body", 22, scl=(1, 1, 1), rot=(0, 0, 0))
    a.done()
    a = Anim(rig.obj, "Punch", 34)
    a.key("Root", 0, loc=(0, 0, 0)); a.key("Root", 14, loc=(0, 0.25, 0)); a.key("Root", 34, loc=(0, 0, 0))
    a.key("Body", 0, scl=(1, 1, 1), rot=(0, 0, 0)); a.key("Body", 14, scl=(1.25, 1.25, 1.25), rot=(0, 0, 360)); a.key("Body", 20, scl=(0.85, 0.85, 0.85), rot=(0, 0, 360)); a.key("Body", 34, scl=(1, 1, 1), rot=(0, 0, 360))
    a.key("ArmL1", 0, rot=(0, 0, 0)); a.key("ArmL1", 14, rot=(0, -70, 0)); a.key("ArmL1", 34, rot=(0, 0, 0))
    a.key("ArmR1", 0, rot=(0, 0, 0)); a.key("ArmR1", 14, rot=(0, 70, 0)); a.key("ArmR1", 34, rot=(0, 0, 0))
    a.done()
    hit_die_dance(rig.obj, bool(wing_parts), legs=(), fall_axis=0)
    return rig, [finalize([body] + parts + wing_parts, rig)]


BUILDERS = {"quad": build_quad, "biped": build_biped, "bird": build_bird, "floater": build_floater}


def build(spec):
    reset()
    return BUILDERS[spec["type"]](spec)
