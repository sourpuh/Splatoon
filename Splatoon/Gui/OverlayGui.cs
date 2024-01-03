﻿using Dalamud.Game.ClientState.Conditions;
using ImGuiNET;
using ImGuiScene;
using Splatoon.Structures;
using Splatoon.Render;
using System.Runtime.InteropServices;


namespace Splatoon.Gui;

unsafe class OverlayGui : IDisposable
{
    static readonly Vector2 UV = ImGui.GetFontTexUvWhitePixel();
    const int RADIAL_SEGMENTS_PER_RADIUS_UNIT = 20;
    const int MINIMUM_CIRCLE_SEGMENTS = 24;
    const int MAXIMUM_CIRCLE_SEGMENTS = 240;
    const int LINEAR_SEGMENTS_PER_UNIT = 1;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr GetMatrixSingletonDelegate();
    private GetMatrixSingletonDelegate _getMatrixSingleton { get; init; }

    public Matrix4x4 ViewProj { get; private set; }
    public Vector2 ViewportSize { get; private set; }

    Renderer renderer;
    readonly Splatoon p;
    int uid = 0;
    public OverlayGui(Splatoon p)
    {
        this.p = p;
        renderer = new Renderer();
        Svc.PluginInterface.UiBuilder.Draw += Draw;
        // https://github.com/goatcorp/Dalamud/blob/d52118b3ad366a61216129c80c0fa250c885abac/Dalamud/Game/Gui/GameGuiAddressResolver.cs#L69
        var funcAddress = Svc.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 8D 4C 24 ?? 48 89 4c 24 ?? 4C 8D 4D ?? 4C 8D 44 24 ??");
        _getMatrixSingleton = Marshal.GetDelegateForFunctionPointer<GetMatrixSingletonDelegate>(funcAddress);
    }

    public void Dispose()
    {
        Svc.PluginInterface.UiBuilder.Draw -= Draw;
    }

    // Dynamic LoD for circles and cones
    // TODO it would be would be more efficient to adjust based on camera distance
    public static int RadialSegments(float radius, float angleRadians = MathF.PI * 2)
    {
        float angularPercent = angleRadians / (MathF.PI * 2);
        int segments = (int)(RADIAL_SEGMENTS_PER_RADIUS_UNIT * radius * angularPercent);
        int minimumSegments = Math.Max((int)(MINIMUM_CIRCLE_SEGMENTS * angularPercent), 1);
        int maximumSegments = Math.Max((int)(MAXIMUM_CIRCLE_SEGMENTS * angularPercent), 1);
        return Math.Clamp(segments, minimumSegments, maximumSegments);
    }
    public static int HorizontalLinearSegments(float radius)
    {
        return Math.Max((int)(radius / LINEAR_SEGMENTS_PER_UNIT), 1);
    }

