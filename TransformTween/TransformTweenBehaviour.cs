using System;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
public class TransformTweenBehaviour : PlayableBehaviour
{
    public enum TweenType
    {
        Linear,
        Deceleration,
        Harmonic,
        Custom,
    }

    public Transform startLocation;
    public Transform endLocation;
    public bool tweenPosition = true;
    public bool tweenRotation = true;
    public TweenType tweenType;
    public AnimationCurve customCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    
    public Vector3 startingPosition;
    public Quaternion startingRotation = Quaternion.identity;
    AnimationCurve m_LinearCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    AnimationCurve m_DecelerationCurve = new AnimationCurve
    (
        new Keyframe(0f, 0f, -k_RightAngleInRads, k_RightAngleInRads),
        new Keyframe(1f, 1f, 0f, 0f)
    );
    AnimationCurve m_HarmonicCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    const float k_RightAngleInRads = Mathf.PI * 0.5f;

    public enum TweenMode
    {
        Line, // 直线
        ParaHeight, // 抛物线
        ParaGravity // 根据重力加速度配置
    }

    public TweenMode tweenMode;
    public float paraHeight = 3f;
    public float paraGravity = 10f;

    // 使用重力加速算出当前位置
    // 主要利用物理公式: s = v * t + 0.5 * a * t * t
    Vector3 LerpParaGravityPosition(float t, float duration)
    {
        var curTime = t * duration;
        var startPos = startingPosition;
        var endPos = endLocation.position;

        float yDis = endPos.y - startPos.y;
        float vy = yDis / duration + 0.5f * paraGravity * duration;

        float curX = Mathf.Lerp(startPos.x, endPos.x, t);
        float curZ = Mathf.Lerp(startPos.z, endPos.z, t);
        float curY = vy * curTime - 0.5f * paraGravity * curTime * curTime + startPos.y;
        return new Vector3(curX, curY, curZ);
    }

    // 通过最大抛物线高度, 求解抛物线方程: y = a * x * x +  b * x
    // b = (-w - sqrt(ww - 4 * (ww/4h) * yDis)) / (2 * -WW/(4h))
    // a = -bb/(4h)
    // 其中: a < 0, b > 0
    Vector3 LerpParaHeightPosition(float t)
    {
        var startPos = startingPosition;
        var endPos = endLocation.position;
        var yDis = endPos.y - startPos.y;
        var h = paraHeight;

        // 目标高度比, 最大高度还大, 退化为线性
        if (yDis > h)
        {
            UnityEngine.Debug.LogWarning($"para mode: yDis beyond paraHeight! yDis: {yDis}, h: {h}");
            return LerpLinePosition(t);
        }

        var WW = new Vector2(endPos.x - startPos.x, endPos.z - startPos.z).sqrMagnitude;
        var W = Mathf.Sqrt(WW);

        var B = 1 + Mathf.Sqrt(1 -  yDis / h);
        B /= (W / (2 * h));
        var A = -B * B / (4 * h);

        var X = t * W;
        var Y = A * X * X + B * X;

        float curX = Mathf.Lerp(startPos.x, endPos.x, t);
        float curZ = Mathf.Lerp(startPos.z, endPos.z, t);
        return new Vector3(curX, Y, curZ);
    }

    Vector3 LerpLinePosition(float t)
    {
        var start = startingPosition;
        var end = endLocation.position;
        return Vector3.Lerp(start, end, t);
    }

    public Vector3 LerpPosition(float t, float duration)
    {
        switch (tweenMode)
        {
            case TweenMode.ParaHeight:
                return LerpParaHeightPosition(t);
            case TweenMode.ParaGravity:
                return LerpParaGravityPosition(t, duration);
            default:
                return LerpLinePosition(t);
        }
    }

    public override void PrepareFrame (Playable playable, FrameData info)
    {
        if (startLocation)
        {
            startingPosition = startLocation.position;
            startingRotation = startLocation.rotation;
        }
    }

    public float EvaluateCurrentCurve (float time)
    {
        if (tweenType == TweenType.Custom && !IsCustomCurveNormalised ())
        {
            Debug.LogError("Custom Curve is not normalised.  Curve must start at 0,0 and end at 1,1.");
            return 0f;
        }
        
        switch (tweenType)
        {
            case TweenType.Linear:
                return m_LinearCurve.Evaluate (time);
            case TweenType.Deceleration:
                return m_DecelerationCurve.Evaluate (time);
            case TweenType.Harmonic:
                return m_HarmonicCurve.Evaluate (time);
            default:
                return customCurve.Evaluate (time);
        }
    }

    bool IsCustomCurveNormalised ()
    {
        if (!Mathf.Approximately (customCurve[0].time, 0f))
            return false;
        
        if (!Mathf.Approximately (customCurve[0].value, 0f))
            return false;
        
        if (!Mathf.Approximately (customCurve[customCurve.length - 1].time, 1f))
            return false;
        
        return Mathf.Approximately (customCurve[customCurve.length - 1].value, 1f);
    }
}