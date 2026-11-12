using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AssetsTools.NET;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

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
                if (topology != 0)
                    continue;

                int firstByte = ReadInt(Child(subMesh, "firstByte"), 0);
                int indexCount = ReadInt(Child(subMesh, "indexCount"), 0);
                int baseVertex = ReadInt(Child(subMesh, "baseVertex"), 0);
                AppendIndexRange(indexBytes, firstByte, indexCount, indexSize, baseVertex, vertexCount, indices);
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

        int usable = Math.Min(indices.Count - indices.Count % 3, 75_000);
        return usable > 0 ? indices.Take(usable).ToArray() : Array.Empty<int>();
    }

    private static void AppendIndexRange(
        byte[] bytes, int firstByte, int count, int indexSize, int baseVertex,
        int vertexCount, List<int> output)
    {
        if (firstByte < 0 || count <= 0 || firstByte > bytes.Length ||
            (long)firstByte + (long)count * indexSize > bytes.Length)
            return;

        int safeCount = Math.Min(count, 75_000 - output.Count);
        for (int i = 0; i < safeCount; i++)
        {
            int offset = firstByte + i * indexSize;
            int value = indexSize == 4
                ? BitConverter.ToInt32(bytes, offset)
                : BitConverter.ToUInt16(bytes, offset);
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

internal sealed class MeshPreviewControl : Control
{
    private readonly Dictionary<IPointer, Point> _pointers = new();
    private readonly Dictionary<IPointer, Point> _gestureStarts = new();
    private readonly HashSet<IPointer> _capturedPointers = new();
    private readonly bool _allowVerticalDrag;
    private readonly Vector3[] _vertices;
    private readonly int[] _indices;
    private double _yaw = -0.55;
    private double _pitch = 0.25;
    private double _zoom = 1.0;
    private bool _wireframe;
    private double _lastPinchDistance;
    private Point _lastPinchCenter;

    public MeshPreviewControl(Vector3[] vertices, int[] indices, bool allowVerticalDrag = false)
    {
        _vertices = vertices ?? Array.Empty<Vector3>();
        _indices = indices ?? Array.Empty<int>();
        _allowVerticalDrag = allowVerticalDrag;
        MinHeight = 300;
        Height = 360;
        ClipToBounds = true;
        Focusable = true;
    }

    public void ResetView()
    {
        _yaw = -0.55;
        _pitch = 0.25;
        _zoom = 1.0;
        InvalidateVisual();
    }

    public void ToggleWireframe()
    {
        _wireframe = !_wireframe;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
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
            var pair = _pointers.Values.Take(2).ToArray();
            _lastPinchDistance = Distance(pair[0], pair[1]);
            _lastPinchCenter = Midpoint(pair[0], pair[1]);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_pointers.TryGetValue(e.Pointer, out Point previous))
            return;

        Point current = e.GetPosition(this);
        _pointers[e.Pointer] = current;

        if (_pointers.Count >= 2)
        {
            var pair = _pointers.Values.Take(2).ToArray();
            double distance = Distance(pair[0], pair[1]);
            Point center = Midpoint(pair[0], pair[1]);

            if (_lastPinchDistance > 0)
                _zoom = Math.Clamp(_zoom * (distance / _lastPinchDistance), 0.35, 4.0);

            double centerDx = center.X - _lastPinchCenter.X;
            double centerDy = center.Y - _lastPinchCenter.Y;
            _yaw += centerDx * 0.009;
            _pitch = Math.Clamp(_pitch + centerDy * 0.009, -1.45, 1.45);
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
            bool horizontalIntent = Math.Abs(dx) >= 7 && Math.Abs(dx) >= Math.Abs(dy) * 1.10;
            bool verticalIntent = Math.Abs(dy) >= 7 && Math.Abs(dy) > Math.Abs(dx) * 1.10;

            if (!horizontalIntent && !(_allowVerticalDrag && verticalIntent))
                return;

            e.Pointer.Capture(this);
            _capturedPointers.Add(e.Pointer);
        }

        double deltaX = current.X - previous.X;
        double deltaY = current.Y - previous.Y;
        _yaw += deltaX * 0.009;
        if (_allowVerticalDrag)
            _pitch = Math.Clamp(_pitch + deltaY * 0.009, -1.45, 1.45);

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _pointers.Remove(e.Pointer);
        _gestureStarts.Remove(e.Pointer);
        bool wasCaptured = _capturedPointers.Remove(e.Pointer);
        if (wasCaptured)
            e.Pointer.Capture(null);

        _lastPinchDistance = 0;
        if (_pointers.Count == 1)
        {
            var remaining = _pointers.Keys.First();
            _gestureStarts[remaining] = _pointers[remaining];
        }
        if (wasCaptured)
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
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(12, 12, 12)), bounds);

        if (_vertices.Length < 3 || _indices.Length < 3 || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        Vector3 min = _vertices[0];
        Vector3 max = _vertices[0];
        foreach (var vertex in _vertices)
        {
            min = Vector3.Min(min, vertex);
            max = Vector3.Max(max, vertex);
        }

        Vector3 center = (min + max) * 0.5f;
        float maxDimension = Math.Max(max.X - min.X, Math.Max(max.Y - min.Y, max.Z - min.Z));
        if (maxDimension < 0.00001f)
            maxDimension = 1f;
        float normalize = 2.2f / maxDimension;

        double cy = Math.Cos(_yaw);
        double sy = Math.Sin(_yaw);
        double cp = Math.Cos(_pitch);
        double sp = Math.Sin(_pitch);

        var projected = new (Point Point, float Depth, Vector3 Camera)[_vertices.Length];
        double focal = Math.Min(Bounds.Width, Bounds.Height) * 0.95;
        double cameraDistance = 4.4 / _zoom;

        for (int i = 0; i < _vertices.Length; i++)
        {
            Vector3 p = (_vertices[i] - center) * normalize;
            float rx = (float)(p.X * cy - p.Z * sy);
            float rz = (float)(p.X * sy + p.Z * cy);
            float ry = (float)(p.Y * cp - rz * sp);
            rz = (float)(p.Y * sp + rz * cp);

            double depth = cameraDistance + rz;
            if (depth < 0.1) depth = 0.1;
            var point = new Point(
                Bounds.Width * 0.5 + rx * focal / depth,
                Bounds.Height * 0.5 - ry * focal / depth);
            projected[i] = (point, rz, new Vector3(rx, ry, rz));
        }

        var triangles = new List<(int A, int B, int C, float Depth)>();
        for (int i = 0; i + 2 < _indices.Length; i += 3)
        {
            int a = _indices[i], b = _indices[i + 1], c = _indices[i + 2];
            if ((uint)a >= projected.Length || (uint)b >= projected.Length || (uint)c >= projected.Length)
                continue;
            float depth = (projected[a].Depth + projected[b].Depth + projected[c].Depth) / 3f;
            triangles.Add((a, b, c, depth));
        }

        foreach (var triangle in triangles.OrderByDescending(t => t.Depth))
        {
            Point a = projected[triangle.A].Point;
            Point b = projected[triangle.B].Point;
            Point c = projected[triangle.C].Point;

            Vector3 va = projected[triangle.A].Camera;
            Vector3 vb = projected[triangle.B].Camera;
            Vector3 vc = projected[triangle.C].Camera;
            Vector3 normal = Vector3.Cross(vb - va, vc - va);
            if (normal.LengthSquared() < 0.000001f)
                continue;
            normal = Vector3.Normalize(normal);
            float light = Math.Clamp(Math.Abs(Vector3.Dot(normal, Vector3.Normalize(new Vector3(-0.35f, 0.65f, 0.7f)))), 0.18f, 1f);
            byte shade = (byte)(48 + light * 165);
            var fill = new SolidColorBrush(Color.FromRgb(shade, (byte)Math.Min(255, shade + 8), (byte)Math.Min(255, shade + 18)));
            var pen = _wireframe
                ? new Pen(new SolidColorBrush(Color.FromRgb(12, 16, 22)), 1.0)
                : null;

            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                g.BeginFigure(a, true);
                g.LineTo(b);
                g.LineTo(c);
                g.EndFigure(true);
            }
            context.DrawGeometry(_wireframe ? null : fill, pen, geometry);
        }
    }

    private static Point Midpoint(Point a, Point b) =>
        new((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5);

    private static double Distance(Point a, Point b)
    {
        double x = a.X - b.X;
        double y = a.Y - b.Y;
        return Math.Sqrt(x * x + y * y);
    }
}
