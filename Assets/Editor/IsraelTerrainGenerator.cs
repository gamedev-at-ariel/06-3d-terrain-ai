using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Random = System.Random;

/**
 * Generates a scene with a terrain of the land of Israel and its surroundings:
 * longitude 32.0E (Cyprus / Port Said) to 38.5E (Jordan),
 * latitude 29.3N (Sinai / Eilat) to 34.9N (Lebanon / Cyprus).
 *
 * Elevation data: Assets/Terrain/Israel/israel-heights.bytes - gzipped 513x513 int16 grid
 * (meters, row 0 = south), resampled from the public AWS "Terrain Tiles" dataset (SRTM + bathymetry).
 *
 * Everything else (textures, terrain layers, tree models, grass, water) is generated procedurally,
 * using a simple rainfall model (latitude, distance from the sea, elevation and rain shadow).
 *
 * Menu: Tools / Generate Israel Terrain.
 * The generation also runs automatically once, if the scene does not exist yet.
 */
[InitializeOnLoad]
public static class IsraelTerrainGenerator {
    const string Dir = "Assets/Terrain/Israel";
    const string HeightsFile = Dir + "/israel-heights.bytes";
    const string ScenePath = "Assets/Scenes/1-terrain/e-israel.unity";

    // Geography
    const float West = 32.0f, East = 38.5f, South = 29.3f, North = 34.9f;
    const int Res = 513;              // heightmap resolution
    const float TerrainSize = 3000f;  // world units (1 unit ~ 205 meters)
    const float MetersPerUnit = 205f;
    const float VerticalExaggeration = 4f;
    const float MinElev = -1500f, MaxElev = 3100f;
    const float CellKm = 1.2f;        // distance between heightmap samples
    const float Y = VerticalExaggeration / MetersPerUnit; // world units per meter of elevation

    const int AlphaRes = 256, DetailRes = 256;

    // Layers
    const int L_SAND = 0, L_DESERT = 1, L_DRY = 2, L_GREEN = 3, L_ROCK = 4, L_SNOW = 5, L_SEABED = 6, NUM_LAYERS = 7;
    // Tree prototypes
    const int T_PINE = 0, T_OAK = 1, T_OLIVE = 2, T_PALM = 3, T_ACACIA = 4;

    static IsraelTerrainGenerator() {
        if (Application.isBatchMode) return;
        if (File.Exists(ScenePath) || SessionState.GetBool("IsraelTerrainGenerator.autoRun", false)) return;
        SessionState.SetBool("IsraelTerrainGenerator.autoRun", true);
        EditorApplication.delayCall += () => {
            if (EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(ScenePath)) return;
            Generate(false);
        };
    }

    [MenuItem("Tools/Generate Israel Terrain")]
    static void GenerateFromMenu() => Generate(true);

    // For command-line use: Unity -batchmode -quit -executeMethod IsraelTerrainGenerator.GenerateBatch
    public static void GenerateBatch() => Generate(false);

    // ------------------------------------------------------------------
    // Grids (all [y, x], y = south->north, x = west->east, size Res x Res)
    // ------------------------------------------------------------------
    static float[,] elev, rain, distSea;
    static bool[,] sea, deadSea, kinneret;
    static Random rnd;

    static float Lon(float gx) => West + gx / (Res - 1) * (East - West);
    static float Lat(float gy) => South + gy / (Res - 1) * (North - South);
    static int GX(float lon) => Mathf.RoundToInt((lon - West) / (East - West) * (Res - 1));
    static int GY(float lat) => Mathf.RoundToInt((lat - South) / (North - South) * (Res - 1));

    public static void Generate(bool openScene) {
        try {
            EditorUtility.DisplayProgressBar("Israel terrain", "Reading elevation data", 0.05f);
            rnd = new Random(1948);
            LoadHeights();
            ComputeWater();
            ComputeRain();

            EditorUtility.DisplayProgressBar("Israel terrain", "Creating textures and models", 0.2f);
            EnsureFolder(Dir + "/Textures");
            EnsureFolder(Dir + "/Layers");
            EnsureFolder(Dir + "/Models");
            TerrainLayer[] layers = CreateTerrainLayers();
            GameObject[] trees = CreateTreePrefabs();
            Texture2D greenGrass = SaveTexture("grass-green", MakeGrassTexture(new Color(0.55f, 0.75f, 0.35f), 7), 64, true);
            Texture2D dryGrass = SaveTexture("grass-dry", MakeGrassTexture(new Color(0.85f, 0.78f, 0.5f), 11), 64, true);
            GameObject stone = CreateStonePrefab();
            Material seaMat = SaveMaterial("water-sea", new Color(0.09f, 0.27f, 0.45f), 0.9f);
            Material deadSeaMat = SaveMaterial("water-deadsea", new Color(0.22f, 0.48f, 0.52f), 0.9f);
            Material lakeMat = SaveMaterial("water-lake", new Color(0.12f, 0.33f, 0.45f), 0.9f);

            EditorUtility.DisplayProgressBar("Israel terrain", "Building terrain data", 0.4f);
            TerrainData td = BuildTerrainData(layers, trees, greenGrass, dryGrass, stone);

            EditorUtility.DisplayProgressBar("Israel terrain", "Building scene", 0.9f);
            BuildScene(td, seaMat, deadSeaMat, lakeMat, openScene);
        } catch (Exception e) {
            Debug.LogException(e);
        } finally {
            EditorUtility.ClearProgressBar();
            elev = rain = distSea = null; sea = deadSea = kinneret = null;
        }
    }

