using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Colors;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Windowing;
using ECommons.ChatMethods;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using ECommons.ImGuiMethods.TerritorySelection;
using ECommons.LanguageHelpers;
using ECommons.PartyFunctions;
using Lumina.Excel.Sheets;
using Splatoon.SplatoonScripting;
using Splatoon.SplatoonScripting.Priority;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Splatoon.Gui.Priority;
#nullable enable
public class PriorityPopupWindow : Window
{
    public uint TerritoryType;
    public readonly ObservableCollection<JobbedPlayer> Assignments = [];
    public static readonly IReadOnlyList<RolePosition> RolePositions = [RolePosition.T1, RolePosition.T2, RolePosition.H1, RolePosition.H2, RolePosition.M1, RolePosition.M2, RolePosition.R1, RolePosition.R2,];
    private TickScheduler? UpdateScheduler;
    public static readonly IReadOnlyDictionary<RolePosition, string> NormalNames = new Dictionary<RolePosition, string>()
    {
        [RolePosition.Not_Selected] = "Not Selected",
        [RolePosition.T1] = "T1",
        [RolePosition.T2] = "T2",
        [RolePosition.H1] = "H1",
        [RolePosition.H2] = "H2",
        [RolePosition.M1] = "M1",
        [RolePosition.M2] = "M2",
        [RolePosition.R1] = "R1",
        [RolePosition.R2] = "R2",
    };
    public static readonly IReadOnlyDictionary<RolePosition, string> DpsUniformNames = new Dictionary<RolePosition, string>()
    {
        [RolePosition.Not_Selected] = "Not Selected",
        [RolePosition.T1] = "T1",
        [RolePosition.T2] = "T2",
        [RolePosition.H1] = "H1",
        [RolePosition.H2] = "H2",
        [RolePosition.M1] = "D1",
        [RolePosition.M2] = "D2",
        [RolePosition.R1] = "D3",
        [RolePosition.R2] = "D4",
    };
    public static IReadOnlyDictionary<RolePosition, string> ConfiguredNames => (P.Config.PrioUnifyDps ? DpsUniformNames : NormalNames);
    private ImGuiEx.RealtimeDragDrop<JobbedPlayer> DragDrop = new("PrioAss", x => x.ID);

    public PriorityPopupWindow() : base("Splatoon Priority Editor", ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse)
    {
        this.SetSizeConstraints(new(500, 100), new(500, float.MaxValue));
        ShowCloseButton = false;
        RespectCloseHotkey = false;
        Assignments.CollectionChanged += Assignments_CollectionChanged;
    }

