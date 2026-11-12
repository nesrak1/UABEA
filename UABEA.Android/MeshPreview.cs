using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AssetsTools.NET;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.Runtime.InteropServices;

namespace UABEA.Mobile;

internal sealed record MeshPreviewData(Vector3[] Vertices, int[] Indices);
internal sealed record MeshStreamInfo(string Path, ulong Offset, ulong Size);

internal static class MeshPreviewDataReader
{
    private sealed record Channel(int Stream, int Offset, int Format, int Dimension);
    private sealed record StreamLayout(int Offset, int Stride);

    public static MeshStreamInfo ReadStreamInfo(AssetTypeValueField mesh)
    {
        var stream = Child(mesh, "m_StreamData");
        if (stream == null || stream.IsDummy)
            return new MeshStreamInfo("", 0, 0);

        return new MeshStreamInfo(
            ReadString(Child(stream, "path")),
            ReadUnsigned(Child(stream, "offset")),
            ReadUnsigned(Child(stream, "size")));
    }

    public static MeshPreviewData Read(AssetTypeValueField mesh, byte[]? streamedVertexData = null)
    {
        var vertexData = Child(mesh, "m_VertexData");
        Vector3[] vertices = vertexData == null || vertexData.IsDummy
            ? ReadLegacyVertices(mesh)
            : ReadVertexData(vertexData, streamedVertexData);

        if (vertices.Length < 3)
            throw new InvalidOperationException("This Mesh does not contain readable vertex positions.");

        int[] indices = ReadIndices(mesh, vertices.Length);
        if (indices.Length < 3)
            throw new InvalidOperationException("This Mesh does not contain a readable triangle index buffer.");

        return new MeshPreviewData(vertices, indices);
    }

