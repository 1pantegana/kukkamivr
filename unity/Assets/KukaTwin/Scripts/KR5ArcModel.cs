using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Gemello digitale del KUKA KR 5 arc con la geometria vera del robot
/// (modello della libreria RoboDK, convertito da tools/robodk_kr5arc_export.py
/// in Resources/KR5arc_mesh.bytes).
///
/// Gli angoli sono quelli del PENDANT KUKA (gradi, A1..A6): stessa convenzione
/// di $AXIS_ACT, dei punti E6AXIS e dei fine corsa di $machine.dat.
/// Posa HOME = {0, -90, 90, 0, 0, 0}: braccio verticale, avambraccio e flangia
/// in avanti, flangia a X 915, Y 0, Z 1120 mm.
///
/// Versi degli assi (-1, 1, 1, -1, 1, -1) verificati su 11 punti reali del
/// robot (programma aaa.dat): con questa convenzione posizione, orientamento e
/// bit Turn di ogni punto tornano. Vedi tools/verify_kr5arc.py.
///
/// Posiziona questo GameObject dove sta la base del robot reale: l'asse Z
/// locale (blu) e' l'avanti del robot (+X KUKA), Y locale e' l'alto (+Z KUKA).
/// </summary>
[ExecuteAlways]
public class KR5ArcModel : MonoBehaviour
{
    [Tooltip("Angoli assi A1..A6 in gradi, come sul pendant")]
    public float[] axesDeg = { 0f, -90f, 90f, 0f, 0f, 0f };

    /// <summary>Fine corsa software da $machine.dat ($SOFTN_END / $SOFTP_END), gradi.</summary>
    public static readonly float[] SoftMin = { -155f, -180f, -15f, -350f, -130f, -350f };
    public static readonly float[] SoftMax = { 155f, 65f, 158f, 350f, 130f, 350f };

    public static readonly float[] Home = { 0f, -90f, 90f, 0f, 0f, 0f };

    // DH modificata (Craig) del modello RoboDK: alpha, a (mm), theta offset, d (mm).
    // Frame base KUKA: X avanti, Y sinistra, Z alto.
    static readonly double[,] Mdh = {
        { 0.0,          0.0,   0.0,          400.0 },
        { -Math.PI / 2, 180.0, 0.0,          0.0 },
        { 0.0,          600.0, -Math.PI / 2, 0.0 },
        { -Math.PI / 2, 120.0, 0.0,          620.0 },
        { Math.PI / 2,  0.0,   0.0,          0.0 },
        { -Math.PI / 2, 0.0,   Math.PI,      0.0 },
    };
    static readonly double[] AxisSign = { -1, 1, 1, -1, 1, -1 };
    public const double FlangeMm = 115.0;

    const string ResourceName = "KR5arc_mesh";
    const string LinkPrefix = "KR5_Link";

    Transform[] links;   // 0 = base, 1..6 = assi
    Transform flange;

    /// <summary>Frame della flangia (TCP con utensile nullo), per disegnare o misurare.</summary>
    public Transform Flange { get { EnsureBuilt(); return flange; } }

    void OnEnable()
    {
        EnsureBuilt();
        Apply();
    }

    void Update()
    {
        Apply();
    }

    /// <summary>Imposta gli angoli (gradi pendant) e aggiorna subito la posa.</summary>
    public void SetAxesDeg(float[] a)
    {
        for (int i = 0; i < 6; i++) axesDeg[i] = a[i];
        Apply();
    }

    /// <summary>True se tutti gli assi sono dentro i fine corsa software.</summary>
    public static bool WithinLimits(float[] a)
    {
        for (int i = 0; i < 6; i++)
            if (a[i] < SoftMin[i] || a[i] > SoftMax[i]) return false;
        return true;
    }

    /// <summary>
    /// Cinematica diretta in coordinate KUKA: flangia X,Y,Z (mm) e A,B,C (gradi,
    /// Eulero Z-Y-X come sul pendant), con utensile nullo e $ROBROOT nullo.
    /// </summary>
    public static void FlangeKuka(float[] axes, double[] xyzabc)
    {
        double[,] t = Identity();
        for (int i = 0; i < 6; i++)
            t = Mul(t, LinkMatrix(i, axes[i]));
        t = Mul(t, Translation(0, 0, FlangeMm));
        xyzabc[0] = t[0, 3]; xyzabc[1] = t[1, 3]; xyzabc[2] = t[2, 3];
        const double r2d = 180.0 / Math.PI;
        xyzabc[3] = Math.Atan2(t[1, 0], t[0, 0]) * r2d;
        xyzabc[4] = Math.Atan2(-t[2, 0], Math.Sqrt(t[0, 0] * t[0, 0] + t[1, 0] * t[1, 0])) * r2d;
        xyzabc[5] = Math.Atan2(t[2, 1], t[2, 2]) * r2d;
    }

    // ---------------------------------------------------------------- posa

    void Apply()
    {
        if (links == null || links[1] == null) return;
        for (int i = 0; i < 6; i++)
            SetLocal(links[i + 1], LinkMatrix(i, axesDeg[i]));
    }

