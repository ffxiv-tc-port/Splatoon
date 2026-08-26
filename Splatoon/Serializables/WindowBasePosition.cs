using System;
using System.Collections.Generic;
using System.Text;

namespace Splatoon.Serializables;

/// <summary>
/// 視窗在畫面某一軸上的基準位置。X 軸為 起點/置中/終點 = 左/中/右，
/// Y 軸為 起點/置中/終點 = 上/中/下。
/// </summary>
public enum WindowBasePosition
{
    Start, Middle, End
}
