using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SceneFader : MonoBehaviour
{
    public static SceneFader Instance; // 全局单例，方便其他 Playmaker FSM 调用

    public Image blackScreen;          // 拖入你的全屏黑图
    public float fadeOutDuration = 2.5f;   // 渐暗时间
    public float fadeInDuration = 2f;    // 渐亮时间

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            // 确保 Canvas 也不被销毁
            if (blackScreen != null)
            {
                DontDestroyOnLoad(blackScreen.canvas.gameObject);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 给 Playmaker 调用：用场景索引
    public void FadeToScene(int sceneIndex)
    {
        StartCoroutine(FadeRoutine(sceneIndex));
    }

    // 给 Playmaker 调用：用场景名
    public void FadeToSceneByName(string sceneName)
    {
        StartCoroutine(FadeRoutine(SceneManager.GetSceneByName(sceneName).buildIndex));
    }

    IEnumerator FadeRoutine(int sceneIndex)
    {
        // 1. 变黑
        yield return StartCoroutine(Fade(1f, fadeOutDuration));

        // 2. 加载场景（此时画面全黑）
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneIndex);
        while (!op.isDone)
            yield return null;

        // 确保场景激活后等一帧，避免引用问题
        yield return null;
        yield return new WaitForEndOfFrame();

        // 重新获取 BlackScreen（防止引用丢失）
        if (blackScreen == null)
        {
            GameObject canvasObj = GameObject.Find("TransitionCanvas");
            if (canvasObj != null)
                blackScreen = canvasObj.transform.Find("BlackScreen")?.GetComponent<Image>();
        }

        // 3. 变亮
        yield return StartCoroutine(Fade(0f, fadeInDuration));
    }

    IEnumerator Fade(float targetAlpha, float duration)
    {
        float startAlpha = blackScreen.color.a;
        float elapsed = 0f;
        Color color = blackScreen.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            blackScreen.color = color;
            yield return null;
        }

        color.a = targetAlpha;
        blackScreen.color = color;
    }
}