    static double[,] LinkMatrix(int i, float axisDeg)
    {
        double th = Mdh[i, 2] + AxisSign[i] * axisDeg * Math.PI / 180.0;
        double ca = Math.Cos(Mdh[i, 0]), sa = Math.Sin(Mdh[i, 0]);
        double ct = Math.Cos(th), st = Math.Sin(th);
        double a = Mdh[i, 1], d = Mdh[i, 3];
        return new double[,] {
            { ct,      -st,      0,   a },
            { st * ca, ct * ca, -sa, -sa * d },
            { st * sa, ct * sa,  ca,  ca * d },
            { 0, 0, 0, 1 },
        };
    }

    // KUKA (destrorso, Z alto, mm) -> Unity (mancino, Y alto, m): (x,y,z) -> (-y, z, x)
    static Vector3 ToUnity(double x, double y, double z)
    {
        return new Vector3((float)(-y * 0.001), (float)(z * 0.001), (float)(x * 0.001));
    }

    static void SetLocal(Transform tr, double[,] m)
    {
        tr.localPosition = ToUnity(m[0, 3], m[1, 3], m[2, 3]);
        // R_unity = C R C^T. C manda X KUKA -> +z Unity e Z KUKA -> +y Unity, quindi
        // forward = C * (R * X) e up = C * (R * Z): colonne 0 e 2 di R, convertite.
        Vector3 fwd = ToUnityDir(m[0, 0], m[1, 0], m[2, 0]);
        Vector3 up = ToUnityDir(m[0, 2], m[1, 2], m[2, 2]);
        tr.localRotation = Quaternion.LookRotation(fwd, up);
    }

    static Vector3 ToUnityDir(double x, double y, double z)
    {
        return new Vector3((float)-y, (float)z, (float)x);
    }

    // ---------------------------------------------------------- costruzione

    void EnsureBuilt()
    {
        if (links != null && links[0] != null) return;

        links = new Transform[7];
        Transform parent = transform;
        for (int i = 0; i < 7; i++)
        {
            Transform existing = parent.Find(LinkPrefix + i);
            links[i] = existing != null ? existing : NewChild(parent, LinkPrefix + i);
            parent = links[i];
        }
        flange = links[6].Find("Flange");
        if (flange == null)
        {
            flange = NewChild(links[6], "Flange");
            flange.localPosition = ToUnity(0, 0, FlangeMm);
            // asse Z utensile (+Z KUKA della flangia) = forward Unity del frame Flange
            flange.localRotation = Quaternion.LookRotation(ToUnityDir(0, 0, 1), ToUnityDir(1, 0, 0));
        }

        if (links[0].GetComponentInChildren<MeshFilter>(true) == null)
            LoadMeshes();
    }

    static Transform NewChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    void LoadMeshes()
    {
        var asset = Resources.Load<TextAsset>(ResourceName);
        if (asset == null)
        {
            Debug.LogError("KR5ArcModel: manca Resources/" + ResourceName + ".bytes " +
                           "(rigeneralo con tools/robodk_kr5arc_export.py)");
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        using (var r = new BinaryReader(new MemoryStream(asset.bytes)))
        {
            if (new string(r.ReadChars(4)) != "KR5M" || r.ReadInt32() != 1)
            {
                Debug.LogError("KR5ArcModel: formato mesh non riconosciuto");
                return;
            }
            int nLinks = r.ReadInt32();
            for (int li = 0; li < nLinks && li < 7; li++)
            {
                int nMesh = r.ReadInt32();
                for (int mi = 0; mi < nMesh; mi++)
                {
                    var color = new Color(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    int nv = r.ReadInt32();
                    var verts = new Vector3[nv];
                    for (int k = 0; k < nv; k++)
                        verts[k] = ToUnity(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    // il cambio di mano inverte l'ordine dei vertici dei triangoli
                    var tris = new int[nv];
                    for (int k = 0; k < nv; k += 3)
                    {
                        tris[k] = k; tris[k + 1] = k + 2; tris[k + 2] = k + 1;
                    }
                    var mesh = new Mesh { name = LinkPrefix + li + "_" + mi };
                    if (nv > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    mesh.vertices = verts;
                    mesh.triangles = tris;
                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();

                    var go = new GameObject(mesh.name);
                    go.hideFlags = HideFlags.DontSave;
                    go.transform.SetParent(links[li], false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var mat = new Material(shader) { color = color };
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                    // alcune facce del modello RoboDK hanno l'ordine dei vertici
                    // invertito: disegna entrambi i lati per non avere buchi
                    if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
                    go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                }
            }
        }
    }

    // ------------------------------------------------------ algebra minima

    static double[,] Identity()
    {
        return new double[,] { { 1, 0, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } };
    }

    static double[,] Translation(double x, double y, double z)
    {
        var t = Identity();
        t[0, 3] = x; t[1, 3] = y; t[2, 3] = z;
        return t;
    }

    static double[,] Mul(double[,] a, double[,] b)
    {
        var c = new double[4, 4];
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
                c[i, j] = a[i, 0] * b[0, j] + a[i, 1] * b[1, j] + a[i, 2] * b[2, j] + a[i, 3] * b[3, j];
        return c;
    }
}