    private static Vector3[] ReadLegacyVertices(AssetTypeValueField mesh)
    {
        var verticesField = Child(mesh, "m_Vertices");
        var array = Child(verticesField, "Array");
        if (array.IsDummy || array.Children.Count == 0)
            return Array.Empty<Vector3>();

        int count = Math.Min(array.Children.Count, 500_000);
        var result = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            var item = array.Children[i];
            float x = ReadFloat(Child(item, "x"), 0);
            float y = ReadFloat(Child(item, "y"), 0);
            float z = ReadFloat(Child(item, "z"), 0);
            if (item.Children.Count >= 3 && Child(item, "x").IsDummy)
            {
                x = ReadFloat(item.Children[0], 0);
                y = ReadFloat(item.Children[1], 0);
                z = ReadFloat(item.Children[2], 0);
            }
            result[i] = new Vector3(x, y, z);
        }
        return result;
    }

    private static Vector3[] ReadVertexData(AssetTypeValueField vertexData, byte[]? streamedVertexData)
    {
        int vertexCount = ReadInt(Child(vertexData, "m_VertexCount"), 0);
        if (vertexCount <= 0 || vertexCount > 500_000)
            throw new InvalidOperationException("The Mesh vertex count is invalid or exceeds the mobile preview limit.");

        byte[] data = streamedVertexData ?? ReadBytes(Child(vertexData, "m_DataSize"));
        if (data.Length == 0)
            throw new InvalidOperationException("The Mesh vertex buffer is empty.");

        var channelsArray = Child(Child(vertexData, "m_Channels"), "Array");
        var channels = new List<Channel>();
        if (channelsArray != null && !channelsArray.IsDummy)
        foreach (var item in channelsArray.Children)
        {
            channels.Add(new Channel(
                ReadInt(Child(item, "stream"), 0),
                ReadInt(Child(item, "offset"), 0),
                ReadInt(Child(item, "format"), 0),
                ReadInt(Child(item, "dimension"), 0)));
        }

        if (channels.Count == 0 || channels[0].Dimension <= 0)
            throw new InvalidOperationException("The Mesh has no position vertex channel.");

        Channel position = channels[0];
        int componentSize = FormatSize(position.Format);
        if (componentSize == 0 || position.Dimension < 3)
            throw new NotSupportedException("This Mesh uses an unsupported position vertex format.");

        var streamLayouts = new Dictionary<int, StreamLayout>();
        var streamsArray = Child(Child(vertexData, "m_Streams"), "Array");
        if (streamsArray != null && !streamsArray.IsDummy)
        foreach (var stream in streamsArray.Children)
        {
            int streamIndex = ReadInt(Child(stream, "stream"), streamLayouts.Count);
            int streamOffset = ReadInt(Child(stream, "offset"), 0);
            int stride = ReadInt(Child(stream, "stride"), 0);
            if (stride > 0)
                streamLayouts[streamIndex] = new StreamLayout(streamOffset, stride);
        }

        if (!streamLayouts.TryGetValue(position.Stream, out var layout))
        {
            int stride = channels.Where(c => c.Stream == position.Stream)
                .Select(c => c.Offset + FormatSize(c.Format) * c.Dimension)
                .DefaultIfEmpty(position.Offset + componentSize * position.Dimension)
                .Max();
            layout = new StreamLayout(0, stride);
        }

        if (layout.Stride <= 0)
            throw new InvalidOperationException("The Mesh vertex stream has an invalid stride.");

        var result = new Vector3[vertexCount];
        for (int i = 0; i < vertexCount; i++)
        {
            long start = (long)layout.Offset + (long)i * layout.Stride + position.Offset;
            float x = ReadVertexComponent(data, start, position.Format);
            float y = ReadVertexComponent(data, start + componentSize, position.Format);
            float z = ReadVertexComponent(data, start + componentSize * 2, position.Format);
            result[i] = new Vector3(x, y, z);
        }
        return result;
    }

    private static int[] ReadIndices(AssetTypeValueField mesh, int vertexCount)
    {
        byte[] indexBytes = ReadBytes(Child(mesh, "m_IndexBuffer"));
        var subMeshes = Child(Child(mesh, "m_SubMeshes"), "Array");
        var indices = new List<int>();

        if (indexBytes.Length > 0)
        {
            int indexFormat = ReadInt(Child(mesh, "m_IndexFormat"), vertexCount > ushort.MaxValue ? 1 : 0);
            int indexSize = indexFormat == 1 ? 4 : 2;
            if (subMeshes != null && !subMeshes.IsDummy)
            foreach (var subMesh in subMeshes.Children)
            {
                int topology = ReadInt(Child(subMesh, "topology"), 0);
                int firstByte = ReadInt(Child(subMesh, "firstByte"), 0);
                int indexCount = ReadInt(Child(subMesh, "indexCount"), 0);
                int baseVertex = ReadInt(Child(subMesh, "baseVertex"), 0);

                if (topology == 0)
                    AppendIndexRange(indexBytes, firstByte, indexCount, indexSize, baseVertex, vertexCount, indices);
                else if (topology == 2)
                    AppendQuadRange(indexBytes, firstByte, indexCount, indexSize, baseVertex, vertexCount, indices);
            }

            if (indices.Count == 0 && (subMeshes == null || subMeshes.IsDummy || subMeshes.Children.Count == 0))
                AppendIndexRange(indexBytes, 0, indexBytes.Length / indexSize, indexSize, 0, vertexCount, indices);
        }
        else
        {
            var legacy = Child(Child(mesh, "m_Indices"), "Array");
            if (legacy != null && !legacy.IsDummy)
            foreach (var item in legacy.Children)
            {
                int value = ReadInt(item, -1);
                if (value >= 0 && value < vertexCount)
                    indices.Add(value);
            }
        }

        if (indices.Count == 0 && vertexCount % 3 == 0)
        {
            int count = Math.Min(vertexCount, 75_000);
            for (int i = 0; i < count; i++)
                indices.Add(i);
        }

        int usable = Math.Min(indices.Count - indices.Count % 3, 360_000);
        return usable > 0 ? indices.Take(usable).ToArray() : Array.Empty<int>();
    }

    private static void AppendQuadRange(
        byte[] bytes, int firstByte, int count, int indexSize, int baseVertex,
        int vertexCount, List<int> output)
    {
        if (firstByte < 0 || count < 4 || firstByte > bytes.Length ||
            (long)firstByte + (long)count * indexSize > bytes.Length)
            return;

        int safeCount = Math.Min(count - count % 4, Math.Max(0, 360_000 - output.Count) / 6 * 4);
        for (int i = 0; i + 3 < safeCount; i += 4)
        {
            int offset = firstByte + i * indexSize;
            int a = ReadRawIndex(bytes, offset, indexSize) + baseVertex;
            int b = ReadRawIndex(bytes, offset + indexSize, indexSize) + baseVertex;
            int c = ReadRawIndex(bytes, offset + indexSize * 2, indexSize) + baseVertex;
            int d = ReadRawIndex(bytes, offset + indexSize * 3, indexSize) + baseVertex;
            if ((uint)a >= vertexCount || (uint)b >= vertexCount || (uint)c >= vertexCount || (uint)d >= vertexCount)
                continue;
            output.Add(a); output.Add(b); output.Add(c);
            output.Add(a); output.Add(c); output.Add(d);
        }
    }

    private static int ReadRawIndex(byte[] bytes, int offset, int indexSize) =>
        indexSize == 4 ? BitConverter.ToInt32(bytes, offset) : BitConverter.ToUInt16(bytes, offset);

    private static void AppendIndexRange(
        byte[] bytes, int firstByte, int count, int indexSize, int baseVertex,
        int vertexCount, List<int> output)
    {
        if (firstByte < 0 || count <= 0 || firstByte > bytes.Length ||
            (long)firstByte + (long)count * indexSize > bytes.Length)
            return;

        int safeCount = Math.Min(count - count % 3, 360_000 - output.Count);
        for (int i = 0; i < safeCount; i++)
        {
            int offset = firstByte + i * indexSize;
            int value = ReadRawIndex(bytes, offset, indexSize);
            long adjusted = (long)value + baseVertex;
            if (adjusted >= 0 && adjusted < vertexCount)
                output.Add((int)adjusted);
        }
    }

    private static float ReadVertexComponent(byte[] data, long offset, int format)
    {
        int size = FormatSize(format);
        if (offset < 0 || offset + size > data.Length)
            throw new InvalidOperationException("The Mesh vertex buffer is shorter than its vertex layout.");

        int i = (int)offset;
        return format switch
        {
            0 => BitConverter.ToSingle(data, i),
            1 => (float)BitConverter.UInt16BitsToHalf(BitConverter.ToUInt16(data, i)),
            2 => data[i] / 255f,
            3 => Math.Max(unchecked((sbyte)data[i]) / 127f, -1f),
            4 => BitConverter.ToUInt16(data, i) / 65535f,
            5 => Math.Max(BitConverter.ToInt16(data, i) / 32767f, -1f),
            _ => throw new NotSupportedException("This Mesh position format is not supported.")
        };
    }

    private static int FormatSize(int format) => format switch
    {
        0 => 4, 1 => 2, 2 or 3 => 1, 4 or 5 => 2,
        6 or 7 => 1, 8 or 9 => 2, 10 or 11 => 4,
        _ => 0
    };

    private static AssetTypeValueField Child(AssetTypeValueField parent, string name)
    {
        if (parent == null || parent.IsDummy)
            return parent!;
        return parent[name];
    }

    private static byte[] ReadBytes(AssetTypeValueField field)
    {
        if (field == null || field.IsDummy || field.Value == null)
            return Array.Empty<byte>();
        try { return field.AsByteArray ?? Array.Empty<byte>(); }
        catch { return Array.Empty<byte>(); }
    }

    private static string ReadString(AssetTypeValueField field)
    {
        if (field == null || field.IsDummy || field.Value == null)
            return "";
        try { return field.AsString ?? ""; }
        catch { return ""; }
    }

    private static ulong ReadUnsigned(AssetTypeValueField field)
    {
        if (field == null || field.IsDummy || field.Value == null)
            return 0;
        try
        {
            return field.Value.ValueType switch
            {
                AssetValueType.Int8 => (ulong)Math.Max(0, (int)field.AsSByte),
                AssetValueType.UInt8 => field.AsByte,
                AssetValueType.Int16 => (ulong)Math.Max(0, (int)field.AsShort),
                AssetValueType.UInt16 => field.AsUShort,
                AssetValueType.Int32 => (ulong)Math.Max(0, field.AsInt),
                AssetValueType.UInt32 => field.AsUInt,
                AssetValueType.Int64 => (ulong)Math.Max(0, field.AsLong),
                AssetValueType.UInt64 => field.AsULong,
                _ => 0
            };
        }
        catch { return 0; }
    }

    private static int ReadInt(AssetTypeValueField field, int fallback)
    {
        if (field == null || field.IsDummy || field.Value == null)
            return fallback;

        try
        {
            return field.Value.ValueType switch
            {
                AssetValueType.Int8 => field.AsSByte,
                AssetValueType.UInt8 => field.AsByte,
                AssetValueType.Int16 => field.AsShort,
                AssetValueType.UInt16 => field.AsUShort,
                AssetValueType.Int32 => field.AsInt,
                AssetValueType.UInt32 => checked((int)field.AsUInt),
                AssetValueType.Int64 => checked((int)field.AsLong),
                AssetValueType.UInt64 => checked((int)field.AsULong),
                AssetValueType.Bool => field.AsBool ? 1 : 0,
                _ => fallback
            };
        }
        catch { return fallback; }
    }

    private static float ReadFloat(AssetTypeValueField field, float fallback)
    {
        if (field == null || field.IsDummy || field.Value == null)
            return fallback;

        try
        {
            return field.Value.ValueType switch
            {
                AssetValueType.Float => field.AsFloat,
                AssetValueType.Double => (float)field.AsDouble,
                AssetValueType.Int32 => field.AsInt,
                AssetValueType.UInt32 => field.AsUInt,
                _ => fallback
            };
        }
        catch { return fallback; }
    }
}

