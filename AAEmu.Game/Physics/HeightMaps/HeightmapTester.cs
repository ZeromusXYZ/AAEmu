using System;
using AAEmu.Game.Models.ClientData;
using Jitter2.Collision;
using Jitter2.LinearMath;

namespace AAEmu.Game.Physics.HeightMaps;

public class HeightmapTester : IDynamicTreeProxy, IRayCastable
{
    public int SetIndex { get; set; } = -1;
    public int NodePtr { get; set; }
    public Heightmap Heightmap { get; init; }
    public JVector Velocity => JVector.Zero;
    public JBoundingBox WorldBoundingBox { get; init; }
    public JVector WorldBoxSize { get; init; }
    public ulong MinIndex { get; init; }
    public ulong MaxIndex { get; init; }

    public HeightmapTester(Heightmap heightmap)
    {
        Heightmap = heightmap;
        WorldBoundingBox = heightmap.GetBoundingBox();
        WorldBoxSize = new JVector(
            WorldBoundingBox.Max.X - WorldBoundingBox.Min.X,
            WorldBoundingBox.Max.Y - WorldBoundingBox.Min.Y,
            WorldBoundingBox.Max.Z - WorldBoundingBox.Min.Z);
        (MinIndex, MaxIndex) = Jitter2.World.RequestId((int)WorldBoxSize.X * (int)WorldBoxSize.Z);
    }

    /// <summary>
    /// RayCastTriangle helper
    /// </summary>
    private void RayCastTriangle(in JVector origin, in JVector direction, in JVector a, in JVector b, in JVector c, out JVector normal, out float lambda)
    {
        var u = b - a;
        var v = c - a;

        normal = v % u;
        var it = 1.0f / normal.LengthSquared();
        var denominator = JVector.Dot(direction, normal);

        if (Math.Abs(denominator) < 1e-06f)
        {
            // triangle and ray are parallel
            lambda = float.MaxValue;
            normal = JVector.Zero;
            return;
        }

        lambda = JVector.Dot(a - origin, normal);
        if (lambda > 0.0f)
        {
            // ray is pointing away from the triangle
            lambda = float.MaxValue;
            normal = JVector.Zero;
            return;
        }

        lambda /= denominator;

        // point where the ray intersects the plane of the triangle.
        var hitPoint = origin + lambda * direction;
        var at = a - hitPoint;

        JVector.Cross(u, at, out var tmp);
        var gamma = JVector.Dot(tmp, normal) * it;
        JVector.Cross(at, v, out tmp);
        var beta = JVector.Dot(tmp, normal) * it;
        var alpha = 1.0f - gamma - beta;

        if (!(alpha > 0 && beta > 0 && gamma > 0))
        {
            // point is outside the triangle
            normal = JVector.Zero;
            lambda = float.MaxValue;
            return;
        }

        normal *= MathF.Sqrt(it);
    }

    /// <summary>
    /// RayCast helper
    /// </summary>
    public bool RayCast(in JVector origin, in JVector direction, out JVector normal, out float lambda)
    {
        const float MaxDistance = 5000.0f;

        var dirX = direction.X;
        var dirZ = direction.Z;

        var len2 = dirX * dirX + dirZ * dirZ;
        var iLen = 1.0f / MathF.Sqrt(len2);

        dirX *= iLen;
        dirZ *= iLen;

        var x = (int)Math.Floor(origin.X);
        var z = (int)Math.Floor(origin.Z);

        var stepX = dirX > 0 ? 1 : -1;
        var stepZ = dirZ > 0 ? 1 : -1;

        var nextX = dirX > 0 ? x + 1 - origin.X : origin.X - x;
        var nextZ = dirZ > 0 ? z + 1 - origin.Z : origin.Z - z;

        var tMaxX = dirX != 0 ? nextX / Math.Abs(dirX) : float.PositiveInfinity;
        var tMaxZ = dirZ != 0 ? nextZ / Math.Abs(dirZ) : float.PositiveInfinity;

        var tDeltaX = direction.X != 0 ? 1f / Math.Abs(dirX) : float.PositiveInfinity;
        var tDeltaZ = direction.Z != 0 ? 1f / Math.Abs(dirZ) : float.PositiveInfinity;

        var t = 0f;

        while (t <= MaxDistance)
        {
            // check if we are out of bounds
            //if (x < 0 || x + 1 >= Heightmap.Width || z < 0 || z + 1 >= Heightmap.Height)
            if (x < WorldBoundingBox.Min.X || x > WorldBoundingBox.Max.X || WorldBoundingBox.Min.Z < 0 || z > WorldBoundingBox.Max.Z)
                goto continue_walk;

            var ma = Heightmap.GetMaterial(x + 0, z + 0);
            var mb = Heightmap.GetMaterial(x + 1, z + 0);
            var mc = Heightmap.GetMaterial(x + 1, z + 1);
            var md = Heightmap.GetMaterial(x + 0, z + 1);
            var ha = ma == NodeCell.HeightMapMaterialHole;
            var hb = mb == NodeCell.HeightMapMaterialHole;
            var hc = mc == NodeCell.HeightMapMaterialHole;
            var hd = md == NodeCell.HeightMapMaterialHole;
            // check this quad!
            var a = new JVector(x + 0, Heightmap.GetHeight(x + 0, z + 0), z + 0);
            var b = new JVector(x + 1, Heightmap.GetHeight(x + 1, z + 0), z + 0);
            var c = new JVector(x + 1, Heightmap.GetHeight(x + 1, z + 1), z + 1);
            var d = new JVector(x + 0, Heightmap.GetHeight(x + 0, z + 1), z + 1);

            //  A - B
            //  | \ |
            //  D - C
            var normal0 = JVector.Zero;
            var lambda0 = float.MaxValue;
            if (!ha && !hb && !hc)
                RayCastTriangle(origin, direction, a, b, c, out normal0, out lambda0);

            var normal1 = JVector.Zero;
            var lambda1 = float.MaxValue;
            if (!ha && !hc && !hd)
                RayCastTriangle(origin, direction, a, c, d, out normal1, out lambda1);

            if (lambda0 < float.MaxValue || lambda1 < float.MaxValue)
            {
                if (lambda0 <= lambda1)
                {
                    normal = normal0;
                    lambda = lambda0;
                }
                else
                {
                    normal = normal1;
                    lambda = lambda1;
                }

                return true;
            }

            continue_walk:

            if (tMaxX < tMaxZ)
            {
                x += stepX;
                t = tMaxX;
                tMaxX += tDeltaX;
            }
            else
            {
                z += stepZ;
                t = tMaxZ;
                tMaxZ += tDeltaZ;
            }
        }

        normal = JVector.Zero; lambda = 0.0f;
        return false;
    }
}
