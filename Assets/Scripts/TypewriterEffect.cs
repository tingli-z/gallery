using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TypewriterEffect : MonoBehaviour
{
    [Header("打字速度")]
    public float charsPerSecond = 0.1f;

    [Header("开始时是否清空文字")]
    public bool clearOnStart = true;

    [Header("要逐字显示的完整文字（在Inspector中直接填写）")]
    [TextArea]
    public string fullTextString;

    private Text textComponent;
    private Coroutine typingRoutine;

    void Awake()
    {
        textComponent = GetComponent<Text>();
        // 如果 fullTextString 为空，则尝试读取 Text 组件中现有的文字作为后备
        if (string.IsNullOrEmpty(fullTextString) && textComponent != null)
            fullTextString = textComponent.text;
    }

    /// <summary>
    /// 开始逐字打印 fullTextString 中的文字
    /// </summary>
    public void StartTyping()
    {
        if (textComponent == null || string.IsNullOrEmpty(fullTextString))
            return;

        StopTyping();
        if (clearOnStart)
            textComponent.text = "";
        typingRoutine = StartCoroutine(TypeRoutine(fullTextString));
    }

    /// <summary>
    /// 立刻显示完整文字（跳过打字动画）
    /// </summary>
    public void SkipToEnd()
    {
        StopTyping();
        if (textComponent != null && !string.IsNullOrEmpty(fullTextString))
            textComponent.text = fullTextString;
    }

    /// <summary>
    /// 停止正在进行的打字动画
    /// </summary>
    public void StopTyping()
    {
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }
    }

    /// <summary>
    /// 动态更换要显示的文字，之后调用 StartTyping 会使用新文字
    /// </summary>
    public void SetNewText(string newText)
    {
        fullTextString = newText;
    }

    private IEnumerator TypeRoutine(string textToType)
    {
        for (int i = 0; i <= textToType.Length; i++)
        {
            textComponent.text = textToType.Substring(0, i);
            yield return new WaitForSeconds(charsPerSecond);
        }
    }
}