"""Converte il modello RoboDK del KUKA KR 5 arc nel file mesh del gemello Unity.

Uso:
    python tools/robodk_kr5arc_export.py                 # scarica da RoboDK
    python tools/robodk_kr5arc_export.py KUKA-KR-5-arc.robot
    python tools/robodk_kr5arc_export.py --obj out_dir   # anche un OBJ per parte

Formato .robot (ricavato dal file, RoboDK non lo documenta):
    4 byte lunghezza (qCompress) + zlib
    dentro: intestazione con tabella DH modificata, limiti, nome;
    da offset 32000: int64 n_parti, poi per parte
        int64 n_mesh, per mesh: int64 n_tri, float32 rgba[4],
        n_tri*3 vertici da 14 byte (float32 x,y,z + 2 byte normale compressa),
        poi 16 byte a zero tra una parte e la successiva.
    I vertici sono in mm nel frame della parte (DH modificata, vedi
    KR5ArcModel.cs).

Uscita: unity/Assets/KukaTwin/Resources/KR5arc_mesh.bytes
    "KR5M", int32 versione=1, int32 n_parti,
    per parte: int32 n_mesh, per mesh: float32 rgba[4], int32 n_vertici,
    float32 xyz * n_vertici (mm, frame KUKA destrorso della parte).
"""
import os
import struct
import sys
import urllib.request
import zlib

URL = "https://cdn.robodk.com/downloads-library/library-robots/KUKA-KR-5-arc.robot"
MESH_START = 32000
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "unity", "Assets", "KukaTwin", "Resources", "KR5arc_mesh.bytes")


def load_raw(path):
    if path is None:
        with urllib.request.urlopen(URL) as r:
            data = r.read()
    else:
        with open(path, "rb") as f:
            data = f.read()
    return zlib.decompress(data[4:])


def parse(raw):
    p = MESH_START

    def i64():
        nonlocal p
        v = struct.unpack_from("<q", raw, p)[0]
        p += 8
        return v

    links = []
    n_links = i64()
    if n_links != 7:
        raise ValueError("attese 7 parti (base + 6 assi), trovate %d" % n_links)
    for li in range(n_links):
        meshes = []
        for _ in range(i64()):
            n_tri = i64()
            rgba = struct.unpack_from("<4f", raw, p)
            p += 16
            verts = []
            for _ in range(n_tri * 3):
                verts.append(struct.unpack_from("<3f", raw, p))
                p += 14
            meshes.append((rgba, verts))
        links.append(meshes)
        if li < n_links - 1:
            if raw[p:p + 16] != bytes(16):
                raise ValueError("separatore inatteso dopo la parte %d" % li)
            p += 16
    return links


def write_bytes(links, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(b"KR5M" + struct.pack("<ii", 1, len(links)))
        for meshes in links:
            f.write(struct.pack("<i", len(meshes)))
            for rgba, verts in meshes:
                f.write(struct.pack("<4fi", *rgba, len(verts)))
                for v in verts:
                    f.write(struct.pack("<3f", *v))


def write_obj(links, out_dir):
    os.makedirs(out_dir, exist_ok=True)
    for li, meshes in enumerate(links):
        name = "base" if li == 0 else "A%d" % li
        with open(os.path.join(out_dir, "kr5arc_%s.obj" % name), "w") as f:
            f.write("# KUKA KR 5 arc, parte %s, mm, frame della parte\n" % name)
            n = 1
            for mi, (rgba, verts) in enumerate(meshes):
                f.write("o mesh%d\n# colore %.3f %.3f %.3f\n" % (mi, *rgba[:3]))
                for v in verts:
                    f.write("v %.4f %.4f %.4f\n" % v)
                for t in range(len(verts) // 3):
                    f.write("f %d %d %d\n" % (n, n + 1, n + 2))
                    n += 3


def main(argv):
    obj_dir = None
    if "--obj" in argv:
        i = argv.index("--obj")
        obj_dir = argv[i + 1]
        del argv[i:i + 2]
    links = parse(load_raw(argv[0] if argv else None))
    write_bytes(links, OUT)
    tris = sum(len(v) // 3 for m in links for _, v in m)
    print("scritto %s: %d parti, %d triangoli" % (os.path.normpath(OUT), len(links), tris))
    if obj_dir:
        write_obj(links, obj_dir)
        print("OBJ in", obj_dir)


if __name__ == "__main__":
    main(sys.argv[1:])
