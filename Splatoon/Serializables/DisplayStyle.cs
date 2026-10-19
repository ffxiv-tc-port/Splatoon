namespace Splatoon.Serializables;

public struct DisplayStyle(
    uint strokeColor,
    float strokeThickness,
    float fillIntensity,
    uint originFillColor,
    uint endFillColor,
    bool filled = true,
    bool overrideFillColor = false,
    float castFraction = 0,
    AnimationStyle animation = default)
{
    public uint strokeColor = strokeColor;
    public float strokeThickness = strokeThickness;
    public float fillIntensity = fillIntensity;
    public uint originFillColor = originFillColor;
    public uint endFillColor = endFillColor;
    public bool filled = filled;
    public bool overrideFillColor = overrideFillColor;
    public float castFraction = castFraction;
    public AnimationStyle animation = animation;

    // 每個欄位逐位元相同才算相同（浮點數比位元，不用 ==）。新增欄位時這裡要一起加：
    // 繪製端靠它判斷上一幀的顯示物件能不能沿用，漏比的欄位改了畫面不會跟著變。
    internal readonly bool BitwiseEquals(in DisplayStyle other)
    {
        return strokeColor == other.strokeColor
            && BitConverter.SingleToInt32Bits(strokeThickness) == BitConverter.SingleToInt32Bits(other.strokeThickness)
            && BitConverter.SingleToInt32Bits(fillIntensity) == BitConverter.SingleToInt32Bits(other.fillIntensity)
            && originFillColor == other.originFillColor
            && endFillColor == other.endFillColor
            && filled == other.filled
            && overrideFillColor == other.overrideFillColor
            && BitConverter.SingleToInt32Bits(castFraction) == BitConverter.SingleToInt32Bits(other.castFraction)
            && animation.BitwiseEquals(other.animation);
    }
}

public struct AnimationStyle(CastAnimationKind kind, uint color, float size, float frequency)
{
    public CastAnimationKind kind = kind;
    public uint color = color;
    public float size = size;
    public float frequency = frequency;

    // 同 DisplayStyle.BitwiseEquals：新增欄位時這裡要一起加。
    internal readonly bool BitwiseEquals(in AnimationStyle other)
    {
        return kind == other.kind
            && color == other.color
            && BitConverter.SingleToInt32Bits(size) == BitConverter.SingleToInt32Bits(other.size)
            && BitConverter.SingleToInt32Bits(frequency) == BitConverter.SingleToInt32Bits(other.frequency);
    }
}