    // ------------------------------------------------------------------
    // Data
    // ------------------------------------------------------------------
    static void LoadHeights() {
        byte[] raw;
        using (var fs = File.OpenRead(HeightsFile))
        using (var gz = new GZipStream(fs, CompressionMode.Decompress))
        using (var ms = new MemoryStream()) { gz.CopyTo(ms); raw = ms.ToArray(); }
        if (raw.Length != Res * Res * 2) throw new Exception("Unexpected heights file size " + raw.Length);
        elev = new float[Res, Res];
        for (int y = 0; y < Res; y++)
            for (int x = 0; x < Res; x++)
                elev[y, x] = BitConverter.ToInt16(raw, (y * Res + x) * 2);
    }

    static void ComputeWater() {
        // Open sea: flood fill from the map border through cells below sea level.
        sea = new bool[Res, Res];
        var queue = new Queue<Vector2Int>();
        for (int i = 0; i < Res; i++) {
            foreach (var p in new[] { new Vector2Int(i, 0), new Vector2Int(i, Res - 1), new Vector2Int(0, i), new Vector2Int(Res - 1, i) })
                if (elev[p.y, p.x] < -2 && !sea[p.y, p.x]) { sea[p.y, p.x] = true; queue.Enqueue(p); }
        }
        Flood(queue, sea, (x, y) => elev[y, x] < 0);

        // Lakes: connected component around a seed, below a threshold.
        deadSea = Lake(35.48f, 31.5f, -405f);
        kinneret = Lake(35.58f, 32.82f, -205f);

        for (int y = 0; y < Res; y++)
            for (int x = 0; x < Res; x++) {
                if (sea[y, x]) elev[y, x] = Mathf.Min(elev[y, x], -8f);
                if (deadSea[y, x]) elev[y, x] = -435f;
                if (kinneret[y, x]) elev[y, x] = -230f;
            }
    }

    static bool[,] Lake(float lon, float lat, float threshold) {
        var mask = new bool[Res, Res];
        var q = new Queue<Vector2Int>();
        var seed = new Vector2Int(GX(lon), GY(lat));
        mask[seed.y, seed.x] = true; q.Enqueue(seed);
        Flood(q, mask, (x, y) => elev[y, x] <= threshold);
        return mask;
    }

    static void Flood(Queue<Vector2Int> q, bool[,] mask, Func<int, int, bool> ok) {
        var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        while (q.Count > 0) {
            var p = q.Dequeue();
            foreach (var d in dirs) {
                var n = p + d;
                if (n.x < 0 || n.y < 0 || n.x >= Res || n.y >= Res || mask[n.y, n.x] || !ok(n.x, n.y)) continue;
                mask[n.y, n.x] = true; q.Enqueue(n);
            }
        }
    }

    static bool IsWater(int x, int y) => sea[y, x] || deadSea[y, x] || kinneret[y, x];

    // Mean annual rainfall at the coast, by latitude (mm), roughly from climate data.
    static readonly float[] rainLat = { 29.3f, 30.5f, 31.0f, 31.5f, 32.0f, 32.8f, 33.9f, 34.9f };
    static readonly float[] rainMm = { 20f, 50f, 110f, 320f, 520f, 620f, 820f, 650f };

    static float CoastRain(float lat) {
        if (lat <= rainLat[0]) return rainMm[0];
        for (int i = 1; i < rainLat.Length; i++)
            if (lat <= rainLat[i]) return Mathf.Lerp(rainMm[i - 1], rainMm[i], Mathf.InverseLerp(rainLat[i - 1], rainLat[i], lat));
        return rainMm[rainMm.Length - 1];
    }

    static void ComputeRain() {
        // Chamfer distance (in km) from the open sea.
        distSea = new float[Res, Res];
        const float INF = 1e6f, D1 = CellKm, D2 = CellKm * 1.4142f;
        for (int y = 0; y < Res; y++) for (int x = 0; x < Res; x++) distSea[y, x] = sea[y, x] ? 0 : INF;
        for (int y = 0; y < Res; y++) for (int x = 0; x < Res; x++) {
            float d = distSea[y, x];
            if (x > 0) d = Mathf.Min(d, distSea[y, x - 1] + D1);
            if (y > 0) d = Mathf.Min(d, distSea[y - 1, x] + D1);
            if (x > 0 && y > 0) d = Mathf.Min(d, distSea[y - 1, x - 1] + D2);
            if (x < Res - 1 && y > 0) d = Mathf.Min(d, distSea[y - 1, x + 1] + D2);
            distSea[y, x] = d;
        }
        for (int y = Res - 1; y >= 0; y--) for (int x = Res - 1; x >= 0; x--) {
            float d = distSea[y, x];
            if (x < Res - 1) d = Mathf.Min(d, distSea[y, x + 1] + D1);
            if (y < Res - 1) d = Mathf.Min(d, distSea[y + 1, x] + D1);
            if (x < Res - 1 && y < Res - 1) d = Mathf.Min(d, distSea[y + 1, x + 1] + D2);
            if (x > 0 && y < Res - 1) d = Mathf.Min(d, distSea[y + 1, x - 1] + D2);
            distSea[y, x] = d;
        }

        // Rain: decays inland, grows with elevation, and drops in the rain shadow east of mountains
        // (the prevailing winter winds come from the west).
        rain = new float[Res, Res];
        const int upwind = 50; // ~60 km
        for (int y = 0; y < Res; y++) {
            float coast = CoastRain(Lat(y));
            for (int x = 0; x < Res; x++) {
                float e = elev[y, x];
                float upMax = e;
                for (int k = 1; k <= upwind && x - k >= 0; k++) upMax = Mathf.Max(upMax, elev[y, x - k]);
                float r = coast
                          * Mathf.Exp(-distSea[y, x] / 130f)
                          * (1f + Mathf.Max(e, 0f) / 2000f)
                          * Mathf.Exp(-Mathf.Max(0f, upMax - e) / 1300f);
                // Irrigated eastern Nile delta.
                float lon = Lon(x), lat = Lat(y);
                float irrigated = Smooth(32.45f, 32.2f, lon) * Smooth(30.3f, 30.6f, lat) * Smooth(31.35f, 31.15f, lat) * Smooth(25f, 5f, e);
                r = Mathf.Max(r, 600f * irrigated);
                rain[y, x] = r;
            }
        }
    }