    private void Assignments_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateScheduler?.Dispose();
        UpdateScheduler = new(() => S.InfoBar.Update(false));
    }

    public override void Draw()
    {
        while(Assignments.Count < 8)
        {
            Assignments.Add(new());
        }
        while(Assignments.Count > 8)
        {
            Assignments.RemoveAt(Assignments.Count - 1);
        }
        ImGuiEx.TextWrapped($"您已進入一個啟用了使用優先順序清單的腳本的區域。若您有任何優先順序清單設為「佔位符」模式，請在此進行設定。");
        if(IsZoneSupported(TerritoryType))
        {
            ImGuiEx.CollectionCheckbox($"在 {ExcelTerritoryHelper.GetName(TerritoryType)} 顯示此彈出視窗", TerritoryType, P.Config.NoPrioPopupTerritories, true);
        }
        else
        {
            ImGuiEx.TextWrapped(EColor.OrangeBright, $"目前選擇的區域 {ExcelTerritoryHelper.GetName(TerritoryType)} 不支援優先順序清單。您仍可編輯它，但必須選擇受支援的區域才能儲存。");
        }
        if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.List, "選擇其他區域"))
        {
            new TerritorySelector(TerritoryType, (_, x) =>
            {
                TerritoryType = x;
                LoadMatchingAssignment();
            })
            {
                HiddenTerritories = Svc.Data.GetExcelSheet<TerritoryType>().Select(x => x.RowId).Where(x => !IsZoneSupported(x)).ToArray(),
                SelectedCategory = TerritorySelector.Category.All,
                ExtraColumns = [TerritorySelector.Column.ID, TerritorySelector.Column.IntendedUse],
                Mode = TerritorySelector.DisplayMode.PlaceNameDutyUnion,
            };
        }
        ImGui.Checkbox("以 D1/D2/D3/D4 顯示 DPS", ref P.Config.PrioUnifyDps);
        if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.List, "自動填入", enabled: ImGuiEx.Ctrl || Assignments.All(x => x.IsPlayerEmpty())))
        {
            Autofill();
        }
        ImGuiEx.Tooltip("按住 CTRL 並點擊");
        ImGui.SameLine();
        if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Ban, "清空清單", enabled: ImGuiEx.Ctrl || Assignments.All(x => x.IsPlayerEmpty())))
        {
            Assignments.Clear();
        }
        ImGuiEx.Tooltip("按住 CTRL 並點擊");

        DragDrop.Begin();
        if(ImGui.BeginTable("PrioAssTable", 3, ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("位置");
            ImGui.TableSetupColumn("DragDrop");
            ImGui.TableSetupColumn("名稱", ImGuiTableColumnFlags.WidthStretch);

            for(var i = 0; i < Assignments.Count; i++)
            {
                var item = Assignments[i];
                ImGui.PushID(item.ID);
                ImGui.TableNextRow();
                DragDrop.SetRowColor(item.ID);
                ImGui.TableNextColumn();
                DragDrop.NextRow();
                var col = (int)RolePositions[i] >= 3000 ? ImGuiColors.DPSRed : ((int)RolePositions[i] >= 2000 ? ImGuiColors.HealerGreen : ImGuiColors.TankBlue);
                ImGuiEx.TextV(col, ConfiguredNames[RolePositions[i]].FancySymbols());
                ImGui.TableNextColumn();
                DragDrop.DrawButtonDummy(item, Assignments, i);
                ImGui.TableNextColumn();
                item.DrawSelector(false);
                ImGui.SameLine();
                if(ImGuiEx.IconButton(FontAwesomeIcon.Ban))
                {
                    item.Jobs = [];
                    item.Name = "";
                }
                ImGuiEx.Tooltip("清除此指派");
                ImGui.PopID();
            }

            ImGui.EndTable();
        }
        DragDrop.End();

        ImGuiEx.LineCentered("PrioPopupWindow1", () =>
        {
            if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Check, "套用".Loc()))
            {
                IsOpen = false;
            }
            if(IsZoneSupported(TerritoryType))
            {
                ImGui.SameLine();
                if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Save, "套用並儲存".Loc()))
                {
                    IsOpen = false;
                    Save();
                }
            }
            ImGuiEx.Tooltip($"當您再次以相同玩家、相同職業進入 {ExcelTerritoryHelper.GetName(TerritoryType)} 時，此優先順序清單將會再次載入。");
        });
        S.InfoBar.Update(false);
    }

    public void Save()
    {
        while(true)
        {
            var ass = GetMatchingAssignment(TerritoryType);
            if(ass != null)
            {
                P.Config.RolePlayerAssignments.Remove(ass);
            }
            else
            {
                break;
            }
        }
        P.Config.RolePlayerAssignments.Add(new()
        {
            Players = [.. Assignments.JSONClone()],
            Territory = TerritoryType
        });
        P.Config.Save();
    }

    public static bool IsZoneSupported(uint zone)
    {
        return ExcelTerritoryHelper.GetTerritoryIntendedUse(zone).EqualsAny([
            TerritoryIntendedUseEnum.Alliance_Raid,
            TerritoryIntendedUseEnum.Dungeon,
            TerritoryIntendedUseEnum.Deep_Dungeon,
            TerritoryIntendedUseEnum.Variant_Dungeon,
            TerritoryIntendedUseEnum.Criterion_Duty,
            TerritoryIntendedUseEnum.Criterion_Savage_Duty,
            TerritoryIntendedUseEnum.Large_Scale_Savage_Raid,
            TerritoryIntendedUseEnum.Eureka,
            TerritoryIntendedUseEnum.Bozja,
            TerritoryIntendedUseEnum.Raid,
            TerritoryIntendedUseEnum.Raid_2,
            TerritoryIntendedUseEnum.Trial,
            TerritoryIntendedUseEnum.Large_Scale_Raid,
            TerritoryIntendedUseEnum.Treasure_Map_Duty,
            ]);
    }

    public bool IsValid()
    {
        return UniversalParty.LengthPlayback == Assignments.Count(x => x.IsInParty(false, out _));
    }

    public void Autofill()
    {
        new TickScheduler(() =>
        {
            var jobs = UniversalParty.MembersPlayback.OrderBy(x => GetOrderedRoleIndex(x.ClassJob)).Select(x => new JobbedPlayer() { Jobs = [x.ClassJob], Name = x.NameWithWorld }).ToList();

            Assignments.Clear();
            Assignments.AddRange([new(), new(), new(), new(), new(), new(), new(), new()]);

            var preferred = P.Config.PreferredPositions.SafeSelect(Player.Job, RolePosition.Not_Selected);
            if(preferred != RolePosition.Not_Selected)
            {
                var index = RolePositions.IndexOf(preferred);
                if(index != -1)
                {
                    Assignments[index] = new()
                    {
                        Jobs = [Player.Job],
                        Name = Player.NameWithWorld
                    };
                    jobs.RemoveAll(x => x.Name == Player.NameWithWorld);
                }
            }

            var tanks = jobs.Where(x => x.Jobs.FirstOrNull()?.IsTank() == true).ToArray();
            var healers = jobs.Where(x => x.Jobs.FirstOrNull()?.IsHealer() == true).ToArray();
            var dps = jobs.Where(x => x.Jobs.FirstOrNull()?.IsDps() == true).ToArray();

            var tankSlots = Assignments.ToArray()[..2].Count(x => x.Name == "");
            var healerSlots = Assignments.ToArray()[2..4].Count(x => x.Name == "");
            var dpsSlots = Assignments.ToArray()[4..].Count(x => x.Name == "");

            //normal composition
            foreach(var x in tanks)
            {
                for(var i = 0; i < 2; i++)
                {
                    if(Assignments[i].IsPlayerEmpty())
                    {
                        Assignments[i] = x;
                        break;
                    }
                }
            }
            foreach(var x in healers)
            {
                for(var i = 2; i < 4; i++)
                {
                    if(Assignments[i].IsPlayerEmpty())
                    {
                        Assignments[i] = x;
                        break;
                    }
                }
            }
            foreach(var x in dps)
            {
                for(var i = 4; i < Assignments.Count; i++)
                {
                    if(Assignments[i].IsPlayerEmpty())
                    {
                        Assignments[i] = x;
                        break;
                    }
                }
            }
            //remaining players
            foreach(var x in jobs)
            {
                if(Assignments.Any(a => a.Name == x.Name && a.Jobs.SequenceEqual(x.Jobs))) continue;
                for(var i = 0; i < Assignments.Count; i++)
                {
                    if(Assignments[i].IsPlayerEmpty())
                    {
                        Assignments[i] = x;
                        break;
                    }
                }
            }
            S.InfoBar.Update(false);
        });
    }


    internal int GetOrderedRoleIndex(Job job)
    {
        if(job == Job.WAR) return 1;
        if(job == Job.PLD) return 2;
        if(job == Job.GNB) return 3;
        if(job == Job.DRK) return 4;
        if(job.IsTank()) return 10;
        if(job.IsHealer()) return 20;
        if(job.IsMeleeDps()) return 30;
        if(job.IsPhysicalRangedDps()) return 40;
        if(job.IsMagicalRangedDps()) return 50;
        return 999;
    }

    public bool ShouldAutoOpen()
    {
        return IsZoneSupported(TerritoryType) && ScriptingProcessor.AnyScriptUsesPriority(TerritoryType) && !P.Config.NoPrioPopupTerritories.Contains(TerritoryType) && !Svc.Condition[ConditionFlag.DutyRecorderPlayback];
    }

    public void Open(bool force)
    {
        IsOpen = false;
        TerritoryType = Svc.ClientState.TerritoryType;
        P.TaskManager.Abort();
        if(force)
        {
            open();
        }
        else
        {
            P.TaskManager.Enqueue(() =>
            {
                if(Player.Available)
                {
                    LoadMatchingAssignment();
                    open();
                    return true;
                }
                return false;
            });
        }
        void open()
        {
            if(force || ShouldAutoOpen())
            {
                IsOpen = true;
            }
        }
    }

    public bool LoadMatchingAssignment()
    {
        var ass = GetMatchingAssignment(TerritoryType);
        if(ass != null)
        {
            Assignments.Clear();
            Assignments.AddRange(ass.Players.JSONClone());
            var assString = $"{RolePositions.Select(x => $"{x}: {Assignments.SafeSelect(RolePositions.IndexOf(x)).GetNameAndJob()}").Print("\n")}";
            var assTitle = $"Priority assignments loaded for {ExcelTerritoryHelper.GetName(TerritoryType)}";
            if(P.Config.ScriptPriorityNotification == Serializables.PriorityInfoOption.Print_in_chat_with_roles)
            {
                ChatPrinter.Green($"[Splatoon] {assTitle}:\n{assString}");
            }
            else if(P.Config.ScriptPriorityNotification == Serializables.PriorityInfoOption.Print_in_chat)
            {
                ChatPrinter.Green($"[Splatoon] {assTitle}.");
            }
            else if(P.Config.ScriptPriorityNotification == Serializables.PriorityInfoOption.Display_notification)
            {
                ref var activeNnotification = ref Ref<IActiveNotification>.Get("PrioNotification");
                activeNnotification?.DismissNow();
                var notification = new Notification()
                {
                    Title = assTitle,
                    Content = assString,
                    Minimized = false,
                    InitialDuration = TimeSpan.FromSeconds(10),
                };
                activeNnotification = Svc.NotificationManager.AddNotification(notification);
            }
        }
        return ass != null;
    }

    public RolePlayerAssignment? GetMatchingAssignment(uint territory)
    {
        foreach(var x in P.Config.RolePlayerAssignments)
        {
            if(x.Territory == territory)
            {
                var members = UniversalParty.Members;
                foreach(var player in x.Players)
                {
                    if(player.Jobs.Count > 0)
                    {
                        if(members.TryGetFirst(p => p.ClassJob.EqualsAny(player.Jobs)
                        &&
                        (player.Name == "" || p.NameWithWorld.EqualsIgnoreCase(player.Name) || p.Name.EqualsIgnoreCase(player.Name))
                        , out var upm))
                        {
                            members.Remove(upm);
                        }
                    }
                    else
                    {
                        if(members.TryGetFirst(p => p.NameWithWorld.EqualsIgnoreCase(player.Name) || p.Name.EqualsIgnoreCase(player.Name), out var upm))
                        {
                            members.Remove(upm);
                        }
                    }
                }
                if(members.Count == 0)
                {
                    return x;
                }
            }
        }
        return null;
    }
}
