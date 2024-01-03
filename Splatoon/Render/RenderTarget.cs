using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using System;
using System.Windows.Forms;
using Format = SharpDX.DXGI.Format;
using Vector2 = SharpDX.Vector2;

namespace Splatoon.Render;

// render target texture with utilities to render to self
public unsafe class RenderTarget : IDisposable
{
    public Vector2 Size { get; private set; }
    private SharpDX.Direct3D11.Device _device;
    private DeviceContext _ctx;
    private Texture2D _rt;
    private RenderTargetView _renderTargetView;
    private ShaderResourceView _rtSRV;
    private BlendState _blendState;

    public nint ImguiHandle => _rtSRV.NativePointer;

    public RenderTarget(int width, int height)
    {
        Size = new(width, height);
        _device = new((nint)FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device.Instance()->D3D11Forwarder);
        _ctx = new(_device);

        _rt = new(_device, new()
        {
            Width = width,
            Height = height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.R8G8B8A8_UNorm,
            SampleDescription = new(1, 0),
            Usage = ResourceUsage.Default,
            BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
            CpuAccessFlags = CpuAccessFlags.None,
            OptionFlags = ResourceOptionFlags.None
        });

        _renderTargetView = new(_device, _rt, new()
        {
            Format = Format.R8G8B8A8_UNorm,
            Dimension = RenderTargetViewDimension.Texture2D,
            Texture2D = new() { }
        });

        _rtSRV = new(_device, _rt, new()
        {
            Format = Format.R8G8B8A8_UNorm,
            Dimension = ShaderResourceViewDimension.Texture2D,
            Texture2D = new()
            {
                MostDetailedMip = 0,
                MipLevels = 1
            }
        });

        var blendDescription = BlendStateDescription.Default();
        blendDescription.RenderTarget[0].IsBlendEnabled = true;
        blendDescription.RenderTarget[0].SourceBlend = BlendOption.One;
        blendDescription.RenderTarget[0].DestinationBlend = BlendOption.InverseSourceAlpha;
        blendDescription.RenderTarget[0].BlendOperation = BlendOperation.Add;
        blendDescription.RenderTarget[0].SourceAlphaBlend = BlendOption.One;
        blendDescription.RenderTarget[0].DestinationAlphaBlend = BlendOption.Zero;
        blendDescription.RenderTarget[0].AlphaBlendOperation = BlendOperation.Add;
        blendDescription.RenderTarget[0].RenderTargetWriteMask = ColorWriteMaskFlags.All;
        _blendState = new(_device, blendDescription);
    }

    public void Dispose()
    {
        _ctx.Dispose();
        _rt.Dispose();
        _renderTargetView.Dispose();
        _rtSRV.Dispose();
        _blendState.Dispose();
    }

    public DeviceContext BeginRender()
    {
        _ctx.ClearRenderTargetView(_renderTargetView, new());
        _ctx.Rasterizer.SetViewport(0, 0, Size.X, Size.Y);
        _ctx.OutputMerger.SetBlendState(_blendState);
        _ctx.OutputMerger.SetTargets(_renderTargetView);
        return _ctx;
    }

    public void EndRender()
    {
        using var cmds = _ctx.FinishCommandList(true);
        _device.ImmediateContext.ExecuteCommandList(cmds, true);
    }
}
