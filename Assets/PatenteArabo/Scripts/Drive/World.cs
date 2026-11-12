using System.Collections.Generic;
using UnityEngine;

namespace PatenteArabo
{
    public enum Biome { City, Country, Highway }

    public static class Theme
    {
        public static Color Hex(string h)
        {
            ColorUtility.TryParseHtmlString("#" + h, out var c);
            return c;
        }
    }

    /// <summary>Kenney (CC0) low-poly models loaded from Resources, normalised to real-world sizes.</summary>
    public static class Models
    {
        static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, Material> KitMats = new Dictionary<string, Material>();
        static readonly Dictionary<string, (Vector3 size, Vector3 center)> Bounds = new Dictionary<string, (Vector3, Vector3)>();

        public static readonly string[] Cars = { "sedan", "sedan-sports", "hatchback-sports", "suv", "suv-luxury", "taxi", "van", "delivery", "police" };

        static Material KitMaterial(string kit)
        {
            if (KitMats.TryGetValue(kit, out var m) && m != null) return m;
            var tex = Resources.Load<Texture2D>("PatenteArabo/Models/" + kit + "/Textures/colormap");
            if (tex == null) return null;
            m = new Material(Mats.Lit(Color.white, 0.3f));
            m.mainTexture = tex;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            m.color = Color.white;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            KitMats[kit] = m;
            return m;
        }

