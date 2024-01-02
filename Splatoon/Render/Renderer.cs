using Dalamud.Interface.Utility;
using Dalamud.Utility;
using ECommons.ImGuiMethods;
using ImGuiNET;
using SharpDX.Direct3D11;
using Splatoon.Render;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Splatoon.Render;

public unsafe class Renderer : IDisposable
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint GetEngineCoreSingletonDelegate();

    private nint _engineCoreSingleton;
    private RenderTarget? _rt;
    private DynamicMesh _mesh = new(16 * 1024, 16 * 1024, 128);
    private DeviceContext? _ctx;
    private DynamicMesh.Builder? _meshBuilder;

    public SharpDX.Matrix ViewProj { get; private set; }
    public SharpDX.Matrix Proj { get; private set; }
    public SharpDX.Matrix View { get; private set; }
    public SharpDX.Matrix CameraWorld { get; private set; }
    public float CameraAzimuth { get; private set; } // facing north = 0, facing west = pi/4, facing south = +-pi/2, facing east = -pi/4
    public float CameraAltitude { get; private set; } // facing horizontally = 0, facing down = pi/4, facing up = -pi/4
    public SharpDX.Vector2 ViewportSize { get; private set; }

    public Renderer()
    {
        _engineCoreSingleton = Marshal.GetDelegateForFunctionPointer<GetEngineCoreSingletonDelegate>(Svc.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 8D 4C 24 ?? 48 89 4C 24 ?? 4C 8D 4D ?? 4C 8D 44 24 ??"))();
    }

    public void Dispose()
    {
        _rt?.Dispose();
        _meshBuilder?.Dispose();
        _mesh.Dispose();
    }

    public void BeginFrame()
    {
        ViewProj = ReadMatrix(_engineCoreSingleton + 0x1B4);
        Proj = ReadMatrix(_engineCoreSingleton + 0x174);
        View = ViewProj * SharpDX.Matrix.Invert(Proj);
        CameraWorld = SharpDX.Matrix.Invert(View);
        CameraAzimuth = MathF.Atan2(View.Column3.X, View.Column3.Z);
        CameraAltitude = MathF.Asin(View.Column3.Y);
        ViewportSize = ReadVec2(_engineCoreSingleton + 0x1F4);

        if (_rt == null || _rt.Size != ViewportSize)
        {
            _rt?.Dispose();
            _rt = new((int)ViewportSize.X, (int)ViewportSize.Y);
        }

        _ctx = _rt.BeginRender();
        _meshBuilder = _mesh.Build(_ctx, new() { View = View, Proj = Proj });
    }

    public void EndFrame()
    {
        _meshBuilder?.Dispose();
        _meshBuilder = null;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
        ImGuiHelpers.ForceNextWindowMainViewport();
        ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(0, 0));
        ImGui.Begin("world_overlay", ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoBackground);
        ImGui.SetWindowSize(ImGui.GetIO().DisplaySize);

        if (_ctx != null && _rt != null)
        {
            _mesh.Draw(_ctx);
            _rt.EndRender();
            ImGui.GetWindowDrawList().AddImage(_rt.ImguiHandle, new(), new(_rt.Size.X, _rt.Size.Y));
        }
        _ctx = null;

        ImGui.End();
        ImGui.PopStyleVar();
    }

    public void DrawCircle(Vector3 origin, float radius, float minAngle, float maxAngle, Vector4 originColor, Vector4 endColor)
    {
        Circle circle = new(36, minAngle, maxAngle);
        Matrix4x4 world = Matrix4x4.CreateScale(radius) * Matrix4x4.CreateTranslation(origin);
        _meshBuilder?.Add(circle, ref world, originColor, endColor);
    }

    public void DrawDonut(Vector3 origin, float innerRadius, float outerRadius, float minAngle, float maxAngle, Vector4 originColor, Vector4 endColor)
    {
        Donut circle = new(36, innerRadius, outerRadius, minAngle, maxAngle);
        Matrix4x4 world = Matrix4x4.CreateTranslation(origin);
        _meshBuilder?.Add(circle, ref world, originColor, endColor);
    }

    public void DrawLine(Vector3 origin, Vector3 direction, float radius, Vector4 originColor, Vector4 endColor)
    {
        Matrix4x4 world = Matrix4x4.CreateTranslation(origin);
        Line mesh = new(direction, radius);
        _meshBuilder?.Add(mesh, ref world, originColor, endColor);
    }

    public void DebugShape(Vector3 origin, Vector4 color)
    {
        Matrix4x4 world = Matrix4x4.CreateTranslation(origin); // * Matrix4x4.CreateScale(outerRadius);
        //Svc.Log.Warning(world.ToString());
        Debug mesh = new();
        _meshBuilder?.Add(mesh, ref world, color, color);
    }

    // public void DrawMesh(IMesh mesh, ref Matrix4x3 world, Vector4 color) => _meshBuilder?.Add(mesh, ref world, color);

    private unsafe SharpDX.Matrix ReadMatrix(IntPtr address)
    {
        var p = (float*)address;
        SharpDX.Matrix mtx = new();
        for (var i = 0; i < 16; i++)
            mtx[i] = *p++;
        return mtx;
    }

    private unsafe SharpDX.Vector2 ReadVec2(IntPtr address)
    {
        var p = (float*)address;
        return new(p[0], p[1]);
    }
}
