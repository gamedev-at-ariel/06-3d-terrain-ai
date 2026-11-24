using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

/**
 * Builds Scenes/2-player/d-israel-navigation: the Israel map (from Scenes/1-terrain/e-israel)
 * with the click-to-move player of b-navigation-demo, and a NavMesh adapted to the map:
 *
 * - The NavMesh is baked with a voxel size of 1 unit (instead of agentRadius/3 = 0.17),
 *   since the map is 3000x3000 units. With the default voxel size the bake would take very long
 *   and produce a huge NavMesh asset. With 1-unit voxels and the Humanoid's step height of 1,
 *   the agent can walk up slopes of up to 45 degrees, which matches the Humanoid's max slope -
 *   so hills are walkable, while cliffs and steep escarpments (e.g. the Dead Sea rift) are not.
 * - The water surfaces are ignored by the NavMesh, so the player does not walk on water.
 * - The sea floor and lake floors are marked "Not Walkable" by NavMeshModifierVolumes, which cover
 *   only the heights below the water level - so beaches and the land below sea level
 *   (Jordan valley, Dead Sea shores) remain walkable.
 * - Tiny disconnected NavMesh islands are removed (minRegionArea).
 * - A height mesh is built, so the agent follows the real terrain height. Without it, with 1-unit voxels,
 *   the NavMesh polygons only approximate the terrain, and the player seems to float above the ground.
 * - The agent turns quickly (like a person turning to face where they walk) instead of sliding sideways.
 *
 * Menu: Tools / Generate Israel Navigation Scene.
 */
public static class IsraelNavigationSceneBuilder {
    const string SourceScene = "Assets/Scenes/1-terrain/e-israel.unity";
    const string ScenePath = "Assets/Scenes/2-player/d-israel-navigation.unity";
    const string NavMeshDir = "Assets/Scenes/2-player/d-israel-navigation";
    const string PlayerPrefab = "Assets/Prefabs/Player.prefab";
    const int NotWalkableArea = 1;

    // Player start: Jerusalem.
    const float StartLon = 35.21f, StartLat = 31.77f;
    const float West = 32.0f, East = 38.5f, South = 29.3f, North = 34.9f;

    [MenuItem("Tools/Generate Israel Navigation Scene")]
    static void GenerateFromMenu() {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Generate();
    }

    // For command-line use: Unity -batchmode -quit -executeMethod IsraelNavigationSceneBuilder.Generate
    public static void Generate() {
        if (!File.Exists(SourceScene)) IsraelTerrainGenerator.GenerateBatch();

        // Start from a copy of the Israel terrain scene (same terrain data, water, light and fog).
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); // in case the old copy is open
        AssetDatabase.DeleteAsset(ScenePath);
        AssetDatabase.CopyAsset(SourceScene, ScenePath);
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Terrain terrain = null;
        GameObject water = null;
        foreach (var go in scene.GetRootGameObjects()) {
            if (go.GetComponent<Terrain>() != null) terrain = go.GetComponent<Terrain>();
            if (go.name == "Water") water = go;
            if (go.GetComponent<Camera>() != null) Object.DestroyImmediate(go); // the player has its own camera
        }

        // Water surfaces are not walkable ground.
        var waterModifier = water.AddComponent<NavMeshModifier>();
        waterModifier.ignoreFromBuild = true;
        waterModifier.applyToChildren = true;

        // Sea and lake floors are not walkable.
        var blockers = new GameObject("NavMesh Water Blockers");
        float floorY = terrain.transform.position.y - 1f;
        foreach (Transform body in water.transform) {
            var mesh = body.GetComponent<MeshFilter>().sharedMesh;
            float topY = body.position.y - 0.03f;
            var parent = new GameObject(body.name);
            parent.transform.SetParent(blockers.transform);
            foreach (var r in MergeQuads(mesh)) {
                var box = new GameObject("Blocker");
                box.transform.SetParent(parent.transform);
                var v = box.AddComponent<NavMeshModifierVolume>();
                v.area = NotWalkableArea;
                v.center = new Vector3(r.center.x, (floorY + topY) / 2, r.center.y);
                v.size = new Vector3(r.width, topY - floorY, r.height);
            }
        }

        // NavMesh surface on the terrain.
        var surface = terrain.gameObject.AddComponent<NavMeshSurface>();
        surface.agentTypeID = 0; // Humanoid, like the player's agent
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        surface.overrideVoxelSize = true;
        surface.voxelSize = 1f;
        surface.overrideTileSize = true;
        surface.tileSize = 128;
        surface.minRegionArea = 50f;
        surface.buildHeightMesh = true;
        surface.BuildNavMesh();

        if (!AssetDatabase.IsValidFolder(NavMeshDir))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(NavMeshDir).Replace('\\', '/'), Path.GetFileName(NavMeshDir));
        string navPath = NavMeshDir + "/NavMesh-Terrain.asset";
        AssetDatabase.DeleteAsset(navPath);
        AssetDatabase.CreateAsset(surface.navMeshData, navPath);
        EditorUtility.SetDirty(surface);

        // The player - configured like in b-navigation-demo.
        var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab), scene);
        player.name = "Player";
        player.GetComponent<CharacterController>().enabled = false;
        var keyboardMover = player.GetComponent<CharacterKeyboardMover>();
        keyboardMover.enabled = false;
        var so = new SerializedObject(keyboardMover);
        so.FindProperty("standingSpeed").floatValue = -0.01f;
        so.ApplyModifiedPropertiesWithoutUndo();

        var agent = player.AddComponent<NavMeshAgent>();
        agent.agentTypeID = 0;
        agent.radius = 0.5f;
        agent.height = 2f;
        agent.baseOffset = 1f;
        agent.speed = 3.5f;
        agent.acceleration = 8f;
        agent.angularSpeed = 720f;
        agent.stoppingDistance = 0f;

        var clickMover = player.AddComponent<ClickMover>();
        AddDefaultBinding(clickMover, "moveTo", "<Mouse>/leftButton");
        AddDefaultBinding(clickMover, "moveToLocation", "<Mouse>/position");

        // Place the player in Jerusalem, on the NavMesh.
        float x = (StartLon - West) / (East - West) * terrain.terrainData.size.x;
        float z = (StartLat - South) / (North - South) * terrain.terrainData.size.z;
        var pos = new Vector3(x, 0, z);
        pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;
        if (NavMesh.SamplePosition(pos, out var hit, 50f, NavMesh.AllAreas)) pos = hit.position;
        player.transform.position = pos + Vector3.up * agent.baseOffset;
        player.transform.rotation = Quaternion.Euler(0, 90, 0); // facing east

        LogHeightError(terrain);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        var tri = NavMesh.CalculateTriangulation();
        Debug.Log($"Israel navigation: scene saved to {ScenePath}; NavMesh has {tri.indices.Length / 3} triangles; " +
                  $"{blockers.GetComponentsInChildren<NavMeshModifierVolume>().Length} water blockers; player at {player.transform.position}");
    }

    // Reports how far the agent's walking surface is from the terrain surface, at random points.
    static void LogHeightError(Terrain terrain) {
        var rnd = new System.Random(1);
        int n = 0, over = 0;
        float sum = 0, max = 0;
        Vector3 size = terrain.terrainData.size;
        for (int i = 0; i < 20000; i++) {
            var p = new Vector3((float)rnd.NextDouble() * size.x, 0, (float)rnd.NextDouble() * size.z);
            p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
            if (!NavMesh.SamplePosition(p, out var hit, 5f, NavMesh.AllAreas)) continue;
            if (new Vector2(hit.position.x - p.x, hit.position.z - p.z).magnitude > 0.05f) continue;
            float d = Mathf.Abs(hit.position.y - p.y);
            n++; sum += d; max = Mathf.Max(max, d);
            if (d > 0.2f) over++;
        }
        if (n > 0) Debug.Log($"Israel navigation: NavMesh-to-ground height error: mean={sum / n:F3} max={max:F2} above-0.2={(float)over / n:P1} (n={n})");
    }

    static void AddDefaultBinding(ClickMover mover, string field, string path) {
        var action = (InputAction)typeof(ClickMover).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(mover);
        if (action.bindings.Count == 0) action.AddBinding(path);
        EditorUtility.SetDirty(mover);
    }

    // The water meshes consist of one quad per horizontal run of water cells.
    // Merge vertically-adjacent quads with the same x-extent into larger rectangles.
    static List<Rect> MergeQuads(Mesh mesh) {
        var verts = mesh.vertices;
        var quads = new List<Rect>();
        for (int i = 0; i + 3 < verts.Length; i += 4) {
            float x0 = Mathf.Min(verts[i].x, verts[i + 2].x), x1 = Mathf.Max(verts[i].x, verts[i + 2].x);
            float z0 = Mathf.Min(verts[i].z, verts[i + 2].z), z1 = Mathf.Max(verts[i].z, verts[i + 2].z);
            quads.Add(Rect.MinMaxRect(x0, z0, x1, z1));
        }
        quads.Sort((a, b) => a.yMin.CompareTo(b.yMin));
        var open = new Dictionary<(int, int), int>(); // x-extent -> index in result
        var result = new List<Rect>();
        foreach (var q in quads) {
            var key = (Mathf.RoundToInt(q.xMin * 100), Mathf.RoundToInt(q.xMax * 100));
            if (open.TryGetValue(key, out int idx) && Mathf.Abs(result[idx].yMax - q.yMin) < 0.01f) {
                var r = result[idx];
                r.yMax = q.yMax;
                result[idx] = r;
            } else {
                open[key] = result.Count;
                result.Add(q);
            }
        }
        return result;
    }
}
