using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace PatenteArabo
{
    public class TrafficLight
    {
        readonly Renderer _r, _y, _g;
        public int State { get; private set; } = 2; // 0 red, 1 yellow, 2 green

        public TrafficLight(Transform parent, Vector3 pos)
        {
            var g = Build.Group("TrafficLight", parent, pos);
            var dark = Theme.Hex("23262B");
            Build.Prim(PrimitiveType.Cylinder, g.transform, new Vector3(0, 1.7f, 0), new Vector3(0.14f, 1.7f, 0.14f), Mats.Lit(dark, 0.4f));
            Build.Box(g.transform, new Vector3(0, 3.95f, 0), new Vector3(0.5f, 1.45f, 0.4f), dark, 0.3f);
            Build.Box(g.transform, new Vector3(0, 3.95f, 0.05f), new Vector3(0.75f, 1.7f, 0.06f), Theme.Hex("111315"), 0.2f);
            _r = Build.Prim(PrimitiveType.Sphere, g.transform, new Vector3(0, 4.42f, -0.2f), Vector3.one * 0.36f, Mats.Unlit(Color.black)).GetComponent<Renderer>();
            _y = Build.Prim(PrimitiveType.Sphere, g.transform, new Vector3(0, 3.95f, -0.2f), Vector3.one * 0.36f, Mats.Unlit(Color.black)).GetComponent<Renderer>();
            _g = Build.Prim(PrimitiveType.Sphere, g.transform, new Vector3(0, 3.48f, -0.2f), Vector3.one * 0.36f, Mats.Unlit(Color.black)).GetComponent<Renderer>();
            Set(2);
        }

        public void Set(int s)
        {
            State = s;
            var off = Theme.Hex("2A2A2A");
            _r.sharedMaterial = Mats.Unlit(s == 0 ? new Color(1f, 0.12f, 0.1f) : off);
            _y.sharedMaterial = Mats.Unlit(s == 1 ? new Color(1f, 0.75f, 0.05f) : off);
            _g.sharedMaterial = Mats.Unlit(s == 2 ? new Color(0.15f, 1f, 0.35f) : off);
        }
    }

    public class Mover
    {
        public Transform T;
        public Vector3 Vel;
        public float Life = 30f;
    }

    public class Scenario
    {
        public string Type;
        public Biome Biome;
        public float Z0;           // stop line (or trigger point)
        public bool Freeze;        // ask while driving (no stop)
        public float TriggerZ;
        public float ReserveFrom, ReserveTo;
        public GameObject Root;
        public string LabelAr;
        public float Tail = 60f;   // how far past Z0 this scenario's landscape continues

        public enum Phase { Cruise, Approach, Stopped, Asking, Resolving, Done }
        public Phase P = Phase.Cruise;
        public float Timer;
        public bool Released;
        public bool Flag1, Flag2;

        public TrafficLight Light;
        public Transform Pedestrian;
        public Transform BarrierA, BarrierB;
        public Renderer FlashA, FlashB;
        public float BarrierAngle; // 0 = up, 1 = down
        public Transform Npc;
        public float NpcAngle;
    }

    /// <summary>The driving mode: endless Italian roads, scenarios at signs, and questions on screen.</summary>
    public class DriveMode : MonoBehaviour
    {
        const float CarHalf = 2.1f;
        const float LaneX = 2f;

        Transform _world;
        Transform _car;
        Camera _cam;
        RoadBuilder _road;
        readonly List<Scenario> _scen = new List<Scenario>();
        readonly List<Mover> _movers = new List<Mover>();
        readonly List<string> _deck = new List<string>();
        float _carZ, _speed, _carX = LaneX, _yaw;
        bool _started;
        int _asked, _correct;
        Vector3 _camVel;

        // UI (UI Toolkit)
        VisualElement _ui, _qPanel, _intro;
        Label _speedText, _scoreText, _labelText;
        readonly Dictionary<string, List<Stmt>> _pool = new Dictionary<string, List<Stmt>>();
        readonly HashSet<int> _usedStmts = new HashSet<int>();

        // saved render settings
        bool _fog; Color _fogColor; FogMode _fogMode; float _fogStart, _fogEnd;

        static readonly Dictionary<string, (Biome biome, string key)> Types = new Dictionary<string, (Biome, string)>
        {
            {"red_light", (Biome.City, "sc_light")},
            {"yellow_light", (Biome.City, "sc_light")},
            {"stop_sign", (Biome.City, "sc_stop")},
            {"pedestrian", (Biome.City, "sc_ped")},
            {"speed_urban", (Biome.City, "sc_speed_city")},
            {"roundabout", (Biome.Country, "sc_round")},
            {"give_way", (Biome.Country, "sc_giveway")},
            {"speed_extra", (Biome.Country, "sc_speed_road")},
            {"railway", (Biome.Country, "sc_rail")},
            {"highway", (Biome.Highway, "sc_highway")},
        };

        /// <summary>Official statements that match each road situation.</summary>
        List<Stmt> PoolFor(string type)
        {
            if (_pool.TryGetValue(type, out var l)) return l;
            switch (type)
            {
                case "speed_urban": l = Listato.Find(new[] { "centri abitati", "centro abitato" }, new[] { 11 }); break;
                case "red_light": l = Listato.Find(new[] { "luce rossa", "rossa" }, new[] { 7 }); break;
                case "yellow_light": l = Listato.Find(new[] { "luce gialla", "gialla" }, new[] { 7 }); break;
                case "pedestrian": l = Listato.Find(new[] { "pedoni", "attraversamento pedonale" }, null); break;
                case "stop_sign": l = Listato.Find(new[] { "fermarsi e dare precedenza", " stop" }, new[] { 5, 6 }); break;
                case "give_way": l = Listato.Find(new[] { "dare precedenza" }, new[] { 5 }); break;
                case "railway": l = Listato.Find(new[] { "passaggio a livello", "passaggi a livello" }, null); break;
                case "roundabout": l = Listato.Find(new[] { "rotatori" }, null); break;
                case "speed_extra": l = Listato.Find(new[] { "extraurban" }, new[] { 11 }); break;
                case "highway": l = Listato.Find(new[] { "autostrad" }, new[] { 11, 17 }); break;
                default: l = new List<Stmt>(); break;
            }
            if (l.Count == 0) l = Listato.Chapter(11);
            _pool[type] = l;
            return l;
        }

        // Order that keeps biomes grouped (city → country → highway → back).
        static readonly string[] Route =
        {
            "speed_urban", "red_light", "pedestrian", "stop_sign", "yellow_light",
            "give_way", "railway", "roundabout", "speed_extra", "highway"
        };

        public void Begin(VisualElement uiRoot)
        {
            _ui = uiRoot;
            _fog = RenderSettings.fog; _fogColor = RenderSettings.fogColor; _fogMode = RenderSettings.fogMode;
            _fogStart = RenderSettings.fogStartDistance; _fogEnd = RenderSettings.fogEndDistance;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.76f, 0.83f, 0.9f);
            RenderSettings.fogStartDistance = 150f;
            RenderSettings.fogEndDistance = 340f;

            if (Object.FindAnyObjectByType<Light>() == null)
            {
                var l = new GameObject("Sun").AddComponent<Light>();
                l.type = LightType.Directional;
                l.intensity = 1.2f;
                l.shadows = LightShadows.Soft;
                l.transform.rotation = Quaternion.Euler(50, -35, 0);
                l.transform.SetParent(transform);
            }

            _cam = Camera.main;
            if (_cam == null)
            {
                _cam = new GameObject("DriveCamera").AddComponent<Camera>();
                _cam.transform.SetParent(transform);
                _cam.tag = "MainCamera";
            }
            _cam.farClipPlane = 420f;
            _cam.fieldOfView = 62f;
            _cam.clearFlags = CameraClearFlags.Skybox;

            _world = new GameObject("World").transform;
            _world.SetParent(transform);

            _car = Build.Car(_world, new Vector3(LaneX, 0, 0), Theme.Hex("C0182B"), "PlayerCar", "hatchback-sports").transform;

            _deck.AddRange(Route);
            for (int i = 0; i < 2; i++) ScheduleNext();
            _road = new RoadBuilder(_world, BiomeAt, Reserved);
            _road.Update(_carZ);
            SnapCamera();

            BuildUI();
        }

        public void Shutdown()
        {
            Time.timeScale = 1f;
            RenderSettings.fog = _fog; RenderSettings.fogColor = _fogColor; RenderSettings.fogMode = _fogMode;
            RenderSettings.fogStartDistance = _fogStart; RenderSettings.fogEndDistance = _fogEnd;
            if (_correct > Srs.DriveBest) Srs.DriveBest = _correct;
            Destroy(gameObject);
        }

        // ---------------- scheduling ----------------
        Biome BiomeAt(float z)
        {
            foreach (var s in _scen)
                if (s.Z0 + s.Tail >= z) return s.Biome;
            return _scen.Count > 0 ? _scen[_scen.Count - 1].Biome : Biome.City;
        }

        bool Reserved(float a, float b)
        {
            foreach (var s in _scen)
                if (s.ReserveTo > s.ReserveFrom && a < s.ReserveTo && b > s.ReserveFrom) return true;
            return false;
        }

        int _routeIdx;
        string[] _route;

        /// <summary>
        /// Every drive is different, but useful: the landscape groups stay together (city, country, highway),
        /// inside each group the situations where the learner has the most mistakes come first, the rest shuffled.
        /// The statements themselves are picked mistakes first, then never seen, then the others (spaced repetition).
        /// </summary>
        string[] SessionRoute()
        {
            var groups = new List<List<string>>();
            foreach (var t in Route)
            {
                if (groups.Count == 0 || Types[groups[groups.Count - 1][0]].biome != Types[t].biome) groups.Add(new List<string>());
                groups[groups.Count - 1].Add(t);
            }
            var outList = new List<string>();
            foreach (var g in groups)
                outList.AddRange(g.Select(t => (t, w: PoolFor(t).Count(x => Srs.Weak(x.id)), r: Random.value))
                                  .OrderByDescending(x => x.w > 0).ThenBy(x => x.r).Select(x => x.t));
            return outList.ToArray();
        }

        void ScheduleNext()
        {
            if (_route == null) _route = SessionRoute();
            string type = _route[_routeIdx % _route.Length];
            _routeIdx++;
            var info = Types[type];
            float last = _scen.Count > 0 ? _scen[_scen.Count - 1].Z0 : 40f;
            float gap = info.biome == Biome.Highway ? 220f : 110f;   // ~10 s between questions
            if (_scen.Count > 0 && _scen[_scen.Count - 1].Biome != info.biome) gap += 50f; // room for the landscape to change
            if (_scen.Count > 0) gap = Mathf.Max(gap, _scen[_scen.Count - 1].Tail + 80f);
            var s = new Scenario
            {
                Type = type, Biome = info.biome, LabelAr = Lang.T(info.key),
                Z0 = Mathf.Max(last + gap, _carZ + 370f)
            };
            if (_scen.Count == 0) s.Z0 = 150f;
            Setup(s);
            _scen.Add(s);
        }

        // ---------------- scenario construction ----------------
        void Setup(Scenario s)
        {
            s.Root = Build.Group("Scenario_" + s.Type, _world, new Vector3(0, 0, s.Z0)).gameObject;
            var t = s.Root.transform;
            var white = Theme.Hex("F2F2F2");
            var asphalt = Theme.Hex("3B3E44");

            void Crossroad()
            {
                Build.Box(t, new Vector3(0, -0.03f, 6f), new Vector3(240f, 0.1f, 10f), asphalt, 0.25f);
                // centre line of the crossing road
                for (float x = -110; x < 110; x += 9)
                    if (Mathf.Abs(x) > 6) Build.Box(t, new Vector3(x, 0.02f, 6f), new Vector3(4.5f, 0.02f, 0.15f), white, 0.3f);
                s.ReserveFrom = s.Z0 - 6f;
                s.ReserveTo = s.Z0 + 16f;
            }

            void StopLine(bool dashed)
            {
                if (!dashed) Build.Box(t, new Vector3(LaneX, 0.02f, -0.25f), new Vector3(3.7f, 0.02f, 0.5f), white, 0.3f);
                else for (float x = 0.5f; x < 4f; x += 0.9f) Build.Box(t, new Vector3(x, 0.02f, -0.2f), new Vector3(0.5f, 0.02f, 0.4f), white, 0.3f);
            }

            switch (s.Type)
            {
                case "red_light":
                case "yellow_light":
                    Crossroad();
                    StopLine(false);
                    s.Light = new TrafficLight(t, new Vector3(4.7f, 0, 0.6f));
                    new TrafficLight(t, new Vector3(-4.7f, 0, 11.4f)).Set(0);
                    break;
                case "stop_sign":
                    Crossroad();
                    StopLine(false);
                    Build.SignPost(t, new Vector3(4.8f, 0, -0.6f), "stop", 1.2f);
                    break;
                case "give_way":
                    Crossroad();
                    StopLine(true);
                    Build.SignPost(t, new Vector3(4.8f, 0, -0.6f), "precedenza", 1.3f);
                    break;
                case "pedestrian":
                {
                    for (float x = -3.4f; x <= 3.5f; x += 1f)
                        Build.Box(t, new Vector3(x, 0.025f, 3.5f), new Vector3(0.55f, 0.03f, 4f), white, 0.3f);
                    Build.SignPost(t, new Vector3(4.9f, 0, 1.2f), "attraversamento_pedonale", 1.1f);
                    Build.SignPost(t, new Vector3(4.9f, 0, -45f), "pericolo_pedoni", 1.1f);
                    s.Pedestrian = Build.Person(t, new Vector3(5.4f, 0.17f, 3.5f), Theme.Hex("2F80ED")).transform;
                    s.Pedestrian.localRotation = Quaternion.Euler(0, -90, 0);
                    break;
                }
                case "speed_urban":
                    Build.SignPost(t, new Vector3(4.9f, 0, 0f), "limite_50", 1.15f);
                    s.Freeze = true;
                    s.TriggerZ = s.Z0 - 32f;
                    break;
                case "speed_extra":
                    s.Freeze = true;
                    s.TriggerZ = s.Z0;
                    s.Tail = 160f;
                    break;
                case "highway":
                {
                    s.Freeze = true;
                    s.TriggerZ = s.Z0;
                    s.Tail = 260f;
                    // green gantry board
                    var g = Build.Group("Gantry", t, new Vector3(0, 0, 45f));
                    var grey = Theme.Hex("8D949C");
                    Build.Box(g.transform, new Vector3(-6.2f, 3.4f, 0), new Vector3(0.3f, 6.8f, 0.3f), grey, 0.4f);
                    Build.Box(g.transform, new Vector3(6.2f, 3.4f, 0), new Vector3(0.3f, 6.8f, 0.3f), grey, 0.4f);
                    Build.Box(g.transform, new Vector3(0, 6.7f, 0), new Vector3(12.8f, 0.3f, 0.3f), grey, 0.4f);
                    Build.Box(g.transform, new Vector3(0, 5.6f, -0.2f), new Vector3(9f, 2.2f, 0.12f), Theme.Hex("0B7A3B"), 0.2f);
                    Build.Box(g.transform, new Vector3(0, 5.6f, -0.17f), new Vector3(9.3f, 2.5f, 0.1f), Color.white, 0.2f);
                    var label = new GameObject("Label");
                    label.transform.SetParent(g.transform, false);
                    label.transform.localPosition = new Vector3(0, 5.6f, -0.3f);
                    var tm = label.AddComponent<TextMesh>();
                    tm.text = "A1  MILANO  130";
                    tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    tm.fontSize = 100;
                    tm.characterSize = 0.19f;
                    tm.anchor = TextAnchor.MiddleCenter;
                    tm.alignment = TextAlignment.Center;
                    tm.color = Color.white;
                    tm.fontStyle = FontStyle.Bold;
                    label.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
                    break;
                }
                case "railway":
                {
                    s.ReserveFrom = s.Z0 - 6f;
                    s.ReserveTo = s.Z0 + 14f;
                    var bed = Theme.Hex("6F665A");
                    Build.Box(t, new Vector3(0, -0.02f, 5f), new Vector3(240f, 0.08f, 3.6f), bed, 0.05f);
                    for (float x = -40; x <= 40; x += 1.1f)
                        Build.Box(t, new Vector3(x, 0.03f, 5f), new Vector3(0.25f, 0.06f, 2.6f), Theme.Hex("5A4636"), 0.05f);
                    Build.Box(t, new Vector3(0, 0.1f, 4.3f), new Vector3(240f, 0.12f, 0.1f), Theme.Hex("9AA0A6"), 0.8f);
                    Build.Box(t, new Vector3(0, 0.1f, 5.7f), new Vector3(240f, 0.12f, 0.1f), Theme.Hex("9AA0A6"), 0.8f);
                    // asphalt over the tracks between the rails
                    Build.Box(t, new Vector3(0, 0.05f, 5f), new Vector3(8f, 0.1f, 1.2f), Theme.Hex("3B3E44"), 0.25f);
                    Build.SignPost(t, new Vector3(5.2f, 0, 0.2f), "croce_s_andrea", 1.3f, 2.2f);
                    Build.SignPost(t, new Vector3(4.9f, 0, -60f), "pericolo_generico", 1.1f);
                    // flashing lights
                    var lamps = Build.Group("Lamps", t, new Vector3(5.2f, 1.6f, 0.1f));
                    Build.Box(lamps.transform, new Vector3(0, 0, 0.05f), new Vector3(1.1f, 0.45f, 0.08f), Theme.Hex("111315"), 0.2f);
                    s.FlashA = Build.Prim(PrimitiveType.Sphere, lamps.transform, new Vector3(-0.3f, 0, -0.05f), Vector3.one * 0.3f, Mats.Unlit(Theme.Hex("2A2A2A"))).GetComponent<Renderer>();
                    s.FlashB = Build.Prim(PrimitiveType.Sphere, lamps.transform, new Vector3(0.3f, 0, -0.05f), Vector3.one * 0.3f, Mats.Unlit(Theme.Hex("2A2A2A"))).GetComponent<Renderer>();
                    s.BarrierA = Barrier(t, new Vector3(4.6f, 1.0f, 1.4f), 1);
                    s.BarrierB = Barrier(t, new Vector3(-4.6f, 1.0f, 8.6f), -1);
                    SetBarriers(s, 0f);
                    break;
                }
                case "roundabout":
                {
                    s.ReserveFrom = s.Z0 - 4f;
                    s.ReserveTo = s.Z0 + 34f;
                    var c = new Vector3(0, 0, 14f);
                    Build.Box(t, new Vector3(0, -0.03f, 14f), new Vector3(240f, 0.1f, 9f), asphalt, 0.25f);
                    Build.Prim(PrimitiveType.Cylinder, t, c + new Vector3(0, 0.015f, 0), new Vector3(30f, 0.015f, 30f), Mats.Lit(asphalt, 0.25f), "Ring");
                    Build.Prim(PrimitiveType.Cylinder, t, c + new Vector3(0, 0.1f, 0), new Vector3(15f, 0.1f, 15f), Mats.Lit(Theme.Hex("D8D4CC"), 0.1f), "Curb");
                    Build.Prim(PrimitiveType.Cylinder, t, c + new Vector3(0, 0.25f, 0), new Vector3(14f, 0.25f, 14f), Mats.Lit(Theme.Hex("5E9A3C"), 0.05f), "Island");
                    Build.Prim(PrimitiveType.Capsule, t, c + new Vector3(0, 3.8f, 0), new Vector3(1.5f, 3.4f, 1.5f), Mats.Lit(Theme.Hex("2F4A2A"), 0.05f));
                    for (int i = 0; i < 10; i++)
                    {
                        float a = i * 36f * Mathf.Deg2Rad;
                        Build.Prim(PrimitiveType.Sphere, t, c + new Vector3(Mathf.Cos(a) * 4.5f, 0.6f, Mathf.Sin(a) * 4.5f), Vector3.one * 1.1f,
                            Mats.Lit(i % 2 == 0 ? Theme.Hex("D94F70") : Theme.Hex("F2C94C"), 0.05f));
                    }
                    StopLine(true);
                    Build.SignPost(t, new Vector3(4.8f, 0, -0.6f), "precedenza", 1.3f);
                    Build.SignPost(t, new Vector3(4.9f, 0, -40f), "rotatoria", 1.1f);
                    Build.SignPost(t, c + new Vector3(0, 0.5f, -7.3f), "rotatoria", 1.0f, 1.6f);
                    s.Npc = Build.Car(t, Vector3.zero, Theme.Hex("2D6CDF"), "RoundaboutCar").transform;
                    s.NpcAngle = 140f;
                    break;
                }
            }
        }

        Transform Barrier(Transform t, Vector3 pivotPos, int dir)
        {
            var post = Build.Box(t, new Vector3(pivotPos.x + dir * 0.25f, 0.55f, pivotPos.z), new Vector3(0.35f, 1.1f, 0.35f), Theme.Hex("E8E8E8"), 0.3f);
            var pivot = Build.Group("BarrierPivot", t, pivotPos).transform;
            for (int i = 0; i < 6; i++)
            {
                var col = i % 2 == 0 ? Theme.Hex("D7263D") : Color.white;
                Build.Box(pivot, new Vector3(-dir * (0.36f + i * 0.72f), 0, 0), new Vector3(0.72f, 0.14f, 0.14f), col, 0.3f);
            }
            return pivot;
        }

        static void SetBarriers(Scenario s, float down)
        {
            s.BarrierAngle = down;
            if (s.BarrierA != null) s.BarrierA.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-85f, 0f, down));
            if (s.BarrierB != null) s.BarrierB.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(85f, 0f, down));
        }

        Scenario Current => _scen.FirstOrDefault(s => s.P != Scenario.Phase.Done);

        void SpawnCross(Scenario s, bool fromLeft, Color c)
        {
            float z = s.Z0 + (fromLeft ? 3.6f : 8.4f);
            float x = fromLeft ? -90f : 90f;
            var car = Build.Car(_world, new Vector3(x, 0, z), c, "CrossCar").transform;
            car.rotation = Quaternion.Euler(0, fromLeft ? 90 : -90, 0);
            _movers.Add(new Mover { T = car, Vel = new Vector3(fromLeft ? 15f : -15f, 0, 0), Life = 14f });
        }

        // ---------------- per-frame ----------------
        void Update()
        {
            if (_car == null) return;
            float dt = Time.deltaTime;
            var s = Current;
            float target = CruiseSpeed(BiomeAt(_carZ));
            bool hold = false;

            if (s != null)
            {
                PreTick(s, dt);
                if (s.Freeze)
                {
                    if (s.P == Scenario.Phase.Cruise && _carZ >= s.TriggerZ && _started)
                    {
                        s.P = Scenario.Phase.Asking;
                        Ask(s);
                    }
                    if (s.Type == "speed_urban" && _carZ > s.Z0 - 120f) target = Mathf.Min(target, 50f / 3.6f);
                }
                else
                {
                    float front = _carZ + CarHalf;
                    float dist = s.Z0 - 0.4f - front;
                    switch (s.P)
                    {
                        case Scenario.Phase.Cruise:
                            if (_started && dist < _speed * _speed / (2f * 2.4f) + 6f) s.P = Scenario.Phase.Approach;
                            break;
                        case Scenario.Phase.Approach:
                        {
                            float allowed = Mathf.Sqrt(2f * 2.4f * Mathf.Max(dist, 0f));
                            target = Mathf.Min(target, allowed);
                            if (_speed > allowed) _speed = Mathf.Max(allowed, _speed - 7f * dt);
                            if (dist < 0.35f && _speed < 0.6f) { _speed = 0; s.P = Scenario.Phase.Stopped; s.Timer = 0; }
                            break;
                        }
                        case Scenario.Phase.Stopped:
                            hold = true;
                            s.Timer += dt;
                            if (s.Timer > 0.9f) { s.P = Scenario.Phase.Asking; Ask(s); }
                            break;
                        case Scenario.Phase.Asking:
                            hold = true;
                            break;
                        case Scenario.Phase.Resolving:
                            hold = !s.Released;
                            if (s.Released) s.P = Scenario.Phase.Done;
                            break;
                    }
                }
            }

            if (!_started) hold = true;
            if (hold) { _speed = Mathf.MoveTowards(_speed, 0, 8f * dt); }
            else _speed = Mathf.MoveTowards(_speed, target, (_speed < target ? 3.2f : 5f) * dt);

            _carZ += _speed * dt;
            UpdateCarPose(dt);
            UpdateMovers(dt);
            _road.Update(_carZ);

            // retire old scenarios and keep two ahead
            for (int i = _scen.Count - 1; i >= 0; i--)
                if (_scen[i].P == Scenario.Phase.Done && _scen[i].Z0 + 90f < _carZ)
                {
                    Destroy(_scen[i].Root);
                    _scen.RemoveAt(i);
                }
            if (_scen.Count(x => x.P != Scenario.Phase.Done) < 2) ScheduleNext();

            if (_speedText != null) _speedText.text = Mathf.RoundToInt(_speed * 3.6f).ToString();
        }

        float CruiseSpeed(Biome b) => b == Biome.City ? 50f / 3.6f : b == Biome.Country ? 88f / 3.6f : 125f / 3.6f;

        void PreTick(Scenario s, float dt)
        {
            float d = s.Z0 - _carZ;
            switch (s.Type)
            {
                case "red_light":
                    if (!s.Flag1 && d < 95f) { s.Flag1 = true; s.Light.Set(1); s.Timer = 0; }
                    if (s.Flag1 && s.Light.State == 1 && s.P < Scenario.Phase.Resolving)
                    {
                        s.Timer += dt;
                        if (s.Timer > 2.2f) { s.Light.Set(0); s.Timer = 0; }
                    }
                    if (s.Light.State == 0 && s.P >= Scenario.Phase.Approach && s.P < Scenario.Phase.Resolving)
                    {
                        s.NpcAngle += dt;
                        if (s.NpcAngle > 3.2f) { s.NpcAngle = 0; SpawnCross(s, Random.value > 0.5f, RandomCarColor()); }
                    }
                    break;
                case "yellow_light":
                    if (!s.Flag1 && d < 70f) { s.Flag1 = true; s.Light.Set(1); }
                    break;
                case "stop_sign":
                case "give_way":
                    if (!s.Flag1 && d < 38f) { s.Flag1 = true; SpawnCross(s, false, RandomCarColor()); }
                    if (!s.Flag2 && d < 12f) { s.Flag2 = true; SpawnCross(s, true, RandomCarColor()); }
                    break;
                case "pedestrian":
                    if (!s.Flag1 && d < 30f) s.Flag1 = true;
                    if (s.Flag1 && s.Pedestrian != null && s.Pedestrian.localPosition.x > -5.4f)
                    {
                        var p = s.Pedestrian.localPosition;
                        p.x -= 1.0f * dt;
                        p.y = 0.17f + Mathf.Abs(Mathf.Sin(Time.time * 6f)) * 0.06f;
                        s.Pedestrian.localPosition = p;
                    }
                    break;
                case "railway":
                    if (!s.Flag1 && d < 100f) s.Flag1 = true;
                    if (s.Flag1 && !s.Flag2)
                    {
                        bool on = Mathf.Repeat(Time.time, 1f) < 0.5f;
                        s.FlashA.sharedMaterial = Mats.Unlit(on ? new Color(1, 0.1f, 0.1f) : Theme.Hex("2A2A2A"));
                        s.FlashB.sharedMaterial = Mats.Unlit(!on ? new Color(1, 0.1f, 0.1f) : Theme.Hex("2A2A2A"));
                        if (s.BarrierAngle < 1f) SetBarriers(s, Mathf.MoveTowards(s.BarrierAngle, 1f, dt / 3.5f));
                    }
                    break;
                case "roundabout":
                    if (s.Npc != null)
                    {
                        s.NpcAngle += dt * (8.5f / 11f) * Mathf.Rad2Deg;
                        float a = s.NpcAngle * Mathf.Deg2Rad;
                        s.Npc.localPosition = new Vector3(Mathf.Cos(a) * 11f, 0, 14f + Mathf.Sin(a) * 11f);
                        s.Npc.localRotation = Quaternion.Euler(0, -s.NpcAngle, 0);
                    }
                    break;
            }
        }

        static Color RandomCarColor()
        {
            var c = new[] { "F2F2F2", "1F2937", "2D6CDF", "8A8F98", "F2C94C", "27AE60" };
            return Theme.Hex(c[Random.Range(0, c.Length)]);
        }

        void UpdateMovers(float dt)
        {
            for (int i = _movers.Count - 1; i >= 0; i--)
            {
                var m = _movers[i];
                m.Life -= dt;
                if (m.T == null || m.Life <= 0) { if (m.T != null) Destroy(m.T.gameObject); _movers.RemoveAt(i); continue; }
                m.T.position += m.Vel * dt;
            }
        }

        void UpdateCarPose(float dt)
        {
            float x = LaneX, slope = 0f;
            foreach (var s in _scen)
            {
                if (s.Type != "roundabout") continue;
                float u = (_carZ - s.Z0) / 28f;
                if (u > 0f && u < 1f)
                {
                    x = LaneX + 9f * Mathf.Sin(Mathf.PI * u);
                    slope = 9f * Mathf.PI / 28f * Mathf.Cos(Mathf.PI * u);
                }
            }
            _carX = x;
            _yaw = Mathf.Atan(slope) * Mathf.Rad2Deg;
            _car.position = new Vector3(_carX, 0, _carZ);
            _car.rotation = Quaternion.Euler(0, _yaw, 0);

            // camera follow
            var fwd = _car.forward;
            var desired = _car.position - fwd * 8.2f + Vector3.up * 3.4f;
            desired.x = Mathf.Lerp(desired.x, LaneX * 0.6f, 0.35f);
            _cam.transform.position = Vector3.SmoothDamp(_cam.transform.position, desired, ref _camVel, 0.18f, Mathf.Infinity, Mathf.Max(dt, 0.0001f));
            var look = _car.position + fwd * 9f + Vector3.up * 1.2f;
            _cam.transform.rotation = Quaternion.Slerp(_cam.transform.rotation, Quaternion.LookRotation(look - _cam.transform.position), 1f - Mathf.Exp(-8f * Mathf.Max(dt, 0.0001f)));
        }

        void SnapCamera()
        {
            _cam.transform.position = new Vector3(LaneX * 0.7f, 3.4f, _carZ - 8.2f);
            _cam.transform.rotation = Quaternion.LookRotation(new Vector3(LaneX, 1.2f, _carZ + 9f) - _cam.transform.position);
        }

        // ---------------- questions ----------------
        void Ask(Scenario s)
        {
            if (!Premium.CanDriveQuestion)
            {
                Time.timeScale = 0f;
                _qPanel = U.Col(_ui, "drive-panel");
                _qPanel.style.alignItems = Align.Center;
                U.Icon(_qPanel, "lock", 90, U.Hex("F2C94C"));
                U.T(_qPanel, Lang.T("drive_limit", Premium.FreeDriveQuestionsPerDay), "h2", "wraptext").style.unityTextAlign = TextAnchor.MiddleCenter;
                var row = U.Row(_qPanel);
                U.IB(row, "premium", Lang.T("go_premium"), () => { Time.timeScale = 1f; App.I.Go(() => new PaywallPage(Lang.T("drive_limit", Premium.FreeDriveQuestionsPerDay), Premium.AddDrive)); }, "btn-gold");
                U.B(row, Lang.T("exit"), () => { Time.timeScale = 1f; App.I.GoHome(); }, "btn-ghost");
                return;
            }
            Time.timeScale = s.Freeze ? 0f : 1f;
            var pool = PoolFor(s.Type);
            var fresh = pool.Where(x => !_usedStmts.Contains(x.id)).ToList();
            if (fresh.Count == 0) { _usedStmts.Clear(); fresh = pool; }
            if (fresh.Count == 0) { Resolve(s); return; }
            // prefer statements the learner got wrong or has not seen
            var q = fresh.OrderBy(x => Srs.Weak(x.id) ? 0 : Srs.Seen(x.id) ? 2 : 1).ThenBy(_ => Random.value).First();
            _usedStmts.Add(q.id);

            _qPanel = U.Col(_ui, "drive-panel");
            var top = U.Row(_qPanel);
            top.style.alignItems = Align.FlexStart;
            var fig = Listato.Figure(q);
            if (fig != null)
            {
                var f = U.Img(top, fig, "figure");
                f.style.width = 220; f.style.height = 220; f.style.flexShrink = 0;
            }
            else if (s.Type == "speed_urban")
            {
                var f = U.Img(top, SignFactory.GetTexture("limite_50"), "figure");
                f.style.width = 200; f.style.height = 200; f.style.flexShrink = 0; f.style.backgroundColor = new Color(0, 0, 0, 0);
            }
            var col = U.Col(top);
            col.style.flexGrow = 1; col.style.flexShrink = 1;
            var it = U.It(col, q.it, "it-text");
            it.style.fontSize = 36;
            var tr = QuestionText.Translation(q, out _);
            if (!StudyPage.ItalianOnly && Lang.Code != "it")
            {
                var trl = U.T(col, tr ?? "...", "tr-text");
                trl.style.fontSize = 30;
                if (tr == null) QuestionText.RequestMachine(q, res => { if (trl.panel != null) trl.text = res ?? ""; });
            }
            var spk = Chrome.IconButton(top, "speaker", () => ItalianTts.Speak(q.it));
            spk.style.flexShrink = 0;

            var answers = U.Row(_qPanel);
            answers.style.justifyContent = Justify.Center;
            answers.style.marginTop = 10;
            void Choose(bool v)
            {
                bool ok = v == q.True;
                Premium.DriveQuestionAsked();   // counted when answered (leaving the drive before answering costs nothing)
                _asked++;
                if (ok) { _correct++; Sfx.Right(); } else Sfx.Wrong();
                Srs.Record(q.id, ok);
                Gamify.Answered(ok);
                UpdateScore();
                answers.Clear();
                var verdict = U.Col(_qPanel, "card", ok ? "verdict-ok" : "verdict-ko");
                U.T(verdict, (ok ? Lang.T("right") : Lang.T("wrong")) + " - " + Lang.T("official_answer") + ": " + (q.True ? "VERO" : "FALSO"), "h2", "wraptext");
                QuestionText.ShowExplanation(verdict, q);
                var go = U.IB(_qPanel, "drive", Lang.T("keep_driving"), () =>
                {
                    _qPanel.RemoveFromHierarchy();
                    _qPanel = null;
                    Time.timeScale = 1f;
                    Resolve(s);
                }, "btn-primary");
                go.style.alignSelf = Align.Center;
                go.style.width = 600;
            }
            U.B(answers, "VERO · " + Lang.T("true"), () => Choose(true), "vf", "btn-green").style.width = 420;
            U.B(answers, "FALSO · " + Lang.T("false"), () => Choose(false), "vf", "btn-red").style.width = 420;
        }

        void Resolve(Scenario s)
        {
            s.P = Scenario.Phase.Resolving;
            StartCoroutine(ResolveRoutine(s));
        }

        IEnumerator ResolveRoutine(Scenario s)
        {
            switch (s.Type)
            {
                case "red_light":
                    yield return new WaitForSeconds(1.2f);
                    s.Light.Set(2);
                    yield return new WaitForSeconds(0.6f);
                    break;
                case "yellow_light":
                    s.Light.Set(0);
                    yield return new WaitForSeconds(1.6f);
                    s.Light.Set(2);
                    yield return new WaitForSeconds(0.6f);
                    break;
                case "stop_sign":
                case "give_way":
                    yield return new WaitForSeconds(0.8f);
                    break;
                case "pedestrian":
                    while (s.Pedestrian != null && s.Pedestrian.localPosition.x > -5.4f) yield return null;
                    yield return new WaitForSeconds(0.4f);
                    break;
                case "railway":
                {
                    while (s.BarrierAngle < 1f) yield return null;
                    var train = Build.Group("Train", _world, new Vector3(-170f, 0, s.Z0 + 5f)).transform;
                    for (int i = 0; i < 4; i++)
                    {
                        var col = i == 0 ? Theme.Hex("C0182B") : Theme.Hex("D8DDE3");
                        Build.Box(train, new Vector3(-i * 19f, 2.1f, 0), new Vector3(18.2f, 3.4f, 3f), col, 0.6f);
                        Build.Box(train, new Vector3(-i * 19f, 2.6f, 0), new Vector3(16f, 0.8f, 3.05f), Theme.Hex("23303F"), 0.9f);
                        Build.Box(train, new Vector3(-i * 19f, 0.45f, 0), new Vector3(17f, 0.6f, 2.4f), Theme.Hex("2B2F36"), 0.3f);
                    }
                    _movers.Add(new Mover { T = train, Vel = new Vector3(42f, 0, 0), Life = 9f });
                    while (train != null && train.position.x < 140f) yield return null;
                    s.Flag2 = true;
                    s.FlashA.sharedMaterial = Mats.Unlit(Theme.Hex("2A2A2A"));
                    s.FlashB.sharedMaterial = Mats.Unlit(Theme.Hex("2A2A2A"));
                    while (s.BarrierAngle > 0f)
                    {
                        SetBarriers(s, Mathf.MoveTowards(s.BarrierAngle, 0f, Time.deltaTime / 2.5f));
                        yield return null;
                    }
                    break;
                }
                case "roundabout":
                    // wait until the circulating car is not about to pass our entry
                    while (true)
                    {
                        float a = Mathf.Repeat(s.NpcAngle, 360f);
                        if (a > 25f && a < 160f) break;
                        yield return null;
                    }
                    break;
            }
            s.Released = true;
            if (s.Freeze) s.P = Scenario.Phase.Done;
        }

        // ---------------- HUD ----------------
        void BuildUI()
        {
            var hud = U.El(_ui, "hud");
            hud.pickingMode = PickingMode.Ignore;
            var sp = U.Col(hud, "hud-pill");
            sp.style.position = Position.Absolute; sp.style.left = 30; sp.style.top = 24; sp.style.width = 200;
            _speedText = U.It(sp, "0", "hud-speed");
            var kmh = U.It(sp, "km/h", "small", "muted");
            kmh.style.unityTextAlign = TextAnchor.MiddleCenter;

            var lab = U.El(hud, "hud-pill");
            lab.style.position = Position.Absolute; lab.style.top = 24; lab.style.left = new Length(30, LengthUnit.Percent); lab.style.right = new Length(30, LengthUnit.Percent);
            _labelText = U.T(lab, Lang.T("drive"), "h2");
            _labelText.style.unityTextAlign = TextAnchor.MiddleCenter;
            _labelText.style.marginBottom = 0;

            var right = U.Row(hud);
            right.style.position = Position.Absolute; right.style.right = 30; right.style.top = 24;
            _scoreText = U.It(right, "", "hud-pill", "h2");
            _scoreText.style.marginBottom = 0;
            U.B(right, Lang.T("exit"), () => App.I.GoHome(), "btn-red", "btn-small");
            UpdateScore();

            _intro = U.El(_ui, "overlay");
            var d = U.Col(_intro, "dialog");
            d.style.alignItems = Align.Center;
            U.T(d, Lang.T("drive_title"), "title").style.unityTextAlign = TextAnchor.MiddleCenter;
            U.T(d, Lang.T("drive_intro"), "ex-text").style.unityTextAlign = TextAnchor.MiddleCenter;
            var warn = U.T(d, Lang.T("drive_safety"), "gold", "bold", "wraptext");
            warn.style.marginTop = 16; warn.style.unityTextAlign = TextAnchor.MiddleCenter;
            U.IB(d, "play", Lang.T("lets_drive"), () => { _intro.RemoveFromHierarchy(); _started = true; }, "btn-green").style.width = 520;
        }

        void UpdateScore()
        {
            if (_scoreText != null) _scoreText.text = _correct + " / " + _asked;
        }

        void LateUpdate()
        {
            if (_labelText == null) return;
            var s = Current;
            var b = BiomeAt(_carZ);
            string label = s != null && s.Z0 - _carZ < 140f ? s.LabelAr : Lang.T(b == Biome.City ? "biome_city" : b == Biome.Country ? "biome_country" : "biome_highway");
            if (_labelText.text != label) _labelText.text = label;
        }
    }
}
