using UnityEngine;

public class TrainShake : MonoBehaviour
{
    [Header("抖动强度")]
    public float shakeAmount = 0.03f;

    [Header("抖动持续时间")]
    public float shakeDuration = 0.3f;

    [Header("随机触发间隔")]
    public float minInterval = 2f;
    public float maxInterval = 6f;

    private Vector3 originalLocalPos;   // 记录原始本地位置（眼睛高度偏移）
    private bool isShaking = false;
    private float shakeTimer = 0f;
    private float nextShakeTime;

    void Start()
    {
        // 记录初始的 localPosition（例如 (0, 0.8, 0)）
        originalLocalPos = transform.localPosition;
        ScheduleNextShake();
    }

    void Update()
    {
        if (!isShaking)
        {
            if (Time.time >= nextShakeTime)
                StartShake();
        }
        else
        {
            shakeTimer -= Time.deltaTime;
            if (shakeTimer <= 0f)
                StopShake();
            else
                ApplyShakeOffset();
        }
    }

    void StartShake()
    {
        isShaking = true;
        shakeTimer = shakeDuration;
    }

    void StopShake()
    {
        isShaking = false;
        // 精确回到原位
        transform.localPosition = originalLocalPos;
        ScheduleNextShake();
    }

    void ApplyShakeOffset()
    {
        float x = Random.Range(-1f, 1f) * shakeAmount;
        float y = Random.Range(-1f, 1f) * shakeAmount;
        // 基于原始眼睛高度进行偏移，不修改 Z 轴
        transform.localPosition = originalLocalPos + new Vector3(x, y, 0f);
    }

    void ScheduleNextShake()
    {
        nextShakeTime = Time.time + Random.Range(minInterval, maxInterval);
    }
}