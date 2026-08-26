using ECommons.DalamudServices;
using ECommons.Hooks;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using Splatoon.SplatoonScripting;
using System.Collections.Generic;

namespace SplatoonScriptsOfficial.Duties.Dawntrail.The_Futures_Rewritten;
public unsafe class P2_Delete_Intermission_Ice : SplatoonScript
{
    public override HashSet<uint>? ValidTerritories { get; } = [1238];
    public override Metadata? Metadata => new(2, "NightmareXIV");

    public override void OnMapEffect(uint position, ushort data1, ushort data2)
    {
        if(position == 24 && data1 == 1 && data2 == 2)
        {
            Svc.Framework.RunOnTick(() =>
            {
                var eventFramework = EventFramework.Instance();
                if(eventFramework == null) return;
                //  +344 is the InstanceContentDirector slot inside EventFramework.
                //  The slot is empty outside of instanced content, and this runs one tick
                //  later, by which time the duty may already have been left.
                var director = *(nint*)((nint)eventFramework + 344);
                if(director == nint.Zero) return;
                MapEffect.Delegate(director, 24, 4, 8);
            });
        }
    }
}