    void Draw()
    {
        if (p.Profiler.Enabled) p.Profiler.Gui.StartTick();
        try
        {
            if (Svc.Condition[ConditionFlag.OccupiedInCutSceneEvent] || Svc.Condition[ConditionFlag.WatchingCutscene78])
            {
                return;
            }

            // Fill shapes
            renderer.BeginFrame();
            foreach (var element in p.displayObjects)
            {
                if (element is DisplayObjectFan elementFan)
                {
                    renderer.DrawDonut(XZY(elementFan.origin), elementFan.innerRadius, elementFan.outerRadius, elementFan.angleMin, elementFan.angleMax, elementFan.style.originFillColor.ToVector4(), elementFan.style.endFillColor.ToVector4());

                    //renderer.DrawCircle(XZY(elementFan.origin), elementFan.outerRadius, 0, 2 * MathF.PI, elementFan.style.strokeColor.ToVector4());
                    //renderer.DebugShape(XZY(elementFan.origin), elementFan.style.strokeColor.ToVector4());
                }
                if (element is DisplayObjectLine elementLine)
                {
                    renderer.DrawLine(elementLine.start, elementLine.stop - elementLine.start, elementLine.radius, elementLine.style.originFillColor.ToVector4(), elementLine.style.endFillColor.ToVector4());
                }
            }
            renderer.EndFrame();

            uid = 0;
            var matrixSingleton = _getMatrixSingleton();
            ViewProj = ReadMatrix(matrixSingleton + 0x1b4);
            ViewportSize = ReadVec2(matrixSingleton + 0x1f4);
            try
            {
                void Draw()
                {
                    foreach (var element in p.displayObjects)
                    {
                        if (element is DisplayObjectFan elementFan)
                        {
                            DrawTriangleFanWorld(elementFan);
                        }
                        if (element is DisplayObjectLine elementLine)
                        {
                            DrawLineWorld(elementLine);
                        }
                    }

                    foreach (var element in p.displayObjects)
                    {
                        if (element is DisplayObjectText elementText)
                        {
                            DrawTextWorld(elementText);
                        }
                    }
                }
                ImGuiHelpers.ForceNextWindowMainViewport();
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
                ImGuiHelpers.SetNextWindowPosRelativeMainViewport(Vector2.Zero);
                ImGui.SetNextWindowSize(ImGuiHelpers.MainViewport.Size);
                ImGui.Begin("Splatoon scene", ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoTitleBar
                    | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.AlwaysUseWindowPadding);
                if (P.Config.SplatoonLowerZ)
                {
                    CImGui.igBringWindowToDisplayBack(CImGui.igGetCurrentWindow());
                }
                if (P.Config.RenderableZones.Count == 0 || !P.Config.RenderableZonesValid)
                {
                    Draw();
                }
                else
                {
                    foreach (var e in P.Config.RenderableZones)
                    {
                        ImGui.PushClipRect(new Vector2(e.Rect.X, e.Rect.Y), new Vector2(e.Rect.Right, e.Rect.Bottom), false);
                        Draw();
                        ImGui.PopClipRect();
                    }
                }
                ImGui.End();
                ImGui.PopStyleVar();
            }
            catch (Exception e)
            {
                p.Log("Splatoon exception: please report it to developer", true);
                p.Log(e.Message, true);
                p.Log(e.StackTrace, true);
            }
        }
        catch (Exception e)
        {
            p.Log("Caught exception: " + e.Message, true);
            p.Log(e.StackTrace, true);
        }
        if (p.Profiler.Enabled) p.Profiler.Gui.StopTick();
    }


    public void DrawTriangleFanWorld(DisplayObjectFan e)
    {
        float totalAngle = e.angleMax - e.angleMin;
        int segments = RadialSegments(e.outerRadius, totalAngle);
        float angleStep = totalAngle / segments;

        int vertexCount = segments + 1;

        bool isCircle = totalAngle == MathF.PI * 2;
        StrokeConnection strokeStyle = isCircle ? StrokeConnection.NoConnection : StrokeConnection.ConnectOriginAndEnd;

        e.style.filled = false;
        RenderShape fan = new(e.style, VertexConnection.NoConnection, strokeStyle);
        for (int step = 0; step < vertexCount; step++)
        {
            float angle = e.angleMin + step * angleStep;

            var origin = e.origin;
            if (e.innerRadius != 0)
            {
                origin = RotatePoint(e.origin, angle, e.origin + new Vector3(0, e.innerRadius, 0));
            }

            var end = RotatePoint(e.origin, angle, e.origin + new Vector3(0, e.outerRadius, 0));
            fan.Add(XZY(origin), XZY(end));
        }
        fan.Draw(ViewProj);
    }

