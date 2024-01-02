using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Runtime.InteropServices;
using static System.Windows.Forms.DataFormats;
using Format = SharpDX.DXGI.Format;

namespace Splatoon.Render;

public unsafe class DynamicMesh : IDisposable
{
    public record struct Mesh(int FirstVertex, int FirstPrimitive, int NumPrimitives);

    [StructLayout(LayoutKind.Sequential)]
    public struct Constants
    {
        public Matrix View;
        public Matrix Proj;
    }

    public class Builder : IDisposable
    {
        private DynamicMesh _mesh;
        private DynamicBuffer.Builder _vertices;
        private DynamicBuffer.Builder _primitives;
        private DynamicBuffer.Builder _instances;

        internal Builder(DeviceContext ctx, DynamicMesh mesh)
        {
            _mesh = mesh;
            _vertices = mesh._vertexBuffer.Map(ctx);
            _primitives = mesh._primBuffer.Map(ctx);
            _instances = mesh._instanceBuffer.Map(ctx);

            mesh._meshes.Clear();
        }

        public void Dispose()
        {
            _vertices.Dispose();
            _primitives.Dispose();
            _instances.Dispose();
        }

        public void Add(IMesh mesh, ref Matrix4x4 world, System.Numerics.Vector4 color, System.Numerics.Vector4 color2)
        {
            var nv = mesh.NumVertices();
            var nt = mesh.NumTriangles();
            _mesh._meshes.Add(new(_vertices.NextElement, _primitives.NextElement, nt));

            _instances.Advance(1);
            _instances.Stream.Write(new System.Numerics.Vector4(world.M11, world.M21, world.M31, world.M41));
            _instances.Stream.Write(new System.Numerics.Vector4(world.M12, world.M22, world.M32, world.M42));
            _instances.Stream.Write(new System.Numerics.Vector4(world.M13, world.M23, world.M33, world.M43));

            _vertices.Advance(nv);
            for (int i = 0; i < nv; ++i)
            {
                _vertices.Stream.Write(mesh.Vertex(i));
                
                if (i % 2 == 0)
                {
                    _vertices.Stream.Write(color);
                }
                else
                {
                    _vertices.Stream.Write(color2);
                }
            }

            _primitives.Advance(nt);
            for (int i = 0; i < nt; ++i)
            {
                (int v1, int v2, int v3) = mesh.Triangle(i);
                _primitives.Stream.Write(v1);
                _primitives.Stream.Write(v2);
                _primitives.Stream.Write(v3);
            }
        }
    }

    public int MaxVertices { get; init; }
    public int MaxPrimitives { get; init; }
    public int MaxInstances { get; init; }

    private SharpDX.Direct3D11.Device _device;
    private DynamicBuffer _vertexBuffer;
    private DynamicBuffer _primBuffer;
    private DynamicBuffer _instanceBuffer;
    private SharpDX.Direct3D11.Buffer _constantBuffer;
    private InputLayout _il;
    private VertexShader _vs;
    private PixelShader _ps;
    private RasterizerState _rsWireframe;
    private List<Mesh> _meshes = new();

    public DynamicMesh(int maxVertices, int maxPrimitives, int maxInstances)
    {
        MaxVertices = maxVertices;
        MaxPrimitives = maxPrimitives;
        MaxInstances = maxInstances;

        _device = new((nint)FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device.Instance()->D3D11Forwarder);

        var shader = """
            struct Vertex
            {
                float3 pos : POSITION;
                float4 color : COLOR;
            };

            struct VSOutput
            {
                float4 projPos : SV_POSITION;
                float4 color : COLOR;
            };

            struct Instance
            {
                float4 worldColX : WORLD0;
                float4 worldColY : WORLD1;
                float4 worldColZ : WORLD2;
            };

            struct Constants
            {
                float4x4 view;
                float4x4 proj;
            };
            Constants k : register(c0);

            VSOutput vs(Vertex v, Instance i)
            {
                VSOutput res;
                float4 lp = float4(v.pos, 1.0);
                float wx = dot(lp, i.worldColX);
                float wy = dot(lp, i.worldColY);
                float wz = dot(lp, i.worldColZ);
                float3 viewPos = mul(float4(wx, wy, wz, 1.0), k.view).xyz;
                res.projPos = mul(float4(viewPos, 1), k.proj);
                res.color = v.color;
                // res.color = float4(0.5, 1.0, 1.0, 0.5);
                return res;
            }

            float4 ps(VSOutput input) : SV_Target
            {
                return input.color;
            }
            """;
        var vs = ShaderBytecode.Compile(shader, "vs", "vs_5_0");
        Svc.Log.Debug($"VS compile: {vs.Message}");
        _vs = new(_device, vs.Bytecode);

        var ps = ShaderBytecode.Compile(shader, "ps", "ps_5_0");
        Svc.Log.Debug($"PS compile: {ps.Message}");
        _ps = new(_device, ps.Bytecode);

        _vertexBuffer = new(_device, 3 * 4 + 16, maxVertices, BindFlags.VertexBuffer);
        _primBuffer = new(_device, 4 * 3, maxPrimitives, BindFlags.IndexBuffer);
        _instanceBuffer = new(_device, 16 * 3, maxVertices, BindFlags.VertexBuffer);
        _constantBuffer = new(_device, 16 * 4 * 2, ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);

        _il = new(_device, vs.Bytecode,
            [
                new InputElement("POSITION", 0, Format.R32G32B32_Float, -1, 0, InputClassification.PerVertexData, 0),
                new InputElement("COLOR", 0, Format.R32G32B32A32_Float, -1, 0, InputClassification.PerVertexData, 0),
                new InputElement("WORLD", 0, Format.R32G32B32A32_Float, -1, 1, InputClassification.PerInstanceData, 1),
                new InputElement("WORLD", 1, Format.R32G32B32A32_Float, -1, 1, InputClassification.PerInstanceData, 1),
                new InputElement("WORLD", 2, Format.R32G32B32A32_Float, -1, 1, InputClassification.PerInstanceData, 1),
            ]);

        var rsDesc = RasterizerStateDescription.Default();
        rsDesc.FillMode = FillMode.Wireframe;
        _rsWireframe = new(_device, rsDesc);
    }

    public void Dispose()
    {
        _vertexBuffer.Dispose();
        _primBuffer.Dispose();
        _instanceBuffer.Dispose();
        _constantBuffer.Dispose();
        _il.Dispose();
        _vs.Dispose();
        _ps.Dispose();
        _rsWireframe.Dispose();
    }

    public Builder Build(DeviceContext ctx, Constants consts)
    {
        consts.View.Transpose();
        consts.Proj.Transpose();
        ctx.UpdateSubresource(ref consts, _constantBuffer);
        return new Builder(ctx, this);
    }

    public void Draw(DeviceContext ctx, bool wireframe = false)
    {
        ctx.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
        ctx.InputAssembler.InputLayout = _il;
        ctx.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(_vertexBuffer.Buffer, _vertexBuffer.ElementSize, 0), new VertexBufferBinding(_instanceBuffer.Buffer, _instanceBuffer.ElementSize, 0));
        ctx.InputAssembler.SetIndexBuffer(_primBuffer.Buffer, Format.R32_UInt, 0);
        ctx.VertexShader.Set(_vs);
        ctx.VertexShader.SetConstantBuffer(0, _constantBuffer);
        if (wireframe)
            ctx.Rasterizer.State = _rsWireframe;
        ctx.PixelShader.Set(_ps);

        int i = 0;
        foreach (var m in _meshes)
        {
            ctx.DrawIndexedInstanced(m.NumPrimitives * 3, 1, m.FirstPrimitive * 3, m.FirstVertex, i++);
        }
    }
}