        /// <summary>Instantiates kit/name scaled so that its largest horizontal size (or height) equals size metres. Returns null if missing.</summary>
        public static GameObject Spawn(string kit, string name, Transform parent, Vector3 pos, float rotY, float size, bool byHeight = false)
        {
            string key = kit + "/" + name;
            if (!Prefabs.TryGetValue(key, out var prefab))
            {
                prefab = Resources.Load<GameObject>("PatenteArabo/Models/" + key);
                Prefabs[key] = prefab;
            }
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, parent, false);
            go.name = name;
            var kitMat = KitMaterial(kit);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (kitMat != null)
                {
                    var arr = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < arr.Length; i++) arr[i] = kitMat;
                    r.sharedMaterials = arr;
                }
                else
                {
                    // nature kit uses plain coloured materials: rebuild them with our URP template so they always render
                    var src = r.sharedMaterials;
                    var arr = new Material[src.Length];
                    for (int i = 0; i < src.Length; i++)
                    {
                        Color c = Color.gray;
                        if (src[i] != null)
                        {
                            if (src[i].HasProperty("_BaseColor")) c = src[i].GetColor("_BaseColor");
                            else if (src[i].HasProperty("_Color")) c = src[i].color;
                        }
                        arr[i] = Mats.Lit(c, 0.1f);
                    }
                    r.sharedMaterials = arr;
                }
            }
            if (!Bounds.TryGetValue(key, out var b))
            {
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                var rs = go.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0)
                {
                    var bb = rs[0].bounds;
                    foreach (var r in rs) bb.Encapsulate(r.bounds);
                    var inv = go.transform.parent != null ? go.transform.parent.InverseTransformVector(bb.size) : bb.size;
                    var c = go.transform.parent != null ? go.transform.parent.InverseTransformPoint(bb.center) : bb.center;
                    b = (new Vector3(Mathf.Abs(inv.x), Mathf.Abs(inv.y), Mathf.Abs(inv.z)), c - go.transform.localPosition);
                }
                else b = (Vector3.one, Vector3.zero);
                Bounds[key] = b;
            }
            float baseSize = byHeight ? b.size.y : Mathf.Max(b.size.x, b.size.z);
            float k = baseSize > 0.0001f ? size / baseSize : 1f;
            go.transform.localScale = Vector3.one * k;
            go.transform.localRotation = Quaternion.Euler(0, rotY, 0);
            // put the model's bottom on the ground at pos
            var bottom = new Vector3(b.center.x, b.center.y - b.size.y / 2f, b.center.z) * k;
            go.transform.localPosition = pos - Quaternion.Euler(0, rotY, 0) * new Vector3(bottom.x, 0, bottom.z) - new Vector3(0, bottom.y, 0);
            return go;
        }
    }

    /// <summary>Material cache built from template materials in Resources (so shaders survive in builds).</summary>
    public static class Mats
    {
        static Material _lit, _unlit, _sign;
        static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        static Material Template(ref Material field, string res, string shader)
        {
            if (field != null) return field;
            field = Resources.Load<Material>(res);
            if (field == null)
            {
                var sh = Shader.Find(shader);
                if (sh == null) sh = Shader.Find("Standard");
                field = new Material(sh);
            }
            return field;
        }

        public static Material Lit(Color c, float smooth = 0.15f)
        {
            string key = "L" + ColorUtility.ToHtmlStringRGBA(c) + smooth;
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Template(ref _lit, "PatenteArabo/Mat_Lit", "Universal Render Pipeline/Lit"));
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            Cache[key] = m;
            return m;
        }

        public static Material Unlit(Color c)
        {
            string key = "U" + ColorUtility.ToHtmlStringRGBA(c);
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Template(ref _unlit, "PatenteArabo/Mat_Unlit", "Universal Render Pipeline/Unlit"));
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            Cache[key] = m;
            return m;
        }

        public static Material Sign(string id)
        {
            string key = "S" + id;
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Template(ref _sign, "PatenteArabo/Mat_Sign", "Universal Render Pipeline/Unlit"));
            var tex = SignFactory.GetTexture(id);
            m.mainTexture = tex;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            m.color = Color.white;
            Cache[key] = m;
            return m;
        }
    }

    /// <summary>Primitive-building helpers.</summary>
    public static class Build
    {
        public static GameObject Prim(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material mat, string name = null)
        {
            var go = GameObject.CreatePrimitive(t);
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            if (name != null) go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            // small parts (windows, markings, poles) don't cast shadows: far fewer draw calls on phones
            if (t == PrimitiveType.Quad || Mathf.Min(scale.x, Mathf.Min(scale.y, scale.z)) < 0.5f)
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public static GameObject Box(Transform parent, Vector3 pos, Vector3 scale, Color c, float smooth = 0.15f) =>
            Prim(PrimitiveType.Cube, parent, pos, scale, Mats.Lit(c, smooth));

        public static GameObject Group(string name, Transform parent, Vector3 pos)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            return g;
        }

        /// <summary>A road sign on a pole facing the driver (who looks toward +Z).</summary>
        public static GameObject SignPost(Transform parent, Vector3 basePos, string signId, float size = 1.1f, float height = 2.6f)
        {
            var g = Group("Sign_" + signId, parent, basePos);
            Prim(PrimitiveType.Cylinder, g.transform, new Vector3(0, height / 2f, 0.04f), new Vector3(0.09f, height / 2f, 0.09f), Mats.Lit(new Color(0.55f, 0.57f, 0.6f), 0.5f));
            var face = Prim(PrimitiveType.Quad, g.transform, new Vector3(0, height + size * 0.4f, 0), new Vector3(size, size, 1), Mats.Sign(signId), "Face");
            face.transform.localRotation = Quaternion.identity; // quad faces -Z (toward the driver)
            var back = Prim(PrimitiveType.Quad, g.transform, new Vector3(0, height + size * 0.4f, 0.01f), new Vector3(size * 0.98f, size * 0.98f, 1),
                Mats.Lit(new Color(0.6f, 0.62f, 0.65f)), "Back");
            back.transform.localRotation = Quaternion.Euler(0, 180, 0);
            return g;
        }

        /// <summary>A car facing +Z. model = Kenney car name (null = random).</summary>
        public static GameObject Car(Transform parent, Vector3 pos, Color body, string name = "Car", string model = null)
        {
            var g = Group(name, parent, pos);
            model = model ?? Models.Cars[Random.Range(0, Models.Cars.Length)];
            if (Models.Spawn("Cars", model, g.transform, Vector3.zero, 0f, 4.3f) != null) return g;
            Box(g.transform, new Vector3(0, 0.62f, 0), new Vector3(1.8f, 0.62f, 4.2f), body, 0.7f);
            Box(g.transform, new Vector3(0, 1.17f, -0.25f), new Vector3(1.55f, 0.52f, 2.1f), body * 0.92f, 0.7f);
            // windows
            var glass = new Color(0.12f, 0.16f, 0.22f);
            Box(g.transform, new Vector3(0, 1.18f, 0.83f), new Vector3(1.45f, 0.42f, 0.05f), glass, 0.9f);
            Box(g.transform, new Vector3(0, 1.18f, -1.31f), new Vector3(1.45f, 0.42f, 0.05f), glass, 0.9f);
            Box(g.transform, new Vector3(0.785f, 1.18f, -0.25f), new Vector3(0.02f, 0.4f, 1.9f), glass, 0.9f);
            Box(g.transform, new Vector3(-0.785f, 1.18f, -0.25f), new Vector3(0.02f, 0.4f, 1.9f), glass, 0.9f);
            // lights
            Prim(PrimitiveType.Cube, g.transform, new Vector3(0.6f, 0.68f, 2.11f), new Vector3(0.38f, 0.14f, 0.04f), Mats.Unlit(new Color(1f, 0.97f, 0.85f)));
            Prim(PrimitiveType.Cube, g.transform, new Vector3(-0.6f, 0.68f, 2.11f), new Vector3(0.38f, 0.14f, 0.04f), Mats.Unlit(new Color(1f, 0.97f, 0.85f)));
            Prim(PrimitiveType.Cube, g.transform, new Vector3(0.62f, 0.72f, -2.11f), new Vector3(0.34f, 0.14f, 0.04f), Mats.Unlit(new Color(0.85f, 0.05f, 0.05f)), "BrakeR");
            Prim(PrimitiveType.Cube, g.transform, new Vector3(-0.62f, 0.72f, -2.11f), new Vector3(0.34f, 0.14f, 0.04f), Mats.Unlit(new Color(0.85f, 0.05f, 0.05f)), "BrakeL");
            // wheels
            var tyre = Mats.Lit(new Color(0.07f, 0.07f, 0.07f));
            foreach (var x in new[] { -0.86f, 0.86f })
            foreach (var z in new[] { -1.35f, 1.35f })
            {
                var w = Prim(PrimitiveType.Cylinder, g.transform, new Vector3(x, 0.34f, z), new Vector3(0.68f, 0.13f, 0.68f), tyre, "Wheel");
                w.transform.localRotation = Quaternion.Euler(0, 0, 90);
            }
            return g;
        }

        public static GameObject Person(Transform parent, Vector3 pos, Color shirt)
        {
            var g = Group("Pedestrian", parent, pos);
            Prim(PrimitiveType.Capsule, g.transform, new Vector3(0, 0.45f, 0), new Vector3(0.32f, 0.45f, 0.32f), Mats.Lit(new Color(0.15f, 0.18f, 0.3f)), "Legs");
            Prim(PrimitiveType.Capsule, g.transform, new Vector3(0, 1.2f, 0), new Vector3(0.48f, 0.38f, 0.32f), Mats.Lit(shirt), "Body");
            Prim(PrimitiveType.Sphere, g.transform, new Vector3(0, 1.72f, 0), new Vector3(0.27f, 0.3f, 0.27f), Mats.Lit(new Color(0.78f, 0.6f, 0.45f)), "Head");
            return g;
        }

        // ---------------- low-poly props (faceted meshes made once, shared by every copy) ----------------
        static Mesh _blob, _cone;

        /// <summary>Faceted, slightly irregular sphere (flat shading): tree crowns, bushes, clouds.</summary>
        public static Mesh Blob
        {
            get
            {
                if (_blob != null) return _blob;
                // icosahedron subdivided once = 80 flat faces
                float t = (1f + Mathf.Sqrt(5f)) / 2f;
                var v = new List<Vector3> {
                    new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                    new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                    new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1) };
                int[] f = { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
                var tris = new List<int>();
                var mid = new Dictionary<long, int>();
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (mid.TryGetValue(key, out int m)) return m;
                    v.Add((v[a] + v[b]) * 0.5f); mid[key] = v.Count - 1; return v.Count - 1;
                }
                for (int k = 0; k < f.Length; k += 3)
                {
                    int a = f[k], b = f[k + 1], c = f[k + 2], ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    tris.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                var rnd = new System.Random(11);
                for (int k = 0; k < v.Count; k++) v[k] = v[k].normalized * (0.5f * (0.88f + 0.24f * (float)rnd.NextDouble()));
                _blob = Flat(v, tris, "Blob");
                return _blob;
            }
        }

        /// <summary>Six-sided cone (spires, pinnacles).</summary>
        public static Mesh Cone
        {
            get
            {
                if (_cone != null) return _cone;
                var v = new List<Vector3> { new Vector3(0, 1, 0) };
                for (int k = 0; k < 6; k++) { float a = k * Mathf.PI / 3f; v.Add(new Vector3(Mathf.Cos(a) * 0.5f, 0, Mathf.Sin(a) * 0.5f)); }
                var tris = new List<int>();
                for (int k = 0; k < 6; k++) { int a = 1 + k, b = 1 + (k + 1) % 6; tris.AddRange(new[] { 0, b, a }); }
                _cone = Flat(v, tris, "Cone");
                return _cone;
            }
        }

        static Mesh Flat(List<Vector3> v, List<int> tris, string name)
        {
            // one vertex per corner of each face = sharp low-poly facets
            var verts = new Vector3[tris.Count];
            var idx = new int[tris.Count];
            for (int k = 0; k < tris.Count; k++) { verts[k] = v[tris[k]]; idx[k] = k; }
            var m = new Mesh { name = name };
            m.vertices = verts; m.triangles = idx;
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        public static GameObject MeshObj(Transform parent, Mesh mesh, Vector3 pos, Vector3 scale, Material mat, bool shadows = true, string name = "Mesh")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            if (!shadows) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static readonly Color[] Leaves = { Theme.Hex("5FA548"), Theme.Hex("6DB352"), Theme.Hex("4E9440"), Theme.Hex("7ABF5A") };

        public static void LowPolyTree(Transform parent, Vector3 basePos, float height, System.Random rnd)
        {
            float trunkH = height * 0.42f;
            Prim(PrimitiveType.Cylinder, parent, basePos + new Vector3(0, trunkH / 2f, 0), new Vector3(0.28f, trunkH / 2f, 0.28f), Mats.Lit(Theme.Hex("7A5434"), 0.05f));
            float cw = height * 0.62f;
            var leaf = Mats.Lit(Leaves[rnd.Next(Leaves.Length)], 0.02f);
            var crown = MeshObj(parent, Blob, basePos + new Vector3(0, trunkH + cw * 0.38f, 0), new Vector3(cw, cw * 0.95f, cw), leaf, true, "Crown");
            crown.transform.localRotation = Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0);
        }

        public static void LowPolyBush(Transform parent, Vector3 basePos, float size, System.Random rnd)
        {
            var o = MeshObj(parent, Blob, basePos + new Vector3(0, size * 0.35f, 0), new Vector3(size * 1.5f, size, size * 1.3f), Mats.Lit(Leaves[rnd.Next(Leaves.Length)], 0.02f), false, "Bush");
            o.transform.localRotation = Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0);
        }

        public static void Cloud(Transform parent, Vector3 pos, float size, System.Random rnd)
        {
            var white = Mats.Lit(new Color(0.97f, 0.98f, 1f), 0.0f);
            for (int k = 0; k < 3; k++)
            {
                float s = size * (k == 1 ? 1f : 0.7f);
                var o = MeshObj(parent, Blob, pos + new Vector3((k - 1) * size * 0.55f, k == 1 ? size * 0.12f : 0, 0), new Vector3(s, s * 0.45f, s * 0.6f), white, false, "Cloud");
                o.transform.localRotation = Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0);
            }
        }

        /// <summary>Black cast-iron street lamp with a warm lantern.</summary>
        public static void ClassicLamp(Transform parent, Vector3 basePos)
        {
            var iron = Mats.Lit(Theme.Hex("1E2226"), 0.45f);
            Prim(PrimitiveType.Cylinder, parent, basePos + new Vector3(0, 0.25f, 0), new Vector3(0.32f, 0.25f, 0.32f), iron);
            Prim(PrimitiveType.Cylinder, parent, basePos + new Vector3(0, 2.4f, 0), new Vector3(0.13f, 2.1f, 0.13f), iron);
            Prim(PrimitiveType.Cube, parent, basePos + new Vector3(0, 4.75f, 0), new Vector3(0.42f, 0.55f, 0.42f), Mats.Unlit(Theme.Hex("FFE7A8")));
            MeshObj(parent, Cone, basePos + new Vector3(0, 5.02f, 0), new Vector3(0.62f, 0.4f, 0.62f), iron, false, "LampCap");
        }

        /// <summary>A Duomo-like gothic cathedral (white marble facade, many spires) seen at the end of the street.</summary>
        public static GameObject Cathedral(Transform parent)
        {
            var g = Group("Cathedral", parent, Vector3.zero);
            var t = g.transform;
            var marble = Mats.Lit(Theme.Hex("ECE6DA"), 0.1f);
            var shade = Mats.Lit(Theme.Hex("D6CEBF"), 0.1f);
            var dark = Mats.Lit(Theme.Hex("5D6470"), 0.3f);
            var gold = Mats.Unlit(Theme.Hex("E8C35A"));
            // nave behind and the facade
            Prim(PrimitiveType.Cube, t, new Vector3(0, 9f, 14f), new Vector3(15f, 18f, 26f), shade);
            Prim(PrimitiveType.Cube, t, new Vector3(0, 8f, 0), new Vector3(16f, 16f, 2f), marble);
            // gable
            var gable = MeshObj(t, Cone, new Vector3(0, 16f, 0), new Vector3(16f, 7f, 2f), marble, true, "Gable");
            gable.transform.localRotation = Quaternion.Euler(0, 30, 0);
            // tall windows and portals
            for (int k = -2; k <= 2; k++)
            {
                Prim(PrimitiveType.Cube, t, new Vector3(k * 3f, 9.5f, -1.05f), new Vector3(1.1f, 5f, 0.1f), dark);
                Prim(PrimitiveType.Cube, t, new Vector3(k * 3f, 2.2f, -1.05f), new Vector3(k == 0 ? 2.2f : 1.5f, k == 0 ? 4.4f : 3.4f, 0.1f), dark);
            }
            // pinnacles along the facade and the roof, a tall central spire with the golden statue
            for (int k = -3; k <= 3; k++)
            {
                float x = k * 2.6f, hgt = k == 0 ? 0 : 3.5f + (3 - Mathf.Abs(k)) * 1.2f;
                if (k == 0) continue;
                Prim(PrimitiveType.Cube, t, new Vector3(x, 16f + hgt / 2f, -0.4f), new Vector3(0.6f, hgt, 0.6f), marble);
                MeshObj(t, Cone, new Vector3(x, 16f + hgt, -0.4f), new Vector3(0.75f, 2.4f, 0.75f), marble, true, "Pinnacle");
            }
            for (int r = 0; r < 4; r++)
            for (int side = -1; side <= 1; side += 2)
            {
                float z = 4f + r * 6f;
                Prim(PrimitiveType.Cube, t, new Vector3(side * 7.2f, 20f, z), new Vector3(0.5f, 4f, 0.5f), marble);
                MeshObj(t, Cone, new Vector3(side * 7.2f, 22f, z), new Vector3(0.65f, 2.2f, 0.65f), marble, true, "Pinnacle");
            }
            Prim(PrimitiveType.Cylinder, t, new Vector3(0, 24f, 14f), new Vector3(3.2f, 6f, 3.2f), marble);
            MeshObj(t, Cone, new Vector3(0, 30f, 14f), new Vector3(3.4f, 14f, 3.4f), marble, true, "Spire");
            Prim(PrimitiveType.Sphere, t, new Vector3(0, 44.6f, 14f), new Vector3(0.8f, 1.4f, 0.8f), gold);
            return g;
        }

        /// <summary>Merges the static boxes of a road chunk into one mesh per material: hundreds of draw calls become a few.</summary>
        public static void Combine(GameObject root)
        {
            var groups = new Dictionary<(Material, UnityEngine.Rendering.ShadowCastingMode), List<CombineInstance>>();
            var used = new List<GameObject>();
            var toLocal = root.transform.worldToLocalMatrix;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                var mr = mf.GetComponent<MeshRenderer>();
                var mesh = mf.sharedMesh;
                if (mr == null || mesh == null || !mesh.isReadable || mr.sharedMaterials.Length != 1 || mf.transform.childCount > 0) continue;
                var key = (mr.sharedMaterial, mr.shadowCastingMode);
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mesh, transform = toLocal * mf.transform.localToWorldMatrix });
                used.Add(mf.gameObject);
            }
            foreach (var kv in groups)
            {
                var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, name = "Combined" };
                m.CombineMeshes(kv.Value.ToArray(), true, true);
                var go = new GameObject("Combined");
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = m;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = kv.Key.Item1;
                r.shadowCastingMode = kv.Key.Item2;
                go.AddComponent<OwnedMesh>().Mesh = m;
            }
            foreach (var u in used) Object.Destroy(u);
        }
    }

    /// <summary>Generates endless straight road chunks along +Z with biome-specific scenery.</summary>
    /// <summary>Frees a mesh made at runtime together with its object (combined chunk meshes).</summary>
    public class OwnedMesh : MonoBehaviour
    {
        public Mesh Mesh;
        void OnDestroy() { if (Mesh != null) Destroy(Mesh); }
    }

    public class RoadBuilder
    {
        public const float ChunkLen = 40f;
        public const float RoadHalf = 4f;
        readonly Transform _root;
        readonly System.Func<float, Biome> _biomeAt;
        readonly System.Func<float, float, bool> _reserved;
        readonly LinkedList<(float z, GameObject go)> _chunks = new LinkedList<(float, GameObject)>();
        float _nextZ = -80f;
        public float BuiltTo => _nextZ;
        readonly System.Random _rnd = new System.Random(7);

        static readonly Color[] Facades =
        {
            Theme.Hex("E7A3A8"), Theme.Hex("F1B98F"), Theme.Hex("F4D06F"), Theme.Hex("C9B3DD"), Theme.Hex("E9C79A"), Theme.Hex("F2DFC2"), Theme.Hex("DDA77B"), Theme.Hex("F3C6A5")
        };

        public RoadBuilder(Transform root, System.Func<float, Biome> biomeAt, System.Func<float, float, bool> reserved)
        {
            _root = root;
            _biomeAt = biomeAt;
            _reserved = reserved;
        }

        float R(float a, float b) => a + (float)_rnd.NextDouble() * (b - a);

        public void Update(float carZ)
        {
            int built = 0;
            // at start fill the view at once; while driving at most one chunk per frame (no hitches)
            while (_nextZ < carZ + 300f && (built == 0 || _chunks.Count < 4))
            {
                var go = BuildChunk(_nextZ, _biomeAt(_nextZ + ChunkLen * 0.5f));
                _chunks.AddLast((_nextZ, go));
                _nextZ += ChunkLen;
                built++;
            }
            while (_chunks.Count > 0 && _chunks.First.Value.z + ChunkLen < carZ - 70f)
            {
                Object.Destroy(_chunks.First.Value.go);
                _chunks.RemoveFirst();
            }
        }

        GameObject BuildChunk(float z0, Biome b)
        {
            var g = Build.Group("Chunk_" + (int)z0, _root, new Vector3(0, 0, z0));
            var t = g.transform;
            float L = ChunkLen;
            float mid = L / 2f;

            Color ground = b == Biome.City ? Theme.Hex("8C8A83") : Theme.Hex("6E9B45");
            Build.Box(t, new Vector3(0, -0.12f, mid), new Vector3(260, 0.2f, L + 0.05f), ground, 0.05f);
            Build.Box(t, new Vector3(0, -0.04f, mid), new Vector3(RoadHalf * 2, 0.1f, L + 0.02f), Theme.Hex("3B3E44"), 0.25f);

            // Markings
            var white = Theme.Hex("F2F2F2");
            for (float z = 2f; z < L; z += 9f)
                if (!_reserved(z0 + z, z0 + z + 4.5f))
                    Build.Box(t, new Vector3(0, 0.015f, z + 2.25f), new Vector3(0.15f, 0.02f, 4.5f), white, 0.3f);
            if (!_reserved(z0, z0 + L))
            {
                Build.Box(t, new Vector3(RoadHalf - 0.3f, 0.015f, mid), new Vector3(0.15f, 0.02f, L), white, 0.3f);
                Build.Box(t, new Vector3(-RoadHalf + 0.3f, 0.015f, mid), new Vector3(0.15f, 0.02f, L), white, 0.3f);
            }

            switch (b)
            {
                case Biome.City: City(t, z0); break;
                case Biome.Country: Country(t, z0); break;
                case Biome.Highway: Highway(t, z0); break;
            }
            Build.Combine(g.gameObject);
            return g;
        }

        void City(Transform t, float z0)
        {
            float L = ChunkLen;
            var walk = Theme.Hex("CFC8BC");
            var curb = Theme.Hex("E6E1D8");
            var trim = Theme.Hex("F5EEE1");      // white stone frames and cornices
            var glass = Theme.Hex("263346");
            var iron = Theme.Hex("1E2226");
            var door = Theme.Hex("4A3426");
            var roof = Theme.Hex("B35A3C");
            for (int side = -1; side <= 1; side += 2)
            {
                // wide pavement in pieces so crossroads can cut through
                for (float z = 0; z < L; z += 10f)
                {
                    if (_reserved(z0 + z, z0 + z + 10f)) continue;
                    Build.Box(t, new Vector3(side * (RoadHalf + 1.8f), 0.08f, z + 5f), new Vector3(3.6f, 0.18f, 10.02f), walk, 0.1f);
                    Build.Box(t, new Vector3(side * (RoadHalf + 0.06f), 0.1f, z + 5f), new Vector3(0.18f, 0.22f, 10.02f), curb, 0.1f);
                }
                // Milanese palazzi: stucco colours, stone frames, balconies with iron railings, arched doors, tiled roofs
                float z1 = 0;
                while (z1 < L)
                {
                    float w = R(9f, 13f);
                    if (z1 + w > L) w = L - z1;
                    if (w < 4f) break;
                    if (!_reserved(z0 + z1 - 2f, z0 + z1 + w + 2f))
                    {
                        int floors = _rnd.Next(4, 7);
                        float fh = 3.3f, gh = 4.2f;
                        float h = gh + floors * fh - fh + 2.6f;
                        float d = R(10f, 14f);
                        float x = side * (RoadHalf + 3.6f + d / 2f + 0.3f);
                        float zc = z1 + w / 2f;
                        var col = Facades[_rnd.Next(Facades.Length)];
                        bool shutters = _rnd.NextDouble() < 0.35;
                        Build.Box(t, new Vector3(x, h / 2f, zc), new Vector3(d, h, w - 0.3f), col, 0.05f);
                        // rusticated ground floor and stone bands between floors
                        Build.Box(t, new Vector3(x, gh / 2f, zc), new Vector3(d + 0.16f, gh, w - 0.25f), col * 0.88f + trim * 0.12f, 0.05f);
                        Build.Box(t, new Vector3(x, gh, zc), new Vector3(d + 0.3f, 0.3f, w - 0.1f), trim, 0.05f);
                        Build.Box(t, new Vector3(x, h, zc), new Vector3(d + 0.7f, 0.55f, w + 0.2f), trim, 0.05f);
                        // terracotta roof in two steps (reads as a pitched roof from the street)
                        Build.Box(t, new Vector3(x, h + 0.75f, zc), new Vector3(d - 0.6f, 1.0f, w - 0.5f), roof, 0.05f);
                        Build.Box(t, new Vector3(x, h + 1.5f, zc), new Vector3(d - 3.4f, 0.7f, w - 2.4f), roof * 0.9f, 0.05f);
                        float faceX = x - side * (d / 2f + 0.03f);
                        // windows with white frames; a balcony on the first floor (piano nobile)
                        int wn = Mathf.Max(2, Mathf.FloorToInt((w - 1.2f) / 2.6f));
                        float step = (w - 1.2f) / wn;
                        for (int f = 0; f < floors - 1; f++)
                        {
                            float wy = gh + 1.5f + f * fh;
                            for (int k = 0; k < wn; k++)
                            {
                                float wz = z1 + 0.6f + step * (k + 0.5f);
                                Build.Box(t, new Vector3(faceX, wy, wz), new Vector3(0.05f, 2.0f, 1.2f), trim, 0.1f);
                                Build.Box(t, new Vector3(faceX - side * 0.03f, wy - 0.05f, wz), new Vector3(0.05f, 1.7f, 0.9f), glass, 0.85f);
                                if (shutters)
                                {
                                    var sh = Theme.Hex("3D6B4E");
                                    Build.Box(t, new Vector3(faceX - side * 0.05f, wy - 0.05f, wz - 0.65f), new Vector3(0.05f, 1.7f, 0.42f), sh, 0.1f);
                                    Build.Box(t, new Vector3(faceX - side * 0.05f, wy - 0.05f, wz + 0.65f), new Vector3(0.05f, 1.7f, 0.42f), sh, 0.1f);
                                }
                                if (f == 0 || (f == 2 && k % 2 == 0))
                                {
                                    float by = wy - 1.05f;
                                    Build.Box(t, new Vector3(faceX - side * 0.35f, by, wz), new Vector3(0.7f, 0.12f, 1.6f), trim, 0.1f);
                                    Build.Box(t, new Vector3(faceX - side * 0.68f, by + 0.5f, wz), new Vector3(0.04f, 0.9f, 1.6f), iron, 0.4f);
                                    Build.Box(t, new Vector3(faceX - side * 0.68f, by + 0.97f, wz), new Vector3(0.08f, 0.06f, 1.65f), iron, 0.4f);
                                }
                            }
                        }
                        // ground floor: arched doors and shop windows
                        for (int k = 0; k < wn; k++)
                        {
                            float wz = z1 + 0.6f + step * (k + 0.5f);
                            bool isDoor = k == wn / 2;
                            Build.Box(t, new Vector3(faceX - side * 0.06f, 1.35f, wz), new Vector3(0.05f, 2.7f, 1.4f), isDoor ? door : glass, isDoor ? 0.2f : 0.85f);
                            var arch = Build.Prim(PrimitiveType.Cylinder, t, new Vector3(faceX - side * 0.06f, 2.7f, wz), new Vector3(1.4f, 0.025f, 1.4f), Mats.Lit(isDoor ? door : glass, isDoor ? 0.2f : 0.85f));
                            arch.transform.localRotation = Quaternion.Euler(0, 0, 90);
                            Build.Box(t, new Vector3(faceX - side * 0.02f, 3.55f, wz), new Vector3(0.05f, 0.18f, 1.7f), trim, 0.1f);
                        }
                    }
                    z1 += w;
                }
                // classic black street lamps
                for (float z = 6f; z < L; z += 20f)
                {
                    if (_reserved(z0 + z - 2, z0 + z + 2)) continue;
                    Build.ClassicLamp(t, new Vector3(side * (RoadHalf + 0.7f), 0.17f, z));
                }
                // low-poly trees on the pavement
                for (float z = 16f; z < L; z += 20f)
                {
                    if (_reserved(z0 + z - 3, z0 + z + 3)) continue;
                    Build.LowPolyTree(t, new Vector3(side * (RoadHalf + 2.5f), 0.17f, z), R(3.8f, 5.2f), _rnd);
                    if (_rnd.NextDouble() < 0.5) Build.LowPolyBush(t, new Vector3(side * (RoadHalf + 3.2f), 0.17f, z + 3f), R(0.7f, 1.0f), _rnd);
                }
            }
            // a low-poly cloud now and then
            if (_rnd.NextDouble() < 0.6) Build.Cloud(t, new Vector3(R(-70f, 70f), R(38f, 60f), R(0, L)), R(8f, 14f), _rnd);
        }

        void Country(Transform t, float z0)
        {
            float L = ChunkLen;
            var cypress = Theme.Hex("2F4A2A");
            var crown = Theme.Hex("3F6B33");
            var trunk = Theme.Hex("5A4330");
            for (int side = -1; side <= 1; side += 2)
            {
                // gravel shoulder
                Build.Box(t, new Vector3(side * (RoadHalf + 0.7f), -0.02f, L / 2f), new Vector3(1.4f, 0.06f, L), Theme.Hex("A79C84"), 0.05f);
                for (int i = 0; i < 7; i++)
                {
                    float z = R(0, L);
                    if (_reserved(z0 + z - 3, z0 + z + 3)) continue;
                    float x = side * R(RoadHalf + 4f, 40f);
                    double p = _rnd.NextDouble();
                    GameObject tree;
                    if (p < 0.45) tree = Models.Spawn("Nature", "tree_cone_dark", t, new Vector3(x, 0, z), R(0, 360), R(7f, 11f), true); // Tuscan cypress
                    else if (p < 0.7) tree = Models.Spawn("Nature", _rnd.NextDouble() < 0.5 ? "tree_oak" : "tree_default", t, new Vector3(x, 0, z), R(0, 360), R(5f, 8f), true);
                    else if (p < 0.85) tree = Models.Spawn("Nature", _rnd.NextDouble() < 0.5 ? "plant_bushLarge" : "plant_bush", t, new Vector3(x, 0, z), R(0, 360), R(1.2f, 2.2f), true);
                    else tree = Models.Spawn("Nature", "rock_largeA", t, new Vector3(x, 0, z), R(0, 360), R(1.5f, 3f), true);
                    if (tree != null) continue;
                    if (p < 0.6)
                    {
                        float h = R(5f, 9f);
                        Build.Prim(PrimitiveType.Capsule, t, new Vector3(x, h / 2f, z), new Vector3(1.3f, h / 2f, 1.3f), Mats.Lit(cypress, 0.05f));
                    }
                    else
                    {
                        Build.Prim(PrimitiveType.Cylinder, t, new Vector3(x, 1.2f, z), new Vector3(0.35f, 1.2f, 0.35f), Mats.Lit(trunk, 0.05f));
                        float s2 = R(3f, 5f);
                        Build.Prim(PrimitiveType.Sphere, t, new Vector3(x, 2.4f + s2 * 0.35f, z), new Vector3(s2, s2 * 0.75f, s2), Mats.Lit(crown, 0.05f));
                    }
                }
                // wild flowers along the verge
                for (int i = 0; i < 4; i++)
                {
                    float z = R(0, L);
                    if (_reserved(z0 + z - 2, z0 + z + 2)) continue;
                    Models.Spawn("Nature", _rnd.NextDouble() < 0.5 ? "flower_redA" : "flower_yellowA", t, new Vector3(side * R(RoadHalf + 1.6f, RoadHalf + 3.5f), 0, z), R(0, 360), 0.6f, true);
                }
                // occasional farmhouse
                if (_rnd.NextDouble() < 0.25)
                {
                    float z = R(8, L - 8);
                    if (!_reserved(z0 + z - 10, z0 + z + 10))
                    {
                        float x = side * R(22f, 45f);
                        var houses = new[] { "building-type-a", "building-type-c", "building-type-f", "building-type-h", "building-type-k", "building-type-m" };
                        if (Models.Spawn("Suburb", houses[_rnd.Next(houses.Length)], t, new Vector3(x, 0, z), side > 0 ? -90f : 90f, R(10f, 13f)) == null)
                        {
                            Build.Box(t, new Vector3(x, 3f, z), new Vector3(9f, 6f, 12f), Theme.Hex("E6CFA3"), 0.05f);
                            var roof = Build.Box(t, new Vector3(x, 6.3f, z), new Vector3(6.6f, 6.6f, 12.6f), Theme.Hex("B5543A"), 0.05f);
                            roof.transform.localRotation = Quaternion.Euler(0, 0, 45);
                            roof.transform.localPosition = new Vector3(x, 5.6f, z);
                        }
                    }
                }
                // rolling hills on the horizon
                if (_rnd.NextDouble() < 0.5)
                {
                    float s = R(40, 90);
                    Build.Prim(PrimitiveType.Sphere, t, new Vector3(side * R(115, 160), -s * 0.35f, R(0, L)), new Vector3(s * 1.6f, s * 0.7f, s * 1.3f),
                        Mats.Lit(Theme.Hex("5F8C3E"), 0.02f));
                }
            }
        }

        void Highway(Transform t, float z0)
        {
            float L = ChunkLen;
            var rail = Theme.Hex("AEB4BC");
            for (int side = -1; side <= 1; side += 2)
            {
                Build.Box(t, new Vector3(side * (RoadHalf + 1.4f), -0.03f, L / 2f), new Vector3(2.8f, 0.08f, L), Theme.Hex("44484F"), 0.2f);
                Build.Box(t, new Vector3(side * (RoadHalf + 3.0f), 0.7f, L / 2f), new Vector3(0.08f, 0.32f, L), rail, 0.7f);
                for (float z = 2; z < L; z += 4)
                    Build.Box(t, new Vector3(side * (RoadHalf + 3.05f), 0.35f, z), new Vector3(0.12f, 0.7f, 0.12f), Theme.Hex("6C727A"), 0.5f);
                for (int i = 0; i < 3; i++)
                {
                    float z = R(0, L);
                    float x = side * R(RoadHalf + 12f, 50f);
                    float h = R(5f, 9f);
                    if (Models.Spawn("Nature", i == 0 ? "tree_pineRoundA" : "tree_cone_dark", t, new Vector3(x, 0, z), R(0, 360), h, true) == null)
                        Build.Prim(PrimitiveType.Capsule, t, new Vector3(x, h / 2f, z), new Vector3(1.3f, h / 2f, 1.3f), Mats.Lit(Theme.Hex("2F4A2A"), 0.05f));
                }
            }
        }
    }
}
