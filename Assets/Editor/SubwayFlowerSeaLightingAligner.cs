using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class SubwayFlowerSeaLightingAligner
{
    private const string RigName = "Subway Flower Sea Lighting Rig";
    private const string VolumeName = "Subway Flower Sea Global Volume";
    private const string AssetFolder = "Assets/Scenes/SubwayFlowerSeaLighting";
    private const string SunPatchMaterialPath = AssetFolder + "/Subway_SunPatch.mat";
    private const string CoreBeamMaterialPath = AssetFolder + "/Subway_CoreWindowBeam.mat";
    private const string SoftBeamMaterialPath = AssetFolder + "/Subway_SubtleWindowBeam.mat";
    private const string HaloMaterialPath = AssetFolder + "/Subway_SubtleWindowHalo.mat";
    private const string BottomFogMaterialPath = AssetFolder + "/Subway_BottomVolumetricFog.mat";
    private const string DirectShaftMaterialPath = AssetFolder + "/Subway_DirectWindowShaft.mat";
    private const string PhotoWindowGlowMaterialPath = AssetFolder + "/Subway_PhotoWindowGlow.mat";
    private const string PhotoShadowPatchMaterialPath = AssetFolder + "/Subway_PhotoSoftShadow.mat";
    private const string ProfilePath = AssetFolder + "/SubwayFlowerSeaVolumeProfile.asset";

    [MenuItem("Tools/Dreamcore/Clear Subway Flower Sea Lighting")]
    public static void ClearLighting()
    {
        DeleteIfExists(RigName);
        DeleteIfExists(VolumeName);
        ConfigureCameraPostProcessing(false);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Subway Flower Sea generated lighting cleared.");
    }

    [MenuItem("Tools/Dreamcore/Regenerate Subway Flower Sea Lighting")]
    public static void RegenerateLighting()
    {
        Directory.CreateDirectory(AssetFolder);
        ClearLighting();

        var camera = Camera.main ?? Object.FindObjectOfType<Camera>();
        if (camera == null)
        {
            Debug.LogError("Subway lighting: no camera found.");
            return;
        }

        ConfigureEnvironment();
        ConfigureDirectionalLight();
        BindTODControllerMainLight();
        ConfigureCameraPostProcessing(true);
        ConfigurePostProcessing();
        ApplyReferenceMaterialTone();

        var rig = new GameObject(RigName);
        CreateWindowSunlight(rig.transform, camera);
        CreateSubtleWindowBeams(rig.transform, camera);
        CreateDirectWindowShafts(rig.transform, camera);
        CreatePhotoReferenceWindowGlow(rig.transform, camera);
        CreateBottomVolumetricFog(rig.transform, camera);
        CreateSunPatches(rig.transform, camera);
        CreateInteriorLights(rig.transform, camera);
        CreateFlowerBounce(rig.transform, camera);

        Selection.activeGameObject = rig;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Subway Flower Sea lighting regenerated in reference-image style: lower exposure, warm window light, visible sun patches.");
    }

    private static void DeleteIfExists(string objectName)
    {
        var existing = GameObject.Find(objectName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }
    }

    private static void ConfigureEnvironment()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.46f, 0.48f, 0.50f, 1f);
        RenderSettings.fogDensity = 0.00036f;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.44f, 0.45f, 0.46f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.31f, 0.32f, 0.33f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.10f, 0.10f, 0.11f, 1f);
        RenderSettings.ambientIntensity = 0.19f;
        RenderSettings.reflectionIntensity = 0.16f;
        RenderSettings.reflectionBounces = 1;
    }

    private static void ConfigureDirectionalLight()
    {
        var lightObject = GameObject.Find("Directional Light") ?? new GameObject("Directional Light");
        var light = lightObject.GetComponent<Light>() ?? lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.92f, 0.97f, 1f, 1f);
        light.intensity = 0.20f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.50f;
        light.bounceIntensity = 0.45f;
        lightObject.transform.SetPositionAndRotation(lightObject.transform.position, Quaternion.Euler(30f, -55f, 0f));
        RenderSettings.sun = light;
    }

    private static void BindTODControllerMainLight()
    {
        var mainLightObject = GameObject.Find("Directional Light");
        var mainLight = mainLightObject != null ? mainLightObject.GetComponent<Light>() : RenderSettings.sun;
        if (mainLight == null)
        {
            return;
        }

        foreach (var behaviour in Object.FindObjectsOfType<MonoBehaviour>(true))
        {
            if (behaviour == null || behaviour.GetType().Name != "TODController")
            {
                continue;
            }

            var serializedObject = new SerializedObject(behaviour);
            var mainLightProperty = serializedObject.FindProperty("MainLight");
            if (mainLightProperty == null || mainLightProperty.propertyType != SerializedPropertyType.ObjectReference)
            {
                continue;
            }

            mainLightProperty.objectReferenceValue = mainLight;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(behaviour);
        }
    }

    private static void ConfigureCameraPostProcessing(bool enabled)
    {
        foreach (var cameraData in Object.FindObjectsOfType<UniversalAdditionalCameraData>())
        {
            cameraData.renderPostProcessing = enabled;
            if (enabled)
            {
                cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            }
            EditorUtility.SetDirty(cameraData);
        }
    }

    private static void ConfigurePostProcessing()
    {
        var volumeObject = new GameObject(VolumeName);
        var volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.weight = 1f;

        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }
        volume.sharedProfile = profile;

        if (!profile.TryGet(out Bloom bloom))
        {
            bloom = profile.Add<Bloom>(true);
        }
        bloom.threshold.Override(0.58f);
        bloom.intensity.Override(0.58f);
        bloom.scatter.Override(0.70f);
        bloom.tint.Override(new Color(0.90f, 0.96f, 1f, 1f));

        if (!profile.TryGet(out Tonemapping tonemapping))
        {
            tonemapping = profile.Add<Tonemapping>(true);
        }
        tonemapping.mode.Override(TonemappingMode.ACES);

        if (!profile.TryGet(out ColorAdjustments color))
        {
            color = profile.Add<ColorAdjustments>(true);
        }
        color.postExposure.Override(-1.35f);
        color.contrast.Override(14f);
        color.saturation.Override(-8f);
        color.colorFilter.Override(new Color(0.94f, 0.98f, 1f, 1f));

        if (!profile.TryGet(out Vignette vignette))
        {
            vignette = profile.Add<Vignette>(true);
        }
        vignette.intensity.Override(0.12f);
        vignette.smoothness.Override(0.55f);

        EditorUtility.SetDirty(profile);
    }

    private static void ApplyReferenceMaterialTone()
    {
        var root = GameObject.Find("\u5730\u94c1\u82b1\u6d77");
        if (root == null)
        {
            return;
        }

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            var sourceMaterials = renderer.sharedMaterials;
            var tonedMaterials = new Material[sourceMaterials.Length];
            var changed = false;

            for (var i = 0; i < sourceMaterials.Length; i++)
            {
                var source = sourceMaterials[i];
                if (source == null)
                {
                    tonedMaterials[i] = null;
                    continue;
                }

                tonedMaterials[i] = GetOrCreateTonedMaterial(source);
                changed = changed || tonedMaterials[i] != source;
            }

            if (changed)
            {
                renderer.sharedMaterials = tonedMaterials;
                EditorUtility.SetDirty(renderer);
            }
        }
    }

    private static Material GetOrCreateTonedMaterial(Material source)
    {
        var sourcePath = AssetDatabase.GetAssetPath(source);
        if (string.IsNullOrEmpty(sourcePath))
        {
            return source;
        }

        var guid = AssetDatabase.AssetPathToGUID(sourcePath);
        var fileName = "SubwayTone_" + guid + "_" + SanitizeFileName(source.name) + ".mat";
        var tonedPath = AssetFolder + "/" + fileName;
        var toned = AssetDatabase.LoadAssetAtPath<Material>(tonedPath);
        if (toned == null)
        {
            toned = new Material(source);
            AssetDatabase.CreateAsset(toned, tonedPath);
        }

        var litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader != null)
        {
            toned.shader = litShader;
        }
        else
        {
            toned.shader = source.shader;
        }

        CopyTextureIfPresent(source, toned, "_BaseMap", "_BaseMap");
        CopyTextureIfPresent(source, toned, "_MainTex", "_BaseMap");
        CopyTextureIfPresent(source, toned, "_BumpMap", "_BumpMap");
        ToneColorProperty(toned, "_BaseColor");
        ToneColorProperty(toned, "_Color");

        if (toned.HasProperty("_EmissionColor"))
        {
            toned.SetColor("_EmissionColor", Color.black);
            toned.DisableKeyword("_EMISSION");
        }

        if (toned.HasProperty("_Surface"))
        {
            toned.SetFloat("_Surface", 0f);
        }

        if (toned.HasProperty("_AlphaClip"))
        {
            toned.SetFloat("_AlphaClip", 0f);
        }

        if (toned.HasProperty("_Smoothness"))
        {
            toned.SetFloat("_Smoothness", Mathf.Min(toned.GetFloat("_Smoothness"), 0.28f));
        }

        if (toned.HasProperty("_Metallic"))
        {
            toned.SetFloat("_Metallic", Mathf.Min(toned.GetFloat("_Metallic"), 0.18f));
        }

        EditorUtility.SetDirty(toned);
        return toned;
    }

    private static void ToneColorProperty(Material material, string propertyName)
    {
        if (!material.HasProperty(propertyName))
        {
            return;
        }

        var color = material.GetColor(propertyName);
        var brightness = Mathf.Max(color.r, color.g, color.b);
        var multiplier = brightness > 0.72f ? 0.36f : 0.74f;
        var warm = new Color(0.88f, 0.94f, 1.00f, color.a);
        color = new Color(color.r * warm.r * multiplier, color.g * warm.g * multiplier, color.b * warm.b * multiplier, color.a);
        material.SetColor(propertyName, color);
    }

    private static void CopyTextureIfPresent(Material source, Material target, string sourceProperty, string targetProperty)
    {
        if (!source.HasProperty(sourceProperty) || !target.HasProperty(targetProperty))
        {
            return;
        }

        var texture = source.GetTexture(sourceProperty);
        if (texture == null)
        {
            return;
        }

        target.SetTexture(targetProperty, texture);
        target.SetTextureScale(targetProperty, source.GetTextureScale(sourceProperty));
        target.SetTextureOffset(targetProperty, source.GetTextureOffset(sourceProperty));
    }

    private static string SanitizeFileName(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(c, '_');
        }
        return value;
    }

    private static void CreateWindowSunlight(Transform parent, Camera camera)
    {
        var t = camera.transform;
        for (var i = 0; i < 5; i++)
        {
            var depth = 7.0f + i * 4.6f;
            var start = t.position + t.forward * depth + t.right * 4.2f + t.up * 1.35f;
            var target = t.position + t.forward * (depth + 2.8f) + t.right * 0.15f + t.up * -1.05f;

            var spot = CreateLight("Warm sunlight through window " + (i + 1), parent, LightType.Spot, start);
            spot.transform.rotation = Quaternion.LookRotation((target - start).normalized, t.up);
            spot.color = new Color(0.90f, 0.96f, 1f, 1f);
            spot.intensity = 0.18f;
            spot.range = 8.5f;
            spot.spotAngle = 20f;
            spot.innerSpotAngle = 10f;
            spot.shadows = LightShadows.Soft;
            spot.shadowStrength = 0.42f;
        }
    }

    private static void CreateSubtleWindowBeams(Transform parent, Camera camera)
    {
        var coreBeamMaterial = GetOrCreateCoreBeamMaterial();
        var softBeamMaterial = GetOrCreateSoftBeamMaterial();
        var haloMaterial = GetOrCreateHaloMaterial();
        var t = camera.transform;

        for (var i = 0; i < 5; i++)
        {
            var depth = 7.0f + i * 4.6f;
            var start = t.position + t.forward * depth + t.right * 4.05f + t.up * 1.25f;
            var end = t.position + t.forward * (depth + 2.8f) + t.right * 0.20f + t.up * -1.05f;
            var seatEnd = t.position + t.forward * (depth + 2.2f) + t.right * 1.35f + t.up * -0.25f;
            var width = 0.70f + i * 0.08f;

            CreateBeam(parent, "Clear window light ray core " + (i + 1), start, end, width * 0.14f, width * 0.55f, coreBeamMaterial, t.up);
            CreateBeam(parent, "Soft visible window light cone " + (i + 1), start + t.up * 0.05f, end + t.up * 0.18f, width * 0.42f, width * 1.55f, softBeamMaterial, t.up);
            CreateBeam(parent, "Seat grazing cool window ray " + (i + 1), start + t.forward * 0.2f, seatEnd, width * 0.10f, width * 0.65f, coreBeamMaterial, t.up);
            CreateCameraFacingPatch(parent, "Cool window volumetric veil " + (i + 1), Vector3.Lerp(start, end, 0.48f) + t.up * 0.18f, Quaternion.AngleAxis(-18f, -t.forward), new Vector3(width * 0.85f, 2.65f, 1f), softBeamMaterial, camera);
            CreatePatch(parent, "Bright window emitting pane " + (i + 1), start + t.right * 0.02f, Quaternion.LookRotation(-t.right, t.up), new Vector3(width * 1.10f, width * 1.55f, 1f), haloMaterial);
            CreatePatch(parent, "Window glow bloom halo " + (i + 1), start - t.right * 0.03f, Quaternion.LookRotation(-t.right, t.up), new Vector3(width * 1.75f, width * 2.35f, 1f), haloMaterial);

            var rim = CreateLight("Window rim glow support " + (i + 1), parent, LightType.Point, start - t.right * 0.08f);
            rim.color = new Color(0.78f, 0.92f, 1f, 1f);
            rim.intensity = 0.035f;
            rim.range = 1.8f;
            rim.shadows = LightShadows.None;
        }
    }

    private static void CreateSunPatches(Transform parent, Camera camera)
    {
        var material = GetOrCreateSunPatchMaterial();
        var t = camera.transform;

        for (var i = 0; i < 6; i++)
        {
            var depth = 6.5f + i * 3.7f;
            var floorPos = t.position + t.forward * depth + t.right * Mathf.Lerp(0.7f, -1.0f, i / 5f) + t.up * -1.35f;
            CreatePatch(parent, "Cool bright window floor patch " + (i + 1), floorPos, Quaternion.LookRotation(t.up, t.forward), new Vector3(1.35f, 0.38f, 1f), material);

            var seatPos = t.position + t.forward * (depth + 0.65f) + t.right * 1.75f + t.up * -0.33f;
            CreatePatch(parent, "Cool window light on seat edge " + (i + 1), seatPos, Quaternion.LookRotation(t.up, t.forward), new Vector3(0.95f, 0.20f, 1f), material);
        }

        for (var i = 0; i < 5; i++)
        {
            var depth = 7.5f + i * 4.4f;
            var wallPos = t.position + t.forward * depth + t.right * 3.45f + t.up * 0.15f;
            CreatePatch(parent, "Cool window wall glow patch " + (i + 1), wallPos, Quaternion.LookRotation(-t.right, t.up), new Vector3(0.58f, 1.04f, 1f), material);
        }
    }

    private static void CreateDirectWindowShafts(Transform parent, Camera camera)
    {
        var material = GetOrCreateDirectShaftMaterial();
        var t = camera.transform;

        for (var i = 0; i < 6; i++)
        {
            var depth = 5.8f + i * 3.6f;
            var top = t.position + t.forward * depth + t.right * 3.05f + t.up * 1.52f;
            var bottom = t.position + t.forward * (depth + 2.05f) + t.right * 0.05f + t.up * -0.88f;
            var center = Vector3.Lerp(top, bottom, 0.52f);

            CreateBeam(parent, "Cool visible direct window ray core " + (i + 1), top, bottom, 0.20f, 1.10f, material, t.forward);
            CreateCameraFacingPatch(parent, "Cool visible window shaft body " + (i + 1), center, Quaternion.AngleAxis(-11f, -t.forward), new Vector3(1.25f + i * 0.03f, 3.75f, 1f), material, camera);

            var readableCenter = t.position + t.forward * (depth + 1.08f) + t.right * 0.62f + t.up * 0.10f;
            CreateCameraFacingPatch(parent, "Readable cool foreground window shaft " + (i + 1), readableCenter, Quaternion.AngleAxis(-12f, -t.forward), new Vector3(1.35f + i * 0.03f, 4.05f, 1f), material, camera);
        }
    }

    private static void CreatePhotoReferenceWindowGlow(Transform parent, Camera camera)
    {
        var glow = GetOrCreatePhotoWindowGlowMaterial();
        var shadow = GetOrCreatePhotoShadowPatchMaterial();
        var t = camera.transform;

        for (var i = 0; i < 6; i++)
        {
            var depth = 5.7f + i * 3.65f;
            var windowPos = t.position + t.forward * depth + t.right * 3.12f + t.up * 0.62f;
            var wallCatch = t.position + t.forward * (depth + 0.72f) + t.right * 2.05f + t.up * 0.40f;

            CreatePatch(parent, "Photo bright window glow rectangle " + (i + 1), windowPos, Quaternion.LookRotation(-t.right, t.up), new Vector3(0.72f, 1.52f, 1f), glow);
            CreatePatch(parent, "Photo soft sun patch on wall " + (i + 1), wallCatch, Quaternion.LookRotation(-t.right, t.up), new Vector3(1.05f, 1.70f, 1f), glow);

            var shadowPos = wallCatch + t.forward * 0.22f + t.up * -0.03f;
            CreatePatch(parent, "Photo soft frame shadow beside light " + (i + 1), shadowPos, Quaternion.LookRotation(-t.right, t.up), new Vector3(0.20f, 1.62f, 1f), shadow);
        }
    }

    private static void CreateBottomVolumetricFog(Transform parent, Camera camera)
    {
        var material = GetOrCreateBottomFogMaterial();
        var t = camera.transform;

        for (var i = 0; i < 8; i++)
        {
            var depth = 4.3f + i * 3.55f;
            var pos = t.position + t.forward * depth + t.right * Mathf.Lerp(-1.35f, 0.85f, i / 7f) + t.up * -0.96f;
            CreateCameraFacingPatch(parent, "Photo low rolling floor fog " + (i + 1), pos, Quaternion.AngleAxis(i % 2 == 0 ? 4f : -5f, -t.forward), new Vector3(3.6f, 0.54f, 1f), material, camera);
        }

        for (var i = 0; i < 4; i++)
        {
            var depth = 7.0f + i * 5.5f;
            var pos = t.position + t.forward * depth + t.right * 0.85f + t.up * -1.18f;
            CreatePatch(parent, "Photo floor haze catching window light " + (i + 1), pos, Quaternion.LookRotation(t.up, t.forward), new Vector3(3.0f, 0.46f, 1f), material);
        }
    }

    private static void CreateInteriorLights(Transform parent, Camera camera)
    {
        var t = camera.transform;
        for (var i = 0; i < 6; i++)
        {
            var pos = t.position + t.forward * (4.0f + i * 4.6f) + t.up * 1.65f;
            var light = CreateLight("Soft fluorescent warm interior " + (i + 1), parent, LightType.Point, pos);
            light.color = new Color(0.78f, 0.88f, 1f, 1f);
            light.intensity = 0.11f;
            light.range = 4.2f;
            light.shadows = LightShadows.None;
        }
    }

    private static void CreateFlowerBounce(Transform parent, Camera camera)
    {
        var t = camera.transform;
        for (var i = 0; i < 5; i++)
        {
            var pos = t.position + t.forward * (5.5f + i * 4.8f) + t.up * -1.05f + t.right * -0.45f;
            var light = CreateLight("Low flower bed warm bounce " + (i + 1), parent, LightType.Point, pos);
            light.color = i % 2 == 0 ? new Color(0.95f, 0.48f, 0.58f, 1f) : new Color(0.92f, 0.66f, 0.35f, 1f);
            light.intensity = 0.07f;
            light.range = 3.6f;
            light.shadows = LightShadows.None;
        }
    }

    private static Light CreateLight(string name, Transform parent, LightType type, Vector3 position)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        var light = go.AddComponent<Light>();
        light.type = type;
        light.renderMode = LightRenderMode.Auto;
        light.bounceIntensity = 0.7f;
        return light;
    }

    private static void CreatePatch(Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.rotation = rotation;
        go.transform.localScale = scale;

        var collider = go.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }

        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static void CreateCameraFacingPatch(Transform parent, string name, Vector3 position, Quaternion extraRotation, Vector3 scale, Material material, Camera camera)
    {
        CreatePatch(parent, name, position, Quaternion.LookRotation(-camera.transform.forward, camera.transform.up) * extraRotation, scale, material);
    }

    private static void CreateBeam(Transform parent, string name, Vector3 start, Vector3 end, float startHalfWidth, float endHalfWidth, Material material, Vector3 upHint)
    {
        var direction = (end - start).normalized;
        var side = Vector3.Cross(upHint, direction);
        if (side.sqrMagnitude < 0.001f)
        {
            side = Vector3.Cross(Vector3.up, direction);
        }
        side.Normalize();

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var mesh = new Mesh { name = name + " Mesh" };
        mesh.vertices = new[]
        {
            start - side * startHalfWidth,
            start + side * startHalfWidth,
            end + side * endHalfWidth,
            end - side * endHalfWidth
        };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.allowOcclusionWhenDynamic = false;
    }

    private static Material GetOrCreateSunPatchMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(SunPatchMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Dreamcore/VolumetricBeam"));
            AssetDatabase.CreateAsset(material, SunPatchMaterialPath);
        }

        material.shader = Shader.Find("Dreamcore/VolumetricBeam");
        material.SetColor("_BaseColor", new Color(0.82f, 0.94f, 1f, 0.22f));
        material.SetFloat("_Softness", 0.85f);
        material.SetFloat("_FadePower", 0.25f);
        material.SetFloat("_Intensity", 1.35f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material GetOrCreateCoreBeamMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(CoreBeamMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Dreamcore/VolumetricBeam"));
            AssetDatabase.CreateAsset(material, CoreBeamMaterialPath);
        }

        material.shader = Shader.Find("Dreamcore/VolumetricBeam");
        material.SetColor("_BaseColor", new Color(0.86f, 0.96f, 1f, 0.42f));
        material.SetFloat("_Softness", 1.10f);
        material.SetFloat("_FadePower", 0.72f);
        material.SetFloat("_Intensity", 2.10f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material GetOrCreateSoftBeamMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(SoftBeamMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Dreamcore/VolumetricBeam"));
            AssetDatabase.CreateAsset(material, SoftBeamMaterialPath);
        }

        material.shader = Shader.Find("Dreamcore/VolumetricBeam");
        material.SetColor("_BaseColor", new Color(0.78f, 0.92f, 1f, 0.30f));
        material.SetFloat("_Softness", 1.35f);
        material.SetFloat("_FadePower", 0.72f);
        material.SetFloat("_Intensity", 1.80f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material GetOrCreateHaloMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(HaloMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Dreamcore/VolumetricBeam"));
            AssetDatabase.CreateAsset(material, HaloMaterialPath);
        }

        material.shader = Shader.Find("Dreamcore/VolumetricBeam");
        material.SetColor("_BaseColor", new Color(0.86f, 0.96f, 1f, 0.34f));
        material.SetFloat("_Softness", 2.10f);
        material.SetFloat("_FadePower", 0.45f);
        material.SetFloat("_Intensity", 1.90f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material GetOrCreateBottomFogMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(BottomFogMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Dreamcore/VolumetricBeam"));
            AssetDatabase.CreateAsset(material, BottomFogMaterialPath);
        }

        material.shader = Shader.Find("Dreamcore/VolumetricBeam");
        material.SetColor("_BaseColor", new Color(0.62f, 0.74f, 0.86f, 0.14f));
        material.SetFloat("_Softness", 3.75f);
        material.SetFloat("_FadePower", 0.30f);
        material.SetFloat("_Intensity", 0.95f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material GetOrCreateDirectShaftMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(DirectShaftMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Dreamcore/VolumetricBeam"));
            AssetDatabase.CreateAsset(material, DirectShaftMaterialPath);
        }

        material.shader = Shader.Find("Dreamcore/VolumetricBeam");
        material.SetColor("_BaseColor", new Color(0.76f, 0.92f, 1f, 0.62f));
        material.SetFloat("_Softness", 0.72f);
        material.SetFloat("_FadePower", 0.30f);
        material.SetFloat("_Intensity", 3.10f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material GetOrCreatePhotoWindowGlowMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(PhotoWindowGlowMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Dreamcore/VolumetricBeam"));
            AssetDatabase.CreateAsset(material, PhotoWindowGlowMaterialPath);
        }

        material.shader = Shader.Find("Dreamcore/VolumetricBeam");
        material.SetColor("_BaseColor", new Color(0.80f, 0.94f, 1f, 0.48f));
        material.SetFloat("_Softness", 0.55f);
        material.SetFloat("_FadePower", 0.18f);
        material.SetFloat("_Intensity", 2.20f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material GetOrCreatePhotoShadowPatchMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(PhotoShadowPatchMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Dreamcore/VolumetricBeam"));
            AssetDatabase.CreateAsset(material, PhotoShadowPatchMaterialPath);
        }

        material.shader = Shader.Find("Dreamcore/VolumetricBeam");
        material.SetColor("_BaseColor", new Color(0.22f, 0.24f, 0.28f, 0.14f));
        material.SetFloat("_Softness", 1.80f);
        material.SetFloat("_FadePower", 0.22f);
        material.SetFloat("_Intensity", 0.42f);
        EditorUtility.SetDirty(material);
        return material;
    }
}