internal sealed class MeshPreviewControl : Control, IDisposable
{
    private readonly bool _interactive;
    private readonly Vector3[] _normalizedVertices;
    private readonly int[] _indices;
    private readonly Vector3[] _cameraVertices;
    private readonly Point[] _screenVertices;
    private readonly float[] _cameraDepths;
    private readonly Dictionary<IPointer, Point> _pointers = new();
    private readonly Dictionary<IPointer, Point> _gestureStarts = new();
    private readonly HashSet<IPointer> _capturedPointers = new();

    private WriteableBitmap? _frameBitmap;
    private int[] _colorBuffer = Array.Empty<int>();
    private float[] _depthBuffer = Array.Empty<float>();
    private int _frameWidth;
    private int _frameHeight;
    private double _yaw = -0.55;
    private double _pitch = 0.25;
    private double _zoom = 1.0;
    private bool _wireframe;
    private double _lastPinchDistance;
    private Point _lastPinchCenter;
    private bool _disposed;

    private const int MaxRenderDimension = 720;
    private const int MaxPreviewIndices = 360_000;
    private static readonly Vector3 LightDirection =
        Vector3.Normalize(new Vector3(-0.35f, 0.65f, 0.7f));

    public MeshPreviewControl(Vector3[] vertices, int[] indices, bool interactive = false)
    {
        _interactive = interactive;
        _indices = indices ?? Array.Empty<int>();
        _normalizedVertices = NormalizeVertices(vertices ?? Array.Empty<Vector3>());
        _cameraVertices = new Vector3[_normalizedVertices.Length];
        _screenVertices = new Point[_normalizedVertices.Length];
        _cameraDepths = new float[_normalizedVertices.Length];

        MinHeight = 300;
        Height = 360;
        ClipToBounds = true;
        Focusable = interactive;
        DetachedFromVisualTree += (_, _) => Dispose();
    }

