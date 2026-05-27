using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SubtitleFade : MonoBehaviour
{
    [Header("淡入淡出时间")]
    public float fadeInDuration = 0.5f;
    public float fadeOutDuration = 0.5f;

    private Text textComponent;
    private Color originalColor;
    private Coroutine currentRoutine;

    void Awake()
    {
        textComponent = GetComponent<Text>();
        if (textComponent != null)
            originalColor = textComponent.color;
    }

    // 立刻显示并开始淡入
    public void Show()
    {
        StopCurrentRoutine();
        currentRoutine = StartCoroutine(FadeInRoutine());
    }

    // 立刻开始淡出并最终隐藏
    public void Hide()
    {
        StopCurrentRoutine();
        currentRoutine = StartCoroutine(FadeOutRoutine());
    }

    // 淡入：从当前透明度渐变到1
    private IEnumerator FadeInRoutine()
    {
        float startAlpha = textComponent.color.a;
        float timer = 0f;
        while (timer < fadeInDuration)
        {
            timer += Time.deltaTime;
            SetAlpha(Mathf.Lerp(startAlpha, 1f, timer / fadeInDuration));
            yield return null;
        }
        SetAlpha(1f);
    }

    // 淡出：从当前透明度渐变到0
    private IEnumerator FadeOutRoutine()
    {
        float startAlpha = textComponent.color.a;
        float timer = 0f;
        while (timer < fadeOutDuration)
        {
            timer += Time.deltaTime;
            SetAlpha(Mathf.Lerp(startAlpha, 0f, timer / fadeOutDuration));
            yield return null;
        }
        SetAlpha(0f);
    }

    private void SetAlpha(float alpha)
    {
        Color c = originalColor;
        c.a = alpha;
        textComponent.color = c;
    }

    private void StopCurrentRoutine()
    {
        if (currentRoutine != null)
            StopCoroutine(currentRoutine);
    }
}