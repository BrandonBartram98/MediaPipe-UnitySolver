using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class VectorExtensions
{
    public static float Find2DAngle(float cx, float cy, float ex, float ey)
    {
        var dy = ey - cy;
        var dx = ex - cx;
        return MathF.Atan2(dy, dx);
    }

    public static Vector2 Unit(Vector2 vector) => vector / vector.magnitude;
    public static Vector3 Unit(Vector3 vector) => vector / vector.magnitude;

    public static float Remap(this float val, float min, float max) => (Math.Clamp(val, min, max) - min) / (max - min);

    public static float NormalizeRadians(float radians)
    {
        if (radians >= MathF.PI / 2)
            radians -= 2 * MathF.PI;

        if (radians <= -MathF.PI / 2)
        {
            radians += 2 * MathF.PI;
            radians = MathF.PI - radians;
        }

        return radians / MathF.PI;
    }

    public static float NormalizeAngle(this float radians)
    {
        var twoPi = MathF.PI * 2;
        var angle = radians % twoPi;
        angle = angle > MathF.PI ? angle - twoPi : angle < -MathF.PI ? twoPi + angle : angle;
        return angle / MathF.PI;
    }

    public static Vector3 FindRotation(Vector3 vector, Vector3 other, bool normalize)
    {
        Vector3 result = new(
            Find2DAngle(vector.z, vector.x, other.z, other.x),
            Find2DAngle(vector.z, vector.y, other.z, other.y),
            Find2DAngle(vector.x, vector.y, other.x, other.y)
         );

        if (normalize)
        {
            return result.normalized;
        }

        return result;
    }

    /// Find 2D angle between 3 points in 3D space. Returns a single angle normalized to 0, 1
    public static float AngleBetween3DCoords(Vector3 a, Vector3 b, Vector3 c)
    {
        var vec1 = a - b;
        var vec2 = c - b;

        var vec1Norm = Unit(vec1);
        var vec2Norm = Unit(vec2);

        var dotProducts = Vector3.Dot(vec1Norm, vec2Norm);
        var angle = MathF.Acos(dotProducts);

        return NormalizeRadians(angle);
    }

    public static Vector3 RollPitchYaw(Vector3 a, Vector3 b, Vector3? c = null)
    {
        if (c == null)
        {
            return new Vector3(
                Find2DAngle(a.z, a.y, b.z, b.y).NormalizeAngle(),
                Find2DAngle(a.z, a.x, b.z, b.x).NormalizeAngle(),
                Find2DAngle(a.x, a.y, b.x, b.y).NormalizeAngle()
                );
        }

        var qb = b - a;
        var qc = (Vector3)(c - a);
        var n = Vector3.Cross(qb, qc);

        var unitZ = Unit(n);
        var unitX = Unit(qb);
        var unitY = Vector3.Cross(unitZ, unitX);

        var beta = MathF.Asin(unitZ.x);
        var alpha = MathF.Atan2(-unitZ.y, unitZ.z);
        var gamma = MathF.Atan2(-unitY.x, unitX.x);

        return new Vector3(alpha.NormalizeAngle(), beta.NormalizeAngle(), gamma.NormalizeAngle());
    }
}
