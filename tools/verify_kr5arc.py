"""Verifica la cinematica del gemello KR 5 arc sui punti reali del robot.

Usa gli 11 punti E6POS del programma aaa.dat registrati sul robot (cartella
KUKA/programmi), con utensile e base nulli. Per ogni punto cerca una soluzione
di cinematica inversa dentro i fine corsa di $machine.dat il cui bit Turn
coincida con quello salvato dal controller (bit i = 1 se l'asse i+1 e'
negativo). Se tutti i punti tornano, la tabella DH e i versi degli assi usati
in KR5ArcModel.cs sono quelli del controller.

Richiede numpy e scipy:  python tools/verify_kr5arc.py
"""
import math

import numpy as np
from scipy.optimize import least_squares

P = math.pi
# stessa tabella e stessi versi di unity/Assets/KukaTwin/Scripts/KR5ArcModel.cs
MDH = [(0, 0, 0, 400), (-P / 2, 180, 0, 0), (0, 600, -P / 2, 0),
       (-P / 2, 120, 0, 620), (P / 2, 0, 0, 0), (-P / 2, 0, P, 0)]
SIGN = np.array([-1, 1, 1, -1, 1, -1])
FLANGE = 115.0
LIM = np.radians([[-155, 155], [-180, 65], [-15, 158],
                  [-350, 350], [-130, 130], [-350, 350]])

POINTS = [  # nome, X, Y, Z, A, B, C, S, T
    ("XP1", 754.09198, -98.3541489, 1389.27197, -102.463097, 40.4041901, -97.3390427, 2, 34),
    ("XP2", 210.443207, -27.8919697, 852.903381, -105.266197, 40.0275993, -101.679199, 2, 34),
    ("XP3", 539.248718, -69.1984863, 963.006592, -155.494904, 82.9898529, -148.159302, 2, 2),
    ("XP4", 923.567322, -610.711975, 145.481705, 149.839401, 2.82316494, -177.943497, 2, 34),
    ("XP5", 923.567322, -610.711975, 145.481705, 149.839401, 2.82316494, -177.943497, 2, 34),
    ("XP6", 923.567017, -610.711914, 910.320984, 149.839203, 2.82313395, -177.943497, 2, 2),
    ("XP7", 918.167297, 434.190094, 910.318787, 149.839996, 2.82277989, -177.943802, 2, 35),
    ("XP8", 918.166626, 434.189911, 284.258392, 149.840393, 2.82262111, -177.943695, 2, 35),
    ("XP9", 918.167297, 434.190491, 181.459198, 149.840805, 2.82268095, -177.944, 2, 35),
    ("XP10", 918.167419, 434.190308, 418.720001, 149.840607, 2.82282901, -177.944, 2, 35),
    ("XP11", 717.285706, -20.5688, 998.552429, 149.860199, 2.82410598, -177.947601, 2, 34),
]


def mdh(al, a, th, d):
    ca, sa, ct, st = math.cos(al), math.sin(al), math.cos(th), math.sin(th)
    return np.array([[ct, -st, 0, a], [st * ca, ct * ca, -sa, -sa * d],
                     [st * sa, ct * sa, ca, ca * d], [0, 0, 0, 1]])


def fk(axes_rad):
    t = np.eye(4)
    for (al, a, th, d), s, q in zip(MDH, SIGN, axes_rad):
        t = t @ mdh(al, a, th + s * q, d)
    f = np.eye(4)
    f[2, 3] = FLANGE
    return t @ f


def rot_abc(a, b, c):
    a, b, c = map(math.radians, (a, b, c))
    rz = np.array([[math.cos(a), -math.sin(a), 0], [math.sin(a), math.cos(a), 0], [0, 0, 1]])
    ry = np.array([[math.cos(b), 0, math.sin(b)], [0, 1, 0], [-math.sin(b), 0, math.cos(b)]])
    rx = np.array([[1, 0, 0], [0, math.cos(c), -math.sin(c)], [0, math.sin(c), math.cos(c)]])
    return rz @ ry @ rx


def solve(x, y, z, a, b, c, turn):
    target_r, target_p = rot_abc(a, b, c), np.array([x, y, z])

    def residual(q):
        t = fk(q)
        return np.concatenate([(t[:3, 3] - target_p) / 100.0, (t[:3, :3] - target_r).ravel()])

    rng = np.random.default_rng(0)
    for _ in range(100):
        r = least_squares(residual, rng.uniform(LIM[:, 0], LIM[:, 1]), bounds=(LIM[:, 0], LIM[:, 1]))
        if np.abs(r.fun).max() < 1e-6:
            deg = np.degrees(r.x)
            if sum(1 << i for i in range(6) if deg[i] < 0) == turn:
                return deg
    return None


def main():
    home = fk(np.radians([0, -90, 90, 0, 0, 0]))[:3, 3]
    print("HOME flangia: X %.1f Y %.1f Z %.1f (atteso 915, 0, 1120)" % tuple(home))
    ok = 0
    for name, x, y, z, a, b, c, s, t in POINTS:
        q = solve(x, y, z, a, b, c, t)
        if q is None:
            print("%-5s NESSUNA soluzione con T=%d" % (name, t))
        else:
            ok += 1
            print("%-5s T=%2d  A1..A6 = %s" % (name, t, " ".join("%8.2f" % v for v in q)))
    print("%d/%d punti coerenti" % (ok, len(POINTS)))
    return 0 if ok == len(POINTS) else 1


if __name__ == "__main__":
    raise SystemExit(main())
