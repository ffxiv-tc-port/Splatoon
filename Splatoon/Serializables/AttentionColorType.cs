using System;
using System.Collections.Generic;
using System.Text;

namespace Splatoon.Serializables;

/// <summary>
/// 「注意色」的產生方式。腳本透過 <c>Controller.AttentionColor</c> 取得目前這一幀的顏色，
/// 用來標示「必須立刻處理」的元素。
/// </summary>
public enum AttentionColorType
{
    Rainbow,
    Gradient,
    Fixed,
}