    private static Vector3[] NormalizeVertices(Vector3[] vertices)
    {
        if (vertices.Length == 0)
            return Array.Empty<Vector3>();

        Vector3 min = vertices[0], max = vertices[0];
        foreach (Vector3 v in vertices)
        {
            min = Vector3.Min(min, v);
            max = Vector3.Max(max, v);
        }

        Vector3 center = (min + max) * 0.5f;
        float size = Math.Max(max.X - min.X, Math.Max(max.Y - min.Y, max.Z - min.Z));
        float scale = size < 0.00001f ? 1f : 2.2f / size;

        var result = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
            result[i] = (vertices[i] - center) * scale;
        return result;
    }

    public void ResetView()
    {
        if (_disposed) return;
        _yaw = -0.55;
        _pitch = 0.25;
        _zoom = 1.0;
        InvalidateVisual();
    }

    public void ToggleWireframe()
    {
        if (_disposed) return;
        _wireframe = !_wireframe;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!_interactive || _disposed)
            return;

        Point position = e.GetPosition(this);
        _pointers[e.Pointer] = position;
        _gestureStarts[e.Pointer] = position;

        if (_pointers.Count >= 2)
        {
            foreach (var pointer in _pointers.Keys)
            {
                pointer.Capture(this);
                _capturedPointers.Add(pointer);
            }

            Point[] pair = _pointers.Values.Take(2).ToArray();
            _lastPinchDistance = Distance(pair[0], pair[1]);
            _lastPinchCenter = Midpoint(pair[0], pair[1]);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_interactive || _disposed || !_pointers.TryGetValue(e.Pointer, out Point previous))
            return;

