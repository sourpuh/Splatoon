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
    private Texture2D _depth;
    private DepthStencilView _depthStencilView;
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

        _depth = new(_device, new()
        {
            Width = width,
            Height = height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.D32_Float,
            SampleDescription = new(1, 0),
            Usage = ResourceUsage.Default,
            BindFlags = BindFlags.DepthStencil,
            CpuAccessFlags = CpuAccessFlags.None,
            OptionFlags = ResourceOptionFlags.None
        });

        _depthStencilView = new(_device, _depth, new()
        {
            Format = Format.D32_Float,
            Dimension = DepthStencilViewDimension.Texture2D,
            Texture2D = new() { }
        });

        var blendDescription = BlendStateDescription.Default();
        for (int i = 0; i < blendDescription.RenderTarget.Length; i++)
        {
            var renderTarget = blendDescription.RenderTarget[i];
            renderTarget.IsBlendEnabled = true;
            renderTarget.SourceBlend = BlendOption.SourceAlpha;
            renderTarget.DestinationBlend = BlendOption.InverseSourceAlpha;
            renderTarget.BlendOperation = BlendOperation.Add;
            renderTarget.SourceAlphaBlend = BlendOption.One;
            renderTarget.DestinationAlphaBlend = BlendOption.Zero;
            renderTarget.AlphaBlendOperation = BlendOperation.Add;
            renderTarget.RenderTargetWriteMask = ColorWriteMaskFlags.All;
        }
        _blendState = new(_device, blendDescription);
    }

    public void Dispose()
    {
        _ctx.Dispose();
        _rt.Dispose();
        _renderTargetView.Dispose();
        _rtSRV.Dispose();
        _depth.Dispose();
        _depthStencilView.Dispose();
        _blendState.Dispose();
    }

    public DeviceContext BeginRender()
    {
        _ctx.ClearRenderTargetView(_renderTargetView, new());
        _ctx.ClearDepthStencilView(_depthStencilView, DepthStencilClearFlags.Depth, 1, 0);
        _ctx.Rasterizer.SetViewport(0, 0, Size.X, Size.Y);
        _ctx.OutputMerger.SetTargets(_depthStencilView, _renderTargetView);
        _ctx.OutputMerger.SetBlendState(_blendState);
        return _ctx;
    }

    public void EndRender()
    {
        using var cmds = _ctx.FinishCommandList(true);
        _device.ImmediateContext.ExecuteCommandList(cmds, true);
    }
}
