using Dalamud.Interface.Utility.Raii;
using ECommons.DalamudServices;
using ImGuiNET;
using Pictomancy;
using Splatoon.SplatoonScripting;
using System.Collections.Generic;
using System.Linq;

namespace SplatoonScriptsOfficial.Generic;
/**
 * Quick hack to preview VFX. Open the script settings and hover buttons to preview those VFX on your character.
 * Click the button to copy the VFX name to clipboard.
 */
internal class VfxPreview : SplatoonScript
{
    public override HashSet<uint>? ValidTerritories => [];
    public override Metadata? Metadata => new(0, "sourpuh");

    public string[] OmenNames;
    public string[] LockonNames;
    public string[] ChannelingNames;

    public override void OnSettingsDraw()
    {
        using (var bar = ImRaii.TabBar("tabs"))
        {
            using (var tab = ImRaii.TabItem("Omen"))
            {
                if (tab)
                {
                    if (ImGui.BeginTable("VfxList", 1, ImGuiTableFlags.BordersOuter | ImGuiTableFlags.ScrollY))
                    {
                        ImGui.TableSetupColumn("Core", ImGuiTableColumnFlags.WidthStretch);
                        foreach (var name in OmenNames)
                        {
                            if (name == null) continue;
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            if (ImGui.Button(name))
                            {
                                ImGui.SetClipboardText(name);
                            }
                            if (ImGui.IsItemHovered())
                            {
                                PictoService.VfxRenderer.AddOmen("preview", name, Svc.ClientState.LocalPlayer.Position, new(10f));
                            }
                        }
                        ImGui.EndTable();
                    }
                }
            }
            using (var tab = ImRaii.TabItem("Lockon"))
            {
                if (tab)
                {
                    if (ImGui.BeginTable("VfxList", 1, ImGuiTableFlags.BordersOuter | ImGuiTableFlags.ScrollY))
                    {
                        ImGui.TableSetupColumn("Core", ImGuiTableColumnFlags.WidthStretch);

                        foreach (var name in LockonNames)
                        {
                            if (name == null) continue;
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            if (ImGui.Button(name))
                            {
                                ImGui.SetClipboardText(name);
                            }
                            if (ImGui.IsItemHovered())
                            {
                                PictoService.VfxRenderer.AddLockon("preview", name, Svc.ClientState.LocalPlayer);
                            }
                        }
                        ImGui.EndTable();
                    }
                }
            }
            using (var tab = ImRaii.TabItem("Channeling (requires target)"))
            {
                if (tab)
                {
                    if (ImGui.BeginTable("VfxList", 1, ImGuiTableFlags.BordersOuter | ImGuiTableFlags.ScrollY))
                    {
                        ImGui.TableSetupColumn("Core", ImGuiTableColumnFlags.WidthStretch);

                        foreach (var name in ChannelingNames)
                        {
                            if (name == null) continue;
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            if (ImGui.Button(name))
                            {
                                ImGui.SetClipboardText(name);
                            }
                            if (ImGui.IsItemHovered() && Svc.ClientState.LocalPlayer.TargetObject != null)
                            {
                                PictoService.VfxRenderer.AddChanneling("preview", name, Svc.ClientState.LocalPlayer, Svc.ClientState.LocalPlayer.TargetObject);
                            }
                        }
                        ImGui.EndTable();
                    }
                }
            }
        }
    }

    public override void OnSetup()
    {
        var omenPathList = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Omen>().Select(x => x.Path.ExtractText()).Where(x => !string.IsNullOrEmpty(x)).ToList();
        omenPathList.Sort();
        OmenNames = omenPathList.ToArray();

        var lockonPathList = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Lockon>().Select(x => x.Unknown0.ExtractText()).Where(x => !string.IsNullOrEmpty(x)).ToList();
        lockonPathList.Sort();
        LockonNames = lockonPathList.ToArray();

        var channelingPathList = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Channeling>().Select(x => x.File.ExtractText()).Where(x => !string.IsNullOrEmpty(x)).ToList();
        channelingPathList.Sort();
        ChannelingNames = channelingPathList.ToArray();
    }
}

