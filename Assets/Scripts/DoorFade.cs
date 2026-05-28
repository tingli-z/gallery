using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class DoorFade : MonoBehaviour
{
    [Header("淡出持续时间")]
    public float fadeDuration = 2f;

    [Header("是否在开始时记录原始颜色")]
    public bool recordOnStart = true;

    private List<Renderer> childRenderers = new List<Renderer>();
    private List<Color> originalColors = new List<Color>();
    private Coroutine fadeCoroutine;

    void Start()
    {
        // 获取所有子物体的 Renderer（MeshRenderer 和 SkinnedMeshRenderer）
        GetComponentsInChildren<Renderer>(true, childRenderers);

        if (recordOnStart)
        {
            originalColors.Clear();
            foreach (var r in childRenderers)
            {
                // 每个 Renderer 可能有多个材质
                Material[] mats = r.materials;
                foreach (var mat in mats)
                    originalColors.Add(mat.color);
            }
        }
    }

    /// <summary>
    /// 开始淡出，透明度从当前值渐变到0
    /// </summary>
    public void FadeOut()
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeOutRoutine());
        foreach (var col in GetComponentsInChildren<Collider>())
            col.enabled = false;
    }

    /// <summary>
    /// 立刻完全消失（Alpha=0）
    /// </summary>
    public void DisappearImmediately()
    {
        SetAllAlpha(0f);
    }

    private IEnumerator FadeOutRoutine()
    {
        float timer = 0f;
        // 记录开始时的透明度（假设所有材质 alpha 相同，取第一个材质的 alpha）
        float startAlpha = 1f;
        if (childRenderers.Count > 0 && childRenderers[0].materials.Length > 0)
            startAlpha = childRenderers[0].materials[0].color.a;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, 0f, timer / fadeDuration);
            SetAllAlpha(alpha);
            yield return null;
        }
        SetAllAlpha(0f);
    }

    private void SetAllAlpha(float alpha)
    {
        foreach (var r in childRenderers)
        {
            // 为每个材质创建独立实例（避免污染共享材质）
            Material[] mats = r.materials;
            foreach (var mat in mats)
            {
                Color c = mat.color;
                c.a = alpha;
                mat.color = c;
            }
        }
    }

    // 可选：如果之后需要恢复原样，可以再加 FadeIn 方法，利用 originalColors 数据
}