using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Bloom = UnityEngine.Rendering.Universal.Bloom;
using ColorAdjustments = UnityEngine.Rendering.Universal.ColorAdjustments;
using Vignette = UnityEngine.Rendering.Universal.Vignette;

public static class FlowerSeaVolumeFxBuilder
{
    private const string ScenePath = "Assets/Scenes/Subway Flower Sea.unity";
    private const string AssetFolder = "Assets/Scenes/FlowerSeaVolumeFx";
    private const string FpsControllerPrefabPath = "Assets/Standard Assets/Characters/FirstPersonCharacter/Prefabs/FPSController.prefab";
    private const string RigName = "FlowerSea Volume FX Rig";
    private const string VolumeName = "FlowerSea Volume FX Global Volume";
    private const string AutoRunMarker = "Library/FlowerSeaVolumeFxBuilder.v3.once";
    private static bool autoRunQueued;

    [InitializeOnLoadMethod]
    private static void AutoRunOnce()
    {
        if (Application.isBatchMode || File.Exists(AutoRunMarker) || autoRunQueued)
        {
            return;
        }

        autoRunQueued = true;
        EditorApplication.update += RunWhenReady;
    }

    private static void RunWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }
            return;
        }

        EditorApplication.update -= RunWhenReady;
        try
        {
            Regenerate();
            File.WriteAllText(AutoRunMarker, "FlowerSea volume FX generated.\n");
            Debug.Log("FlowerSea Volume FX generated. Use Tools/FlowerSea/Regenerate Volume FX to rebuild it.");
        }
        catch (System.Exception exception)
        {
            autoRunQueued = false;
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/FlowerSea/Regenerate Volume FX")]
    public static void Regenerate()
    {
        Directory.CreateDirectory(AssetFolder);
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        DeleteIfExists(RigName);
        DeleteIfExists(VolumeName);

        var bounds = CalculateSceneBounds();
        DisableLegacyLighting();
        EnsureWalkablePlayer(bounds);
        ConfigureAtmosphere();
        ConfigureSun(bounds);
        ConfigureVolumeProfile();

        var rig = new GameObject(RigName);
        CreateWindowVolumeLights(rig.transform, bounds);
        CreateLowFloorFog(rig.transform, bounds);
        CreateFarHaze(rig.transform, bounds);

        Selection.activeGameObject = rig;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/FlowerSea/Clear Volume FX")]
    public static void Clear()
    {
        DeleteIfExists(RigName);
        DeleteIfExists(VolumeName);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    private static void DeleteIfExists(string objectName)
    {
        var existing = GameObject.Find(objectName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }
    }

    private static void DisableLegacyLighting()
    {
        SetActiveIfExists("Subway Flower Sea Lighting Rig", false);
        SetActiveIfExists("Subway Flower Sea Global Volume", false);
    }

    private static void SetActiveIfExists(string objectName, bool active)
    {
        var existing = GameObject.Find(objectName);
        if (existing != null)
        {
            existing.SetActive(active);
        }
    }

    private static void EnsureWalkablePlayer(Bounds bounds)
    {
        var existingController = GameObject.Find("FPSController");
        if (existingController != null)
        {
            existingController.SetActive(true);
            PlaceControllerAtReferenceCamera(existingController, bounds);
            ConfigurePlayerCamera(existingController);
            DisableStaticMainCamera(existingController);
            return;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FpsControllerPrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"FlowerSea Volume FX: FPSController prefab not found at {FpsControllerPrefabPath}.");
            return;
        }

        var position = bounds.center + new Vector3(-bounds.size.x * 0.24f, 0.95f, -bounds.size.z * 0.08f);
        var rotation = Quaternion.Euler(0f, 84f, 0f);
        var referenceCamera = GetStaticReferenceCamera() ?? Camera.main ?? Object.FindObjectOfType<Camera>(true);
        if (referenceCamera != null)
        {
            position = referenceCamera.transform.position - Vector3.up * 0.8f;
            rotation = Quaternion.Euler(0f, referenceCamera.transform.eulerAngles.y, 0f);
        }

        var controller = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (controller == null)
        {
            controller = Object.Instantiate(prefab);
            PrefabUtility.UnpackPrefabInstance(controller, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }

        controller.name = "FPSController";
        controller.transform.position = position;
        controller.transform.rotation = rotation;
        ConfigurePlayerCamera(controller);
        DisableStaticMainCamera(controller);
    }

    private static void PlaceControllerAtReferenceCamera(GameObject controller, Bounds bounds)
    {
        var referenceCamera = GetStaticReferenceCamera();
        if (referenceCamera == null)
        {
            return;
        }

        controller.transform.position = referenceCamera.transform.position - Vector3.up * 0.8f;
        controller.transform.rotation = Quaternion.Euler(0f, referenceCamera.transform.eulerAngles.y, 0f);
    }

    private static Camera GetStaticReferenceCamera()
    {
        var staticCameraObject = GameObject.Find("Main Camera");
        return staticCameraObject != null ? staticCameraObject.GetComponent<Camera>() : null;
    }

    private static void ConfigurePlayerCamera(GameObject controller)
    {
        var controllerCamera = controller.GetComponentInChildren<Camera>(true);
        if (controllerCamera == null)
        {
            return;
        }

        controllerCamera.gameObject.SetActive(true);
        controllerCamera.tag = "MainCamera";
        controllerCamera.enabled = true;
        controllerCamera.fieldOfView = 62f;

        var listener = controllerCamera.GetComponent<AudioListener>();
        if (listener == null)
        {
            listener = controllerCamera.gameObject.AddComponent<AudioListener>();
        }
        listener.enabled = true;
    }

    private static void DisableStaticMainCamera(GameObject activeController)
    {
        foreach (var camera in Object.FindObjectsOfType<Camera>(true))
        {
            if (camera.transform.IsChildOf(activeController.transform))
            {
                continue;
            }

            if (camera.gameObject.name == "Main Camera")
            {
                camera.enabled = false;
                camera.tag = "Untagged";
                var listener = camera.GetComponent<AudioListener>();
                if (listener != null)
                {
                    listener.enabled = false;
                }
            }
        }
    }

    private static Bounds CalculateSceneBounds()
    {
        var renderers = Object.FindObjectsOfType<Renderer>(true);
        var hasBounds = false;
        var bounds = new Bounds(Vector3.zero, Vector3.one);

        foreach (var renderer in renderers)
        {
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            var name = renderer.gameObject.name;
            if (name.Contains("FlowerSea Volume FX") || name.Contains("Subway Flower Sea Lighting Rig"))
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            bounds = new Bounds(new Vector3(13f, 1.5f, 1.5f), new Vector3(36f, 4.2f, 6.5f));
        }

        bounds.Expand(new Vector3(-bounds.size.x * 0.12f, -bounds.size.y * 0.18f, -bounds.size.z * 0.18f));
        return bounds;
    }

    private static void ConfigureAtmosphere()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.0068f;
        RenderSettings.fogColor = new Color(0.72f, 0.71f, 0.69f, 1f);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.76f, 0.75f, 0.72f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.48f, 0.49f, 0.49f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.25f, 0.26f, 0.27f, 1f);
        RenderSettings.ambientIntensity = 0.64f;
    }

    private static void ConfigureSun(Bounds bounds)
    {
        var lightObject = GameObject.Find("Directional Light");
        if (lightObject == null)
        {
            lightObject = new GameObject("Directional Light");
            lightObject.AddComponent<Light>();
        }

        lightObject.transform.position = bounds.center + new Vector3(-14f, 12f, -8f);
        lightObject.transform.rotation = Quaternion.Euler(28f, -56f, 4f);

        var light = lightObject.GetComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.86f, 0.70f, 1f);
        light.intensity = 0.82f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.34f;
        light.shadowBias = 0.035f;
        light.shadowNormalBias = 0.32f;
        RenderSettings.sun = light;
    }

    private static void ConfigureVolumeProfile()
    {
        var profile = GetOrCreateAsset<VolumeProfile>($"{AssetFolder}/FlowerSeaVolumeFX_Profile.asset");
        profile.components.Clear();

        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.overrideState = true;
        bloom.intensity.value = 0.48f;
        bloom.threshold.overrideState = true;
        bloom.threshold.value = 1.02f;
        bloom.scatter.overrideState = true;
        bloom.scatter.value = 0.72f;
        bloom.tint.overrideState = true;
        bloom.tint.value = new Color(1f, 0.91f, 0.82f, 1f);

        var color = profile.Add<ColorAdjustments>(true);
        color.postExposure.overrideState = true;
        color.postExposure.value = -0.42f;
        color.contrast.overrideState = true;
        color.contrast.value = 2f;
        color.saturation.overrideState = true;
        color.saturation.value = -8f;
        color.colorFilter.overrideState = true;
        color.colorFilter.value = new Color(1f, 0.95f, 0.90f, 1f);

        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.overrideState = true;
        vignette.intensity.value = 0.09f;
        vignette.smoothness.overrideState = true;
        vignette.smoothness.value = 0.55f;

        var volumeObject = new GameObject(VolumeName);
        var volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 120f;
        volume.weight = 1f;
        volume.sharedProfile = profile;
    }

    private static void CreateWindowVolumeLights(Transform parent, Bounds bounds)
    {
        var beamMaterial = CreateMistMaterial(
            "FlowerSea_WarmWhiteBeam.mat",
            new Color(0.90f, 0.88f, 0.84f, 0.48f),
            0.42f,
            1.55f,
            0.30f);
        var coreMaterial = CreateMistMaterial(
            "FlowerSea_SoftBeamCore.mat",
            new Color(0.98f, 0.93f, 0.84f, 0.52f),
            0.32f,
            1.35f,
            0.24f);
        var haloMaterial = CreateMistMaterial(
            "FlowerSea_WindowHalo.mat",
            new Color(0.94f, 0.91f, 0.86f, 0.44f),
            0.28f,
            1.3f,
            0.25f);

        var minX = bounds.min.x + bounds.size.x * 0.12f;
        var maxX = bounds.max.x - bounds.size.x * 0.12f;
        var leftZ = bounds.min.z + bounds.size.z * 0.08f;
        var rightZ = bounds.max.z - bounds.size.z * 0.08f;
        var centerZ = bounds.center.z;
        var floorY = bounds.min.y + 0.22f;
        var windowY = bounds.min.y + bounds.size.y * 0.62f;
        var points = new[] { 0.16f, 0.34f, 0.53f, 0.72f, 0.88f };

        for (var i = 0; i < points.Length; i++)
        {
            var x = Mathf.Lerp(minX, maxX, points[i]);
            var leftStart = new Vector3(x, windowY, leftZ);
            var leftEnd = new Vector3(x + bounds.size.x * 0.11f, floorY + 0.34f, centerZ);
            var rightStart = new Vector3(x, windowY, rightZ);
            var rightEnd = new Vector3(x - bounds.size.x * 0.08f, floorY + 0.30f, centerZ);

            CreateSpot(parent, $"Volume FX soft window light L {i + 1}", leftStart, leftEnd, 1.35f);
            CreateSpot(parent, $"Volume FX soft window light R {i + 1}", rightStart, rightEnd, 1.05f);
            CreateBeam(parent, $"Volume FX visible light shaft L {i + 1}", leftStart, leftEnd, beamMaterial, 0.18f, 1.35f);
            CreateBeam(parent, $"Volume FX visible light shaft R {i + 1}", rightStart, rightEnd, beamMaterial, 0.16f, 1.05f);
            CreateBeam(parent, $"Volume FX inner glow shaft L {i + 1}", leftStart, Vector3.Lerp(leftStart, leftEnd, 0.72f), coreMaterial, 0.10f, 0.62f);
            CreateHalo(parent, $"Volume FX window bloom halo L {i + 1}", leftStart, haloMaterial, new Vector3(1.35f, 0.62f, 0.38f));
            CreateHalo(parent, $"Volume FX window bloom halo R {i + 1}", rightStart, haloMaterial, new Vector3(1.20f, 0.54f, 0.34f));
        }
    }

    private static void CreateLowFloorFog(Transform parent, Bounds bounds)
    {
        var fogMaterial = CreateMistMaterial(
            "FlowerSea_LowVolumetricFog.mat",
            new Color(0.74f, 0.74f, 0.72f, 0.48f),
            0.40f,
            2.7f,
            0.46f);
        var mesh = GetOrCreateEllipsoidMesh();
        var length = bounds.size.x * 0.78f;
        var width = bounds.size.z * 0.64f;
        var y = bounds.min.y + 0.28f;

        var offsets = new[]
        {
            new Vector3(-length * 0.30f, 0.00f, -width * 0.08f),
            new Vector3(-length * 0.08f, 0.05f, width * 0.12f),
            new Vector3(length * 0.16f, 0.02f, -width * 0.04f),
            new Vector3(length * 0.34f, 0.07f, width * 0.08f)
        };

        for (var i = 0; i < offsets.Length; i++)
        {
            var go = new GameObject($"Volume FX low floor mist {i + 1}");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(bounds.center.x, y, bounds.center.z) + offsets[i];
            go.transform.rotation = Quaternion.Euler(0f, i % 2 == 0 ? -8f : 7f, 0f);
            go.transform.localScale = new Vector3(length * (0.36f + i * 0.035f), 0.48f + i * 0.04f, width * (0.42f + i * 0.03f));
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = fogMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private static void CreateFarHaze(Transform parent, Bounds bounds)
    {
        var hazeMaterial = CreateMistMaterial(
            "FlowerSea_DepthHaze.mat",
            new Color(0.76f, 0.76f, 0.74f, 0.36f),
            0.26f,
            2.9f,
            0.45f);
        var mesh = GetOrCreateEllipsoidMesh();
        var go = new GameObject("Volume FX soft depth haze");
        go.transform.SetParent(parent, false);
        go.transform.position = bounds.center + new Vector3(bounds.size.x * 0.24f, bounds.min.y + bounds.size.y * 0.28f, 0f);
        go.transform.localScale = new Vector3(bounds.size.x * 0.38f, bounds.size.y * 0.38f, bounds.size.z * 0.62f);
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = hazeMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static void CreateSpot(Transform parent, string name, Vector3 position, Vector3 target, float intensity)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.rotation = Quaternion.LookRotation(target - position, Vector3.up);

        var light = go.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = new Color(1f, 0.91f, 0.78f, 1f);
        light.intensity = intensity;
        light.range = 10f;
        light.spotAngle = 38f;
        light.innerSpotAngle = 15f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.22f;
        light.bounceIntensity = 0.70f;
    }

    private static void CreateBeam(Transform parent, string name, Vector3 start, Vector3 end, Material material, float startRadius, float endRadius)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = CreateBeamMesh(name, start, end, startRadius, endRadius);
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static Mesh CreateBeamMesh(string name, Vector3 start, Vector3 end, float startRadius, float endRadius)
    {
        var path = $"{AssetFolder}/{Sanitize(name)}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);

        const int segments = 28;
        var direction = (end - start).normalized;
        var side = Vector3.Cross(direction, Vector3.up);
        if (side.sqrMagnitude < 0.001f)
        {
            side = Vector3.right;
        }
        side.Normalize();
        var up = Vector3.Cross(side, direction).normalized;

        var vertices = new Vector3[segments * 2];
        var normals = new Vector3[segments * 2];
        var colors = new Color[segments * 2];
        var triangles = new int[segments * 6];

        for (var i = 0; i < segments; i++)
        {
            var angle = i / (float)segments * Mathf.PI * 2f;
            var ring = Mathf.Cos(angle) * side + Mathf.Sin(angle) * up * 0.72f;
            vertices[i] = start + ring * startRadius;
            vertices[i + segments] = end + ring * endRadius;
            normals[i] = ring.normalized;
            normals[i + segments] = ring.normalized;
            colors[i] = new Color(1f, 1f, 1f, 0.42f);
            colors[i + segments] = new Color(1f, 1f, 1f, 0.04f);

            var next = (i + 1) % segments;
            var t = i * 6;
            triangles[t] = i;
            triangles[t + 1] = next;
            triangles[t + 2] = i + segments;
            triangles[t + 3] = next;
            triangles[t + 4] = next + segments;
            triangles[t + 5] = i + segments;
        }

        return SaveMesh(path, name, vertices, normals, colors, triangles, existing);
    }

    private static void CreateHalo(Transform parent, string name, Vector3 position, Material material, Vector3 scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = scale;
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = GetOrCreateEllipsoidMesh();
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static Mesh GetOrCreateEllipsoidMesh()
    {
        var path = $"{AssetFolder}/FlowerSea_SoftEllipsoidVolume.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            return existing;
        }

        const int lon = 40;
        const int lat = 16;
        var vertices = new Vector3[(lat + 1) * (lon + 1)];
        var normals = new Vector3[vertices.Length];
        var colors = new Color[vertices.Length];
        var triangles = new int[lat * lon * 6];
        var vertex = 0;

        for (var y = 0; y <= lat; y++)
        {
            var fy = y / (float)lat;
            var theta = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, fy);
            var verticalAlpha = Mathf.Sin(fy * Mathf.PI);
            for (var x = 0; x <= lon; x++)
            {
                var fx = x / (float)lon;
                var phi = fx * Mathf.PI * 2f;
                var normal = new Vector3(
                    Mathf.Cos(theta) * Mathf.Cos(phi),
                    Mathf.Sin(theta),
                    Mathf.Cos(theta) * Mathf.Sin(phi));
                vertices[vertex] = normal * 0.5f;
                normals[vertex] = normal;
                colors[vertex] = new Color(1f, 1f, 1f, verticalAlpha * 0.52f);
                vertex++;
            }
        }

        var tri = 0;
        for (var y = 0; y < lat; y++)
        {
            for (var x = 0; x < lon; x++)
            {
                var a = y * (lon + 1) + x;
                var b = a + 1;
                var c = a + lon + 1;
                var d = c + 1;
                triangles[tri++] = a;
                triangles[tri++] = c;
                triangles[tri++] = b;
                triangles[tri++] = b;
                triangles[tri++] = c;
                triangles[tri++] = d;
            }
        }

        return SaveMesh(path, "FlowerSea_SoftEllipsoidVolume", vertices, normals, colors, triangles, existing);
    }

    private static Mesh SaveMesh(string path, string name, Vector3[] vertices, Vector3[] normals, Color[] colors, int[] triangles, Mesh existing = null)
    {
        var assetName = Path.GetFileNameWithoutExtension(path);
        var mesh = new Mesh
        {
            name = assetName,
            vertices = vertices,
            normals = normals,
            colors = colors,
            triangles = triangles
        };
        mesh.RecalculateBounds();
        if (existing != null)
        {
            EditorUtility.CopySerialized(mesh, existing);
            AssetDatabase.SaveAssets();
            return existing;
        }

        AssetDatabase.CreateAsset(mesh, path);
        return AssetDatabase.LoadAssetAtPath<Mesh>(path);
    }

    private static Material CreateMistMaterial(string fileName, Color color, float alpha, float edgeSoftness, float noiseStrength)
    {
        var path = $"{AssetFolder}/{fileName}";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("FlowerSea/Volume Mist");
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.SetColor("_Color", color);
        material.SetFloat("_Alpha", alpha);
        material.SetFloat("_EdgeSoftness", edgeSoftness);
        material.SetFloat("_NoiseStrength", noiseStrength);
        material.SetFloat("_NoiseScale", 2.7f);
        material.SetFloat("_VerticalFade", 0.8f);
        return material;
    }

    private static T GetOrCreateAsset<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
        {
            return asset;
        }

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static string Sanitize(string name)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return name.Replace(' ', '_');
    }
}