        Point current = e.GetPosition(this);
        _pointers[e.Pointer] = current;

        if (_pointers.Count >= 2)
        {
            Point[] pair = _pointers.Values.Take(2).ToArray();
            double distance = Distance(pair[0], pair[1]);
            Point center = Midpoint(pair[0], pair[1]);

            if (_lastPinchDistance > 1)
                _zoom = Math.Clamp(_zoom * (distance / _lastPinchDistance), 0.45, 2.0);

            _yaw += (center.X - _lastPinchCenter.X) * 0.008;
            _pitch = Math.Clamp(_pitch + (center.Y - _lastPinchCenter.Y) * 0.008, -1.45, 1.45);
            _lastPinchDistance = distance;
            _lastPinchCenter = center;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (!_capturedPointers.Contains(e.Pointer))
        {
            Point start = _gestureStarts[e.Pointer];
            double dx = current.X - start.X;
            double dy = current.Y - start.Y;
            if (Math.Abs(dx) + Math.Abs(dy) < 7)
                return;

            e.Pointer.Capture(this);
            _capturedPointers.Add(e.Pointer);
        }

        _yaw += (current.X - previous.X) * 0.008;
        _pitch = Math.Clamp(_pitch + (current.Y - previous.Y) * 0.008, -1.45, 1.45);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_interactive)
            return;

        _pointers.Remove(e.Pointer);
        _gestureStarts.Remove(e.Pointer);
        bool captured = _capturedPointers.Remove(e.Pointer);
        if (captured)
            e.Pointer.Capture(null);