    void DrawLineWorld(DisplayObjectLine e)
    {
        e.style.filled = false;
        ImDrawListPtr drawList = ImGui.GetWindowDrawList();
        if (e.radius == 0)
        {
            if (p.Profiler.Enabled) p.Profiler.GuiLines.StartTick();
            var nearPlane = ViewProj.Column3();

            Vector3 start = e.start;
            Vector3 stop = e.stop;
            if (ClipLineToPlane(nearPlane, ref start, ref stop, out float _) == LineClipStatus.NotVisible)
                return;

            drawList.PathLineTo(WorldToScreen(ViewProj, start));
            drawList.PathLineTo(WorldToScreen(ViewProj, stop));
            drawList.PathStroke(e.style.strokeColor, ImDrawFlags.None, e.style.strokeThickness);
            if (p.Profiler.Enabled) p.Profiler.GuiLines.StopTick();
        }
        else
        {
            var leftStart = e.start - e.PerpendicularRadius;
            var leftStop = e.stop - e.PerpendicularRadius;

            var rightStart = e.start + e.PerpendicularRadius;
            var rightStop = e.stop + e.PerpendicularRadius;

            // This is a tiny hack. Instead of clipping the line horizontally properly, we just cull segments that are offscreen
            // By segmenting the line horizontally, culling offscreen segments still leaves segments on screen.
            // A better fix would be to clip the line horizontally instead of culling offscreen segments.
            int segments = HorizontalLinearSegments(e.radius);
            Vector3 perpendicularStep = e.PerpendicularRadius * 2 / segments;

            RenderShape line = new(e.style, VertexConnection.NoConnection, StrokeConnection.ConnectOriginAndEnd);
            for (int step = 0; step < segments; step++)
            {
                line.Add(leftStart + step * perpendicularStep, leftStop + step * perpendicularStep);

            }
            line.Add(rightStart, rightStop);
            line.Draw(ViewProj);
        }
    }

    public void DrawTextWorld(DisplayObjectText e)
    {
        if (Svc.GameGui.WorldToScreen(
                        new Vector3(e.x, e.z, e.y),
                        out Vector2 pos))
        {
            DrawText(e, pos);
        }
    }

    public void DrawText(DisplayObjectText e, Vector2 pos)
    {
        var scaled = e.fscale != 1f;
        var size = scaled ? ImGui.CalcTextSize(e.text) * e.fscale : ImGui.CalcTextSize(e.text);
        size = new Vector2(size.X + 10f, size.Y + 10f);
        ImGui.SetNextWindowPos(new Vector2(pos.X - size.X / 2, pos.Y - size.Y / 2));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(5, 5));
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 10f);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, ImGui.ColorConvertU32ToFloat4(e.bgcolor));
        ImGui.BeginChild("##child" + e.text + ++uid, size, false,
            ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoNav
            | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysUseWindowPadding);
        ImGui.PushStyleColor(ImGuiCol.Text, e.fgcolor);
        if (scaled) ImGui.SetWindowFontScale(e.fscale);
        ImGuiEx.Text(e.text);
        if (scaled) ImGui.SetWindowFontScale(1f);
        ImGui.PopStyleColor();
        ImGui.EndChild();
        ImGui.PopStyleColor();
        ImGui.PopStyleVar(2);
    }

    public void DrawPoint(DisplayObjectDot e)
    {
        if (Svc.GameGui.WorldToScreen(new Vector3(e.x, e.z, e.y), out Vector2 pos))
            ImGui.GetWindowDrawList().AddCircleFilled(
            new Vector2(pos.X, pos.Y),
            e.thickness,
            ImGui.GetColorU32(e.color),
            MINIMUM_CIRCLE_SEGMENTS);
    }

    private static unsafe Matrix4x4 ReadMatrix(IntPtr address)
    {
        var p = (float*)address;
        Matrix4x4 mtx = new();
        for (var i = 0; i < 16; i++)
            mtx[i / 4, i % 4] = *p++;
        return mtx;
    }
    private static unsafe Vector2 ReadVec2(IntPtr address)
    {
        var p = (float*)address;
        return new(p[0], p[1]);
    }
}