    static float Sample(float[,] g, float u, float v) {
        float fx = Mathf.Clamp01(u) * (Res - 1), fy = Mathf.Clamp01(v) * (Res - 1);
        int x0 = Mathf.Min((int)fx, Res - 2), y0 = Mathf.Min((int)fy, Res - 2);
        float tx = fx - x0, ty = fy - y0;
        return Mathf.Lerp(Mathf.Lerp(g[y0, x0], g[y0, x0 + 1], tx), Mathf.Lerp(g[y0 + 1, x0], g[y0 + 1, x0 + 1], tx), ty);
    }

    static bool WaterAt(float u, float v) {
        int x = Mathf.RoundToInt(Mathf.Clamp01(u) * (Res - 1)), y = Mathf.RoundToInt(Mathf.Clamp01(v) * (Res - 1));
        return IsWater(x, y);
    }

    static float Noise(float u, float v, float freq, float seed) {
        float n = 0, amp = 1, total = 0;
        for (int o = 0; o < 4; o++) {
            n += amp * Mathf.PerlinNoise(u * freq + seed, v * freq + seed * 1.7f);
            total += amp; amp *= 0.5f; freq *= 2f;
        }
        return n / total; // ~0..1
    }

    static float Smooth(float a, float b, float x) {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3 - 2 * t);
    }

    // ------------------------------------------------------------------
    // Terrain data
    // ------------------------------------------------------------------
    static TerrainData BuildTerrainData(TerrainLayer[] layers, GameObject[] trees, Texture2D greenGrass, Texture2D dryGrass, GameObject stone) {
        var td = new TerrainData();
        td.heightmapResolution = Res;
        td.size = new Vector3(TerrainSize, (MaxElev - MinElev) * Y, TerrainSize);
        // Save the asset first, so that the splat-map textures are stored as its sub-assets.
        string path = Dir + "/israel-terrain.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(td, path);

        var h = new float[Res, Res];
        for (int y = 0; y < Res; y++)
            for (int x = 0; x < Res; x++)
                h[y, x] = Mathf.InverseLerp(MinElev, MaxElev, elev[y, x]);
        td.SetHeights(0, 0, h);

        // Splat map
        td.terrainLayers = layers;
        td.alphamapResolution = AlphaRes;
        var alpha = new float[AlphaRes, AlphaRes, NUM_LAYERS];
        var w = new float[NUM_LAYERS];
        for (int ay = 0; ay < AlphaRes; ay++) {
            for (int ax = 0; ax < AlphaRes; ax++) {
                float u = (ax + 0.5f) / AlphaRes, v = (ay + 0.5f) / AlphaRes;
                Array.Clear(w, 0, w.Length);
                if (WaterAt(u, v)) {
                    w[L_SEABED] = 1;
                } else {
                    float e = Sample(elev, u, v);
                    float n = Noise(u, v, 40f, 3.1f);
                    float r = Sample(rain, u, v) * (0.7f + 0.6f * n);
                    float steep = td.GetSteepness(u, v);
                    float desert = 1 - Smooth(150, 300, r);
                    float green = Smooth(420, 600, r);
                    float dry = Mathf.Clamp01(1 - desert - green);
                    float sand = desert * (1 - Smooth(250, 650, e)) * (1 - Smooth(8, 25, steep)) * Smooth(0.3f, 0.6f, Noise(u, v, 25f, 9.3f) + 0.15f);
                    if (e < 8 && Sample(distSea, u, v) < 2.5f) sand = Mathf.Max(sand, 0.9f); // beaches
                    w[L_SAND] = sand;
                    w[L_DESERT] = Mathf.Max(0, desert - sand);
                    w[L_DRY] = dry;
                    w[L_GREEN] = green;
                    float rock = Smooth(28, 45, steep + 10 * (n - 0.5f));
                    for (int l = 0; l < NUM_LAYERS; l++) w[l] *= 1 - rock;
                    w[L_ROCK] = rock;
                    float snow = Smooth(2250, 2550, e + 300 * (n - 0.5f));
                    if (snow > 0) {
                        for (int l = 0; l < NUM_LAYERS; l++) w[l] *= 1 - snow;
                        w[L_SNOW] = snow;
                    }
                }
                float sum = 0;
                for (int l = 0; l < NUM_LAYERS; l++) sum += w[l];
                if (sum <= 0) { w[L_DESERT] = 1; sum = 1; }
                for (int l = 0; l < NUM_LAYERS; l++) alpha[ay, ax, l] = w[l] / sum;
            }
        }
        td.SetAlphamaps(0, 0, alpha);

        // Trees
        var protos = new TreePrototype[trees.Length];
        for (int i = 0; i < trees.Length; i++) protos[i] = new TreePrototype { prefab = trees[i] };
        td.treePrototypes = protos;
        td.SetTreeInstances(PlaceTrees(td).ToArray(), true);

        // Details
        td.SetDetailResolution(DetailRes, 32);
        td.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
        td.detailPrototypes = new[] {
            new DetailPrototype {
                prototypeTexture = greenGrass, renderMode = DetailRenderMode.GrassBillboard, usePrototypeMesh = false,
                minWidth = 0.6f, maxWidth = 1.2f, minHeight = 0.4f, maxHeight = 0.9f, noiseSpread = 0.3f,
                healthyColor = new Color(0.55f, 0.75f, 0.35f), dryColor = new Color(0.7f, 0.7f, 0.4f),
            },
            new DetailPrototype {
                prototypeTexture = dryGrass, renderMode = DetailRenderMode.GrassBillboard, usePrototypeMesh = false,
                minWidth = 0.6f, maxWidth = 1.1f, minHeight = 0.3f, maxHeight = 0.7f, noiseSpread = 0.3f,
                healthyColor = new Color(0.85f, 0.78f, 0.5f), dryColor = new Color(0.75f, 0.62f, 0.4f),
            },
            new DetailPrototype {
                prototype = stone, renderMode = DetailRenderMode.VertexLit, usePrototypeMesh = true, useInstancing = true,
                minWidth = 0.3f, maxWidth = 0.9f, minHeight = 0.2f, maxHeight = 0.6f, noiseSpread = 0.5f,
                healthyColor = Color.white, dryColor = Color.white,
            },
        };
        var dGreen = new int[DetailRes, DetailRes];
        var dDry = new int[DetailRes, DetailRes];
        var dStone = new int[DetailRes, DetailRes];
        for (int dy = 0; dy < DetailRes; dy++) {
            for (int dx = 0; dx < DetailRes; dx++) {
                float u = (dx + 0.5f) / DetailRes, v = (dy + 0.5f) / DetailRes;
                if (WaterAt(u, v)) continue;
                float e = Sample(elev, u, v);
                float r = Sample(rain, u, v) * (0.7f + 0.6f * Noise(u, v, 40f, 3.1f));
                float flat = 1 - Smooth(25, 40, td.GetSteepness(u, v));
                float snow = Smooth(2200, 2400, e);
                float green = Smooth(420, 600, r), desert = 1 - Smooth(150, 300, r);
                float dry = Mathf.Clamp01(1 - desert - green);
                dGreen[dy, dx] = Mathf.RoundToInt(16 * green * flat * (1 - snow) * (float)rnd.NextDouble());
                dDry[dy, dx] = Mathf.RoundToInt(16 * dry * flat * (float)rnd.NextDouble());
                dStone[dy, dx] = Mathf.RoundToInt(8 * desert * (float)rnd.NextDouble());
            }
        }
        td.SetDetailLayer(0, 0, 0, dGreen);
        td.SetDetailLayer(0, 0, 1, dDry);
        td.SetDetailLayer(0, 0, 2, dStone);
        td.wavingGrassAmount = 0.2f;
        td.wavingGrassStrength = 0.3f;
        td.wavingGrassTint = new Color(0.8f, 0.8f, 0.7f);

        EditorUtility.SetDirty(td);
        AssetDatabase.SaveAssets();
        return td;
    }

    static List<TreeInstance> PlaceTrees(TerrainData td) {
        var list = new List<TreeInstance>();
        var counts = new int[5];
        const int grid = 500;
        var p = new float[5];
        for (int gy = 0; gy < grid; gy++) {
            for (int gx = 0; gx < grid; gx++) {
                float u = (gx + (float)rnd.NextDouble()) / grid, v = (gy + (float)rnd.NextDouble()) / grid;
                if (WaterAt(u, v)) continue;
                // keep trees away from the shoreline
                const float m = 1.5f / (Res - 1);
                if (WaterAt(u + m, v) || WaterAt(u - m, v) || WaterAt(u, v + m) || WaterAt(u, v - m)) continue;
                float steep = td.GetSteepness(u, v);
                if (steep > 40) continue;
                float e = Sample(elev, u, v);
                float r = Sample(rain, u, v) * (0.7f + 0.6f * Noise(u, v, 40f, 3.1f));
                float lon = West + u * (East - West), lat = South + v * (North - South);
                float dist = Sample(distSea, u, v);
                float forest = Noise(u, v, 60f, 17.7f); // patchiness of forests

                p[T_PINE] = 0.75f * Smooth(450, 800, r) * Smooth(100, 300, e) * (1 - Smooth(1800, 2100, e)) * Smooth(0.35f, 0.6f, forest);
                p[T_OAK] = 0.45f * Smooth(420, 700, r) * (1 - Smooth(1200, 1600, e)) * Smooth(0.3f, 0.55f, 1 - forest);
                p[T_OLIVE] = 0.30f * Smooth(300, 420, r) * (1 - Smooth(550, 800, r)) * (1 - Smooth(800, 1100, e));
                float palm = 0;
                if (e < -150 && lon > 35.3f && lon < 35.75f && lat > 31f && lat < 32.6f) palm = 0.5f;        // Jordan valley / Dead Sea oases
                if (lat < 31.8f && e < 40 && dist < 6 && lat > 30.8f) palm = Mathf.Max(palm, 0.35f);       // Gaza / El-Arish coast
                if (lon > 34.9f && lon < 35.4f && lat > 29.5f && lat < 30.9f && e < 250) palm = Mathf.Max(palm, 0.04f); // Arava
                if (lon < 32.35f && lat > 30.4f && lat < 31.3f && e < 15) palm = Mathf.Max(palm, 0.15f);    // Nile delta
                p[T_PALM] = palm;
                p[T_ACACIA] = 0.03f * (1 - Smooth(120, 250, r)) * (1 - Smooth(500, 800, e)) * (1 - Smooth(10, 25, steep));

                double roll = rnd.NextDouble(), acc = 0;
                for (int t = 0; t < 5; t++) {
                    acc += p[t];
                    if (roll >= acc) continue;
                    float s = TreeScale(t) * (0.8f + 0.4f * (float)rnd.NextDouble());
                    list.Add(new TreeInstance {
                        position = new Vector3(u, 0, v), prototypeIndex = t,
                        heightScale = s, widthScale = s * (0.85f + 0.3f * (float)rnd.NextDouble()),
                        rotation = (float)(rnd.NextDouble() * Math.PI * 2),
                        color = Color.white, lightmapColor = Color.white,
                    });
                    counts[t]++;
                    break;
                }
            }
        }
        Debug.Log($"Israel terrain: trees: pine={counts[0]} oak={counts[1]} olive={counts[2]} palm={counts[3]} acacia={counts[4]} total={list.Count}");
        return list;
    }

    static float TreeScale(int t) {
        switch (t) {
            case T_PINE: return 3.2f;
            case T_OAK: return 2.4f;
            case T_OLIVE: return 1.8f;
            case T_PALM: return 3f;
            default: return 2f;
        }
    }

    // ------------------------------------------------------------------
    // Scene
    // ------------------------------------------------------------------
    static void BuildScene(TerrainData td, Material seaMat, Material deadSeaMat, Material lakeMat, bool openScene) {
        Scene previousActive = SceneManager.GetActiveScene();
        bool hasUntitled = false;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path)) hasUntitled = true;

        if (hasUntitled && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var mode = hasUntitled ? NewSceneMode.Single : NewSceneMode.Additive;
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, mode);
        SceneManager.SetActiveScene(scene);

        // Terrain
        GameObject terrainGO = Terrain.CreateTerrainGameObject(td);
        terrainGO.name = "Israel Terrain";
        terrainGO.transform.position = new Vector3(0, MinElev * Y, 0);
        var terrain = terrainGO.GetComponent<Terrain>();
        terrain.heightmapPixelError = 4;
        terrain.basemapDistance = 1500;
        terrain.treeDistance = 2000;
        terrain.treeBillboardDistance = 2000;
        terrain.treeMaximumFullLODCount = 100000;
        terrain.detailObjectDistance = 250;
        terrain.detailObjectDensity = 1;
        terrain.drawInstanced = true;

        // Water
        var water = new GameObject("Water");
        CreateWater("Mediterranean and Red Sea", sea, 0f, seaMat, water.transform);
        CreateWater("Dead Sea", deadSea, -415f, deadSeaMat, water.transform);
        CreateWater("Sea of Galilee", kinneret, -210f, lakeMat, water.transform);

        // Light
        foreach (var go in scene.GetRootGameObjects()) {
            var light = go.GetComponent<Light>();
            if (light != null) {
                light.transform.rotation = Quaternion.Euler(45f, 150f, 0f); // afternoon sun from the south-west
                light.color = new Color(1f, 0.95f, 0.84f);
                light.intensity = 1.1f;
                light.shadows = LightShadows.Soft;
            }
            var cam = go.GetComponent<Camera>();
            if (cam != null) {
                // Over the Mediterranean, west of Tel-Aviv, looking east.
                cam.transform.position = new Vector3(650f, 380f, 1150f);
                cam.transform.LookAt(new Vector3(1600f, 0f, 1750f));
                cam.nearClipPlane = 0.5f;
                cam.farClipPlane = 6000f;
                go.AddComponent<FlyCamera>();
            }
        }

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.72f, 0.79f, 0.86f);
        RenderSettings.fogStartDistance = 1200f;
        RenderSettings.fogEndDistance = 5500f;

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("Israel terrain: scene saved to " + ScenePath);

        if (mode == NewSceneMode.Additive) {
            if (openScene && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            } else {
                if (previousActive.IsValid()) SceneManager.SetActiveScene(previousActive);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));
    }

    // Water surface: one quad per horizontal run of water cells (keeps the mesh tiny).
    static void CreateWater(string name, bool[,] mask, float levelMeters, Material mat, Transform parent) {
        const int cells = (Res - 1) / 2; // 256
        float cs = TerrainSize / cells;
        var cellMask = new bool[cells, cells];
        for (int cy = 0; cy < cells; cy++)
            for (int cx = 0; cx < cells; cx++)
                for (int dy = -1; dy <= 3 && !cellMask[cy, cx]; dy++)   // includes a one-sample margin
                    for (int dx = -1; dx <= 3; dx++) {
                        int x = cx * 2 + dx, y = cy * 2 + dy;
                        if (x >= 0 && y >= 0 && x < Res && y < Res && mask[y, x]) { cellMask[cy, cx] = true; break; }
                    }

        var verts = new List<Vector3>(); var tris = new List<int>(); var uvs = new List<Vector2>();
        for (int cy = 0; cy < cells; cy++) {
            int cx = 0;
            while (cx < cells) {
                if (!cellMask[cy, cx]) { cx++; continue; }
                int start = cx;
                while (cx < cells && cellMask[cy, cx]) cx++;
                int i = verts.Count;
                float x0 = start * cs, x1 = cx * cs, z0 = cy * cs, z1 = (cy + 1) * cs;
                verts.Add(new Vector3(x0, 0, z0)); verts.Add(new Vector3(x0, 0, z1));
                verts.Add(new Vector3(x1, 0, z1)); verts.Add(new Vector3(x1, 0, z0));
                foreach (var vv in verts.GetRange(i, 4)) uvs.Add(new Vector2(vv.x, vv.z) / 100f);
                tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
        }
        var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        mesh = SaveMesh(mesh, "water-" + name.ToLower().Replace(' ', '-'));

        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = new Vector3(0, levelMeters * Y, 0);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // ------------------------------------------------------------------
    // Assets: textures, layers, materials, models
    // ------------------------------------------------------------------
    static void EnsureFolder(string path) {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }

    static Texture2D SaveTexture(string name, Texture2D tex, int maxSize, bool alpha) {
        string path = $"{Dir}/Textures/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.maxTextureSize = maxSize;
        imp.alphaIsTransparency = alpha;
        imp.wrapMode = alpha ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
        imp.mipmapEnabled = true;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material SaveMaterial(string name, Color color, float smoothness) {
        string path = $"{Dir}/Models/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) {
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = color;
        mat.SetFloat("_Glossiness", smoothness);
        mat.SetFloat("_Metallic", 0f);
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Mesh SaveMesh(Mesh mesh, string name) {
        string path = $"{Dir}/Models/{name}.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static GameObject SavePrefab(string name, Mesh mesh, Material[] mats) {
        var go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = mats;
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{Dir}/Models/{name}.prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }

    // --- Tileable noise for textures ---
    static float Hash(int x, int y, int seed) {
        unchecked {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }

    static float TileNoise(float u, float v, int period, int seed) {
        float fx = u * period, fy = v * period;
        int ix = Mathf.FloorToInt(fx), iy = Mathf.FloorToInt(fy);
        float tx = fx - ix, ty = fy - iy;
        tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty);
        int x0 = ((ix % period) + period) % period, y0 = ((iy % period) + period) % period;
        int x1 = (x0 + 1) % period, y1 = (y0 + 1) % period;
        return Mathf.Lerp(Mathf.Lerp(Hash(x0, y0, seed), Hash(x1, y0, seed), tx),
                          Mathf.Lerp(Hash(x0, y1, seed), Hash(x1, y1, seed), tx), ty);
    }

    static float TileFbm(float u, float v, int basePeriod, int seed) {
        float n = 0, amp = 1, total = 0;
        for (int o = 0, p = basePeriod; o < 5; o++, p *= 2) { n += amp * TileNoise(u, v, p, seed + o); total += amp; amp *= 0.5f; }
        return n / total;
    }

    static Texture2D MakeGroundTexture(int size, Func<float, float, Color> f) {
        var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = f((float)x / size, (float)y / size);
        tex.SetPixels(px); tex.Apply();
        return tex;
    }

    static TerrainLayer[] CreateTerrainLayers() {
        const int S = 128;
        var defs = new (string name, Func<float, float, Color> f, float tile)[] {
            ("sand", (u, v) => {
                float n = TileFbm(u, v, 4, 1);
                float ripple = 0.5f + 0.5f * Mathf.Sin(2 * Mathf.PI * (v * 8 + 1.5f * n));
                return Color.Lerp(new Color(0.80f, 0.68f, 0.47f), new Color(0.92f, 0.82f, 0.60f), 0.6f * n + 0.4f * ripple);
            }, 30f),
            ("desert", (u, v) => {
                float n = TileFbm(u, v, 4, 2), speck = TileNoise(u, v, 64, 3);
                Color c = Color.Lerp(new Color(0.58f, 0.44f, 0.31f), new Color(0.74f, 0.60f, 0.44f), n);
                return speck > 0.8f ? c * 0.75f : c;
            }, 40f),
            ("dry-grass", (u, v) => {
                float n = TileFbm(u, v, 4, 4), blades = TileNoise(u, v, 64, 5);
                Color c = Color.Lerp(new Color(0.55f, 0.50f, 0.32f), new Color(0.72f, 0.66f, 0.42f), n);
                return Color.Lerp(c, new Color(0.45f, 0.47f, 0.28f), blades * 0.4f);
            }, 35f),
            ("green", (u, v) => {
                float n = TileFbm(u, v, 4, 6), blades = TileNoise(u, v, 64, 7);
                Color c = Color.Lerp(new Color(0.22f, 0.33f, 0.14f), new Color(0.38f, 0.48f, 0.22f), n);
                return Color.Lerp(c, new Color(0.45f, 0.40f, 0.28f), blades * 0.3f);
            }, 35f),
            ("rock", (u, v) => {
                float n = TileFbm(u, v, 4, 8), crack = Mathf.Abs(TileNoise(u, v, 16, 9) - 0.5f);
                Color c = Color.Lerp(new Color(0.48f, 0.45f, 0.41f), new Color(0.70f, 0.67f, 0.61f), n);
                return crack < 0.04f ? c * 0.7f : c;
            }, 25f),
            ("snow", (u, v) => {
                float n = TileFbm(u, v, 4, 10);
                return Color.Lerp(new Color(0.85f, 0.88f, 0.93f), new Color(0.97f, 0.98f, 1f), n);
            }, 30f),
            ("seabed", (u, v) => {
                float n = TileFbm(u, v, 4, 11);
                return Color.Lerp(new Color(0.40f, 0.42f, 0.36f), new Color(0.58f, 0.56f, 0.45f), n);
            }, 40f),
        };
        var layers = new TerrainLayer[defs.Length];
        for (int i = 0; i < defs.Length; i++) {
            Texture2D tex = SaveTexture(defs[i].name, MakeGroundTexture(S, defs[i].f), S, false);
            string path = $"{Dir}/Layers/{defs[i].name}.terrainlayer";
            AssetDatabase.DeleteAsset(path);
            var layer = new TerrainLayer {
                diffuseTexture = tex,
                tileSize = new Vector2(defs[i].tile, defs[i].tile),
                smoothness = defs[i].name == "snow" ? 0.3f : 0f,
            };
            AssetDatabase.CreateAsset(layer, path);
            layers[i] = layer;
        }
        return layers;
    }

    static Texture2D MakeGrassTexture(Color tint, int seed) {
        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        var px = new Color[S * S];
        var r = new Random(seed);
        for (int b = 0; b < 16; b++) {
            float baseX = 4 + (float)r.NextDouble() * (S - 8);
            float height = S * (0.45f + 0.5f * (float)r.NextDouble());
            float lean = ((float)r.NextDouble() - 0.5f) * 16f;
            float width = 1.5f + (float)r.NextDouble() * 1.5f;
            float shade = 0.7f + 0.3f * (float)r.NextDouble();
            for (int y = 0; y < height; y++) {
                float t = y / height;
                float cx = baseX + lean * t * t, hw = width * (1 - t) + 0.3f;
                for (int x = Mathf.FloorToInt(cx - hw); x <= Mathf.CeilToInt(cx + hw); x++) {
                    if (x < 0 || x >= S || Mathf.Abs(x - cx) > hw) continue;
                    Color c = Color.Lerp(tint * 0.6f, tint * 1.1f, t) * shade;
                    c.a = 1;
                    px[y * S + x] = c;
                }
            }
        }
        tex.SetPixels(px); tex.Apply();
        return tex;
    }

    // --- Low-poly mesh builder (flat shaded) ---
    class MeshBuilder {
        readonly List<Vector3> verts = new List<Vector3>(), norms = new List<Vector3>();
        readonly List<int>[] subs;
        public MeshBuilder(int submeshes) {
            subs = new List<int>[submeshes];
            for (int i = 0; i < submeshes; i++) subs[i] = new List<int>();
        }

        // Adds a triangle facing the direction of 'outward'.
        public void Tri(int s, Vector3 a, Vector3 b, Vector3 c, Vector3 outward) {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, outward) < 0) { (b, c) = (c, b); n = -n; }
            n.Normalize();
            foreach (var p in new[] { a, b, c }) { subs[s].Add(verts.Count); verts.Add(p); norms.Add(n); }
        }

        public void Quad(int s, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward) {
            Tri(s, a, b, c, outward); Tri(s, a, c, d, outward);
        }

        // Truncated cone around the vertical axis through 'center' (r1 = 0 gives a cone).
        public void Frustum(int s, Vector3 center, float y0, float y1, float r0, float r1, int sides, bool bottomCap = false) {
            for (int i = 0; i < sides; i++) {
                float a0 = i * 2 * Mathf.PI / sides, a1 = (i + 1) * 2 * Mathf.PI / sides;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                Vector3 p00 = center + d0 * r0 + Vector3.up * y0, p10 = center + d1 * r0 + Vector3.up * y0;
                Vector3 p01 = center + d0 * r1 + Vector3.up * y1, p11 = center + d1 * r1 + Vector3.up * y1;
                Vector3 outDir = (d0 + d1) * 0.5f;
                if (r1 <= 0) Tri(s, p00, p10, p01, outDir + Vector3.up * 0.3f);
                else Quad(s, p00, p10, p11, p01, outDir);
                if (bottomCap) Tri(s, center + Vector3.up * y0, p00, p10, Vector3.down);
            }
        }

        public void Blob(int s, Vector3 c, Vector3 radii, int rings, int segs, Random jitter = null, float amount = 0) {
            var grid = new Vector3[rings + 1, segs];
            for (int i = 0; i <= rings; i++) {
                float phi = Mathf.PI * i / rings;
                for (int j = 0; j < segs; j++) {
                    float th = 2 * Mathf.PI * j / segs;
                    var dir = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                    float k = 1;
                    if (jitter != null && i > 0 && i < rings) k += amount * ((float)jitter.NextDouble() - 0.5f) * 2;
                    grid[i, j] = c + Vector3.Scale(dir * k, radii);
                }
            }
            for (int i = 0; i < rings; i++)
                for (int j = 0; j < segs; j++) {
                    int j1 = (j + 1) % segs;
                    Vector3 a = grid[i, j], b = grid[i, j1], cc = grid[i + 1, j1], d = grid[i + 1, j];
                    Vector3 outDir = (a + b + cc + d) * 0.25f - c;
                    if (i == 0) Tri(s, a, cc, d, outDir);
                    else if (i == rings - 1) Tri(s, a, b, cc, outDir);
                    else Quad(s, a, b, cc, d, outDir);
                }
        }

        public Mesh Build(string name) {
            var m = new Mesh { name = name };
            m.SetVertices(verts); m.SetNormals(norms);
            m.subMeshCount = subs.Length;
            for (int i = 0; i < subs.Length; i++) m.SetTriangles(subs[i], i);
            m.RecalculateBounds();
            return m;
        }
    }

    static GameObject[] CreateTreePrefabs() {
        Material bark = SaveMaterial("bark", new Color(0.33f, 0.24f, 0.16f), 0.05f);
        Material palmBark = SaveMaterial("palm-bark", new Color(0.47f, 0.38f, 0.26f), 0.05f);
        Material pineLeaf = SaveMaterial("leaves-pine", new Color(0.12f, 0.28f, 0.13f), 0.1f);
        Material oakLeaf = SaveMaterial("leaves-oak", new Color(0.21f, 0.36f, 0.14f), 0.1f);
        Material oliveLeaf = SaveMaterial("leaves-olive", new Color(0.46f, 0.53f, 0.37f), 0.1f);
        Material palmLeaf = SaveMaterial("leaves-palm", new Color(0.30f, 0.45f, 0.17f), 0.1f);
        Material acaciaLeaf = SaveMaterial("leaves-acacia", new Color(0.42f, 0.47f, 0.23f), 0.1f);
        var r = new Random(5);
        var result = new GameObject[5];

        // Pine (Jerusalem pine / Lebanon cedar)
        var mb = new MeshBuilder(2);
        mb.Frustum(0, Vector3.zero, 0, 0.3f, 0.06f, 0.045f, 6);
        mb.Frustum(1, Vector3.zero, 0.2f, 0.58f, 0.38f, 0, 8, true);
        mb.Frustum(1, Vector3.zero, 0.42f, 0.8f, 0.3f, 0, 8, true);
        mb.Frustum(1, Vector3.zero, 0.62f, 1.0f, 0.2f, 0, 8, true);
        result[T_PINE] = SavePrefab("tree-pine", SaveMesh(mb.Build("tree-pine"), "tree-pine"), new[] { bark, pineLeaf });

        // Oak (Tabor oak / carob)
        mb = new MeshBuilder(2);
        mb.Frustum(0, Vector3.zero, 0, 0.42f, 0.08f, 0.05f, 6);
        mb.Blob(1, new Vector3(0, 0.62f, 0), new Vector3(0.42f, 0.3f, 0.42f), 5, 8, r, 0.12f);
        mb.Blob(1, new Vector3(0.18f, 0.78f, 0.08f), new Vector3(0.25f, 0.2f, 0.25f), 4, 7, r, 0.1f);
        mb.Blob(1, new Vector3(-0.15f, 0.74f, -0.12f), new Vector3(0.24f, 0.2f, 0.24f), 4, 7, r, 0.1f);
        result[T_OAK] = SavePrefab("tree-oak", SaveMesh(mb.Build("tree-oak"), "tree-oak"), new[] { bark, oakLeaf });

        // Olive
        mb = new MeshBuilder(2);
        mb.Frustum(0, Vector3.zero, 0, 0.32f, 0.09f, 0.06f, 6);
        mb.Blob(1, new Vector3(0, 0.48f, 0), new Vector3(0.4f, 0.22f, 0.4f), 4, 8, r, 0.2f);
        result[T_OLIVE] = SavePrefab("tree-olive", SaveMesh(mb.Build("tree-olive"), "tree-olive"), new[] { bark, oliveLeaf });

        // Date palm
        mb = new MeshBuilder(2);
        mb.Frustum(0, Vector3.zero, 0, 0.88f, 0.045f, 0.035f, 6);
        var top = new Vector3(0, 0.88f, 0);
        const int fronds = 10, segs = 4;
        for (int f = 0; f < fronds; f++) {
            float a = f * 2 * Mathf.PI / fronds + (float)r.NextDouble() * 0.3f;
            float rise = f % 2 == 0 ? 0.25f : 0.05f;
            var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            var side = new Vector3(-dir.z, 0, dir.x);
            Vector3 Point(float t) => top + dir * (0.42f * t) + Vector3.up * (rise * t - 0.45f * t * t);
            for (int k = 0; k < segs; k++) {
                float t0 = (float)k / segs, t1 = (float)(k + 1) / segs;
                float w0 = 0.09f * Mathf.Sin(Mathf.PI * Mathf.Max(t0, 0.1f)), w1 = 0.09f * Mathf.Sin(Mathf.PI * t1);
                Vector3 a0 = Point(t0) - side * w0, b0 = Point(t0) + side * w0, a1 = Point(t1) - side * w1, b1 = Point(t1) + side * w1;
                mb.Quad(1, a0, b0, b1, a1, Vector3.up);   // double sided
                mb.Quad(1, a0, b0, b1, a1, Vector3.down);
            }
        }
        result[T_PALM] = SavePrefab("tree-palm", SaveMesh(mb.Build("tree-palm"), "tree-palm"), new[] { palmBark, palmLeaf });

        // Acacia (umbrella-shaped desert tree)
        mb = new MeshBuilder(2);
        mb.Frustum(0, Vector3.zero, 0, 0.45f, 0.045f, 0.03f, 5);
        mb.Frustum(0, new Vector3(0.08f, 0, 0.05f), 0.25f, 0.5f, 0.025f, 0.015f, 4);
        mb.Blob(1, new Vector3(0, 0.52f, 0), new Vector3(0.5f, 0.09f, 0.5f), 3, 10, r, 0.15f);
        result[T_ACACIA] = SavePrefab("tree-acacia", SaveMesh(mb.Build("tree-acacia"), "tree-acacia"), new[] { bark, acaciaLeaf });

        return result;
    }

    static GameObject CreateStonePrefab() {
        Material stone = SaveMaterial("stone", new Color(0.55f, 0.47f, 0.38f), 0.05f);
        var mb = new MeshBuilder(1);
        mb.Blob(0, new Vector3(0, 0.25f, 0), new Vector3(0.5f, 0.35f, 0.45f), 4, 6, new Random(3), 0.3f);
        return SavePrefab("stone", SaveMesh(mb.Build("stone"), "stone"), new[] { stone });
    }
}
