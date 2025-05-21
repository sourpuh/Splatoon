using Dalamud.Interface.Utility.Raii;
using ECommons.DalamudServices;
using ImGuiNET;
using Pictomancy;
using Splatoon.SplatoonScripting;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SplatoonScriptsOfficial.Generic;
/**
 * Quick hack to preview VFX. Open the script settings and hover buttons to preview those VFX on your character.
 * Click the button to copy the VFX name to clipboard.
 */
internal class VfxPreviewer : SplatoonScript
{
    public override HashSet<uint>? ValidTerritories => [];
    public override Metadata? Metadata => new(0, "sourpuh");

    public string[] OmenNames;
    public string[] LockonNames;
    public string[] ChannelingNames;
    public string[] CommonNames;

    Vector4 Color = Vector4.One;
    Vector3 OmenScale = new(10f);
    bool OmenScaleLock = true;

    public override void OnSettingsDraw()
    {
        ImGui.ColorEdit4("Color Tint", ref Color, ImGuiColorEditFlags.NoInputs);
        using (var bar = ImRaii.TabBar("tabs"))
        {
            using (var tab = ImRaii.TabItem("Omen"))
            {
                if (tab)
                {
                    ImGui.Checkbox("Lock Proportions", ref OmenScaleLock);
                    if (OmenScaleLock) {
                        var scale = OmenScale.X;
                        if (ImGui.SliderFloat("Scale", ref scale, 0.1f, 100f))
                        {
                            OmenScale = new(scale);
                        }
                    }
                    else
                    {
                        ImGui.SliderFloat("Width", ref OmenScale.X, 0.1f, 100f);
                        ImGui.SliderFloat("Length", ref OmenScale.Z, 0.1f, 100f);
                        ImGui.SliderFloat("Height", ref OmenScale.Y, 0.1f, 100f);
                    }
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
                                PictoService.VfxRenderer.AddOmen("preview", name, Svc.ClientState.LocalPlayer.Position, OmenScale, Svc.ClientState.LocalPlayer.Rotation, Color);
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
                                PictoService.VfxRenderer.AddLockon("preview", name, Svc.ClientState.LocalPlayer, color: Color);
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
                                PictoService.VfxRenderer.AddChanneling("preview", name, Svc.ClientState.LocalPlayer, Svc.ClientState.LocalPlayer.TargetObject, color: Color);
                            }
                        }
                        ImGui.EndTable();
                    }
                }
            }
            using (var tab = ImRaii.TabItem("Common"))
            {
                if (tab)
                {
                    if (ImGui.BeginTable("VfxList", 1, ImGuiTableFlags.BordersOuter | ImGuiTableFlags.ScrollY))
                    {
                        ImGui.TableSetupColumn("Core", ImGuiTableColumnFlags.WidthStretch);

                        foreach (var name in CommonNames)
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
                                PictoService.VfxRenderer.AddCommon("preview", name, Svc.ClientState.LocalPlayer, Svc.ClientState.LocalPlayer, color: Color);
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
        OmenNames = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Omen>().Select(x => x.Path.ExtractText()).Where(x => !string.IsNullOrEmpty(x)).Order().Distinct().ToArray();
        LockonNames = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Lockon>().Select(x => x.Unknown0.ExtractText()).Where(x => !string.IsNullOrEmpty(x)).Order().Distinct().ToArray();
        ChannelingNames = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Channeling>().Select(x => x.File.ExtractText()).Where(x => !string.IsNullOrEmpty(x)).Order().Distinct().ToArray();
        CommonNames = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.VFX>().Select(x => x.Location.ExtractText()).Where(x => !string.IsNullOrEmpty(x)).Order().Distinct().ToArray();
    }
}

