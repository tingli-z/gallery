using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CharacterFade : MonoBehaviour
{
    [Header("渐显持续时间")]
    public float fadeInDuration = 1.5f;

    [Header("是否在Start时自动隐藏（Alpha=0）")]
    public bool autoHide = true;

    private List<Renderer> childRenderers = new List<Renderer>();
    private List<Material> managedMaterials = new List<Material>();  // 管理的材质实例
    private List<Color> originalColors = new List<Color>();
    private Coroutine fadeCoroutine;

    void Awake()
    {
        // 获取所有子物体的 Renderer（包含未激活的）
        GetComponentsInChildren<Renderer>(true, childRenderers);

        // 为每个 Renderer 的每个材质创建独立实例，并强制设为透明模式
        foreach (var r in childRenderers)
        {
            Material[] sharedMats = r.sharedMaterials; // 先拿原始材质
            Material[] instanceMats = new Material[sharedMats.Length];
            for (int i = 0; i < sharedMats.Length; i++)
            {
                if (sharedMats[i] == null) continue;
                // 创建材质实例（自动脱离共享材质）
                Material mat = new Material(sharedMats[i]);
                // 强制设为透明模式（URP Lit 适用）
                SetupMaterialForTransparency(mat);
                instanceMats[i] = mat;
                managedMaterials.Add(mat);
                originalColors.Add(mat.color);
            }
            r.materials = instanceMats; // 将实例材质赋给 Renderer
        }

        // 如果需要自动隐藏，立即把 Alpha 设为 0
        if (autoHide)
            SetAllAlpha(0f);
    }

    /// <summary>
    /// 开始渐显
    /// </summary>
    public void FadeIn()
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeInRoutine());
    }

    /// <summary>
    /// 开始渐隐（如果需要离开时消失）
    /// </summary>
    public void FadeOut()
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeOutRoutine());
    }

    /// <summary>
    /// 立刻完全显示
    /// </summary>
    public void ShowImmediately()
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        SetAllAlpha(1f);
    }

    // 将材质强制改为可透明模式（针对 URP Lit）
    private void SetupMaterialForTransparency(Material mat)
    {
        // 设置 Surface Type 为 Transparent
        mat.SetFloat("_Surface", 1f);  // 0 = Opaque, 1 = Transparent
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.DisableKeyword("_ALPHAMODULATE_ON");

        // 设置混合模式
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.SetInt("_Blend", 0);

        // 设置 Render Queue 为透明队列
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        // 确保 Alpha Clip 关闭
        mat.SetFloat("_AlphaClip", 0f);
    }

    private IEnumerator FadeInRoutine()
    {
        float timer = 0f;
        while (timer < fadeInDuration)
        {
            timer += Time.deltaTime;
            float t = timer / fadeInDuration;
            SetAllAlpha(t);
            yield return null;
        }
        SetAllAlpha(1f);
    }

    private IEnumerator FadeOutRoutine()
    {
        float timer = 0f;
        float startAlpha = (managedMaterials.Count > 0) ? managedMaterials[0].color.a : 1f;
        while (timer < fadeInDuration)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, 0f, timer / fadeInDuration);
            SetAllAlpha(alpha);
            yield return null;
        }
        SetAllAlpha(0f);
    }

    private void SetAllAlpha(float alpha)
    {
        for (int i = 0; i < managedMaterials.Count; i++)
        {
            Color c = originalColors[i];
            c.a = alpha;
            managedMaterials[i].color = c;
        }
    }

    void OnDestroy()
    {
        // 销毁运行时创建的材质实例，避免泄漏
        foreach (var mat in managedMaterials)
        {
            if (mat != null)
                Destroy(mat);
        }
    }
}