        _lastPinchDistance = 0;
        if (_pointers.Count == 1)
        {
            IPointer remaining = _pointers.Keys.First();
            _gestureStarts[remaining] = _pointers[remaining];
        }
        if (captured)
            e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pointers.Clear();
        _gestureStarts.Clear();
        _capturedPointers.Clear();
        _lastPinchDistance = 0;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        Rect bounds = new(Bounds.Size);
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(12, 12, 12)), bounds);

        if (_disposed || _normalizedVertices.Length < 3 || _indices.Length < 3 ||
            Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        double scale = Math.Min(1.0, MaxRenderDimension / Math.Max(Bounds.Width, Bounds.Height));
        int width = Math.Max(1, (int)Math.Ceiling(Bounds.Width * scale));
        int height = Math.Max(1, (int)Math.Ceiling(Bounds.Height * scale));
        EnsureFrameBuffer(width, height);
        Array.Fill(_colorBuffer, unchecked((int)0xFF0C0C0C));
        Array.Fill(_depthBuffer, float.PositiveInfinity);

        double cy = Math.Cos(_yaw), sy = Math.Sin(_yaw);
        double cp = Math.Cos(_pitch), sp = Math.Sin(_pitch);
        float cameraDistance = (float)(4.4 / _zoom);
        float focal = Math.Min(width, height) * 1.05f;
        float centerX = width * 0.5f, centerY = height * 0.5f;

        for (int i = 0; i < _normalizedVertices.Length; i++)
        {
            Vector3 p = _normalizedVertices[i];
            float rx = (float)(p.X * cy - p.Z * sy);
            float rz = (float)(p.X * sy + p.Z * cy);
            float ry = (float)(p.Y * cp - rz * sp);
            rz = (float)(p.Y * sp + rz * cp);

            _cameraVertices[i] = new Vector3(rx, ry, rz);
            float depth = cameraDistance + rz;
            _cameraDepths[i] = depth;
            if (depth <= 0.05f)
            {
                _screenVertices[i] = new Point(-100000, -100000);
                continue;
            }

            _screenVertices[i] = new Point(
                centerX + rx * focal / depth,
                centerY - ry * focal / depth);
        }

        int indexLimit = Math.Min(_indices.Length - _indices.Length % 3, MaxPreviewIndices);
        for (int i = 0; i < indexLimit; i += 3)
        {
            int ia = _indices[i], ib = _indices[i + 1], ic = _indices[i + 2];
            if ((uint)ia >= _screenVertices.Length || (uint)ib >= _screenVertices.Length ||
                (uint)ic >= _screenVertices.Length)
                continue;

            float za = _cameraDepths[ia], zb = _cameraDepths[ib], zc = _cameraDepths[ic];
            if (za <= 0.05f || zb <= 0.05f || zc <= 0.05f)
                continue;

            RasterizeTriangle(_screenVertices[ia], _screenVertices[ib], _screenVertices[ic],
                za, zb, zc, _cameraVertices[ia], _cameraVertices[ib], _cameraVertices[ic],
                width, height);
        }

        using (var framebuffer = _frameBitmap!.Lock())
        {
            for (int y = 0; y < height; y++)
                Marshal.Copy(_colorBuffer, y * width,
                    IntPtr.Add(framebuffer.Address, y * framebuffer.RowBytes), width);
        }

        context.DrawImage(_frameBitmap!, new Rect(0, 0, width, height), bounds);
    }

    private void RasterizeTriangle(
        Point pa, Point pb, Point pc,
        float za, float zb, float zc,
        Vector3 va, Vector3 vb, Vector3 vc,
        int width, int height)
    {
        float ax = (float)pa.X, ay = (float)pa.Y;
        float bx = (float)pb.X, by = (float)pb.Y;
        float cx = (float)pc.X, cy = (float)pc.Y;

        float area = Edge(ax, ay, bx, by, cx, cy);
        if (Math.Abs(area) < 0.0001f)
            return;

        int minX = Math.Clamp((int)MathF.Floor(Math.Min(ax, Math.Min(bx, cx))), 0, width - 1);
        int maxX = Math.Clamp((int)MathF.Ceiling(Math.Max(ax, Math.Max(bx, cx))), 0, width - 1);
        int minY = Math.Clamp((int)MathF.Floor(Math.Min(ay, Math.Min(by, cy))), 0, height - 1);
        int maxY = Math.Clamp((int)MathF.Ceiling(Math.Max(ay, Math.Max(by, cy))), 0, height - 1);
        if (maxX < minX || maxY < minY)
            return;

        Vector3 normal = Vector3.Cross(vb - va, vc - va);
        float normalLengthSquared = normal.LengthSquared();
        if (normalLengthSquared < 0.000001f)
            return;
        normal /= MathF.Sqrt(normalLengthSquared);

        // Absolute dot product keeps both front- and back-facing triangles visible.
        float lighting = Math.Clamp(MathF.Abs(Vector3.Dot(normal, LightDirection)), 0.18f, 1f);
        int shade = 48 + (int)(lighting * 165f);
        int red = Math.Min(255, shade + 18);
        int green = Math.Min(255, shade + 8);
        int blue = shade;
        int color = unchecked((int)(0xFF000000u | ((uint)red << 16) | ((uint)green << 8) | (uint)blue));

        float inverseArea = 1f / area;
        float inverseZa = 1f / za, inverseZb = 1f / zb, inverseZc = 1f / zc;

        for (int y = minY; y <= maxY; y++)
        {
            float py = y + 0.5f;
            int row = y * width;
            for (int x = minX; x <= maxX; x++)
            {
                float px = x + 0.5f;
                float wa = Edge(bx, by, cx, cy, px, py) * inverseArea;
                float wb = Edge(cx, cy, ax, ay, px, py) * inverseArea;
                float wc = 1f - wa - wb;
                if (wa < -0.0001f || wb < -0.0001f || wc < -0.0001f)
                    continue;

                float inverseDepth = wa * inverseZa + wb * inverseZb + wc * inverseZc;
                if (inverseDepth <= 0)
                    continue;

                float depth = 1f / inverseDepth;
                int pixel = row + x;
                if (depth < _depthBuffer[pixel])
                {
                    _depthBuffer[pixel] = depth;
                    _colorBuffer[pixel] = color;
                }
            }
        }

        if (_wireframe)
        {
            const int wireColor = unchecked((int)0xFF17202A);
            DrawDepthLine(pa, pb, za, zb, width, height, wireColor);
            DrawDepthLine(pb, pc, zb, zc, width, height, wireColor);
            DrawDepthLine(pc, pa, zc, za, width, height, wireColor);
        }
    }

    private void DrawDepthLine(Point start, Point end, float startDepth, float endDepth,
        int width, int height, int color)
    {
        float dx = (float)(end.X - start.X);
        float dy = (float)(end.Y - start.Y);
        int steps = Math.Max(1, (int)MathF.Ceiling(Math.Max(Math.Abs(dx), Math.Abs(dy))));
        float inverseStart = 1f / startDepth;
        float inverseEnd = 1f / endDepth;

        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            int x = (int)MathF.Round((float)start.X + dx * t);
            int y = (int)MathF.Round((float)start.Y + dy * t);
            if ((uint)x >= width || (uint)y >= height)
                continue;

            float inverseDepth = inverseStart + (inverseEnd - inverseStart) * t;
            if (inverseDepth <= 0)
                continue;

            float depth = 1f / inverseDepth;
            int pixel = y * width + x;
            if (depth <= _depthBuffer[pixel] + 0.002f)
                _colorBuffer[pixel] = color;
        }
    }

    private static float Edge(float ax, float ay, float bx, float by, float px, float py) =>
        (px - ax) * (by - ay) - (py - ay) * (bx - ax);

    private void EnsureFrameBuffer(int width, int height)
    {
        if (_frameBitmap != null && width == _frameWidth && height == _frameHeight)
            return;

        _frameBitmap?.Dispose();
        _frameWidth = width;
        _frameHeight = height;
        _frameBitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Avalonia.Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);
        _colorBuffer = new int[width * height];
        _depthBuffer = new float[width * height];
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _frameBitmap?.Dispose();
        _frameBitmap = null;
        _colorBuffer = Array.Empty<int>();
        _depthBuffer = Array.Empty<float>();
        _pointers.Clear();
        _gestureStarts.Clear();
        _capturedPointers.Clear();
    }

    private static Point Midpoint(Point a, Point b) =>
        new((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5);

    private static double Distance(Point a, Point b)
    {
        double x = a.X - b.X, y = a.Y - b.Y;
        return Math.Sqrt(x * x + y * y);
    }
}
