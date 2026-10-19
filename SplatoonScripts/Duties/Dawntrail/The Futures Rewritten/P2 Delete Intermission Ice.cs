using ECommons.DalamudServices;
using ECommons.Hooks;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using Splatoon.SplatoonScripting;
using System.Collections.Generic;

namespace SplatoonScriptsOfficial.Duties.Dawntrail.The_Futures_Rewritten;
public unsafe class P2_Delete_Intermission_Ice : SplatoonScript
{
    public override HashSet<uint>? ValidTerritories { get; } = [1238];
    public override Metadata? Metadata => new(3, "NightmareXIV");

    public override void OnMapEffect(uint position, ushort data1, ushort data2)
    {
        if(position == 24 && data1 == 1 && data2 == 2)
        {
            Svc.Framework.RunOnTick(() =>
            {
                //  This runs one tick later, by which time the duty may already have been left.
                var eventFramework = EventFramework.Instance();
                if(eventFramework == null) return;
                var director = eventFramework->DirectorModule.ActiveContentDirector;
                //  The native function dereferences MapEffects without checking it;
                //  the game's own callers check it first, so the same check is required here.
                if(director == null || director->MapEffects == null) return;
                MapEffect.Delegate((nint)director, 24, 4, 8);
            });
        }
    }
}
