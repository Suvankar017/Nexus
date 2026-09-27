using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Provides GLSL/HLSL-style swizzle extension methods for <see cref="Vector2"/>.
    ///
    /// Swizzle methods allow individual components of a Vector2 to be selected,
    /// reordered, and repeated to construct scalar, Vector2, Vector3, or Vector4 values.
    ///
    /// The letters X and Y correspond to the source vector's x and y components
    /// respectively. The order of the letters determines the order of the
    /// resulting components.
    ///
    /// Examples:
    /// <code>
    /// Vector2 v = new Vector2(1, 2);
    ///
    /// v.X()     // 1
    /// v.XY()    // (1, 2)
    /// v.YX()    // (2, 1)
    /// v.XXY()   // (1, 1, 2)
    /// v.XYYX()  // (1, 2, 2, 1)
    /// </code>
    ///
    /// All ordered combinations of X and Y are provided for lengths
    /// one through four, including repeated components.
    /// </summary>
    public static class Vector2SwizzleExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float X(this Vector2 v) => v.x;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Y(this Vector2 v) => v.y;


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 XX(this Vector2 v) => new(v.x, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 XY(this Vector2 v) => new(v.x, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 YX(this Vector2 v) => new(v.y, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 YY(this Vector2 v) => new(v.y, v.y);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 XXX(this Vector2 v) => new(v.x, v.x, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 XXY(this Vector2 v) => new(v.x, v.x, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 XYX(this Vector2 v) => new(v.x, v.y, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 XYY(this Vector2 v) => new(v.x, v.y, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 YXX(this Vector2 v) => new(v.y, v.x, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 YXY(this Vector2 v) => new(v.y, v.x, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 YYX(this Vector2 v) => new(v.y, v.y, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 YYY(this Vector2 v) => new(v.y, v.y, v.y);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XXXX(this Vector2 v) => new(v.x, v.x, v.x, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XXXY(this Vector2 v) => new(v.x, v.x, v.x, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XXYX(this Vector2 v) => new(v.x, v.x, v.y, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XXYY(this Vector2 v) => new(v.x, v.x, v.y, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XYXX(this Vector2 v) => new(v.x, v.y, v.x, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XYXY(this Vector2 v) => new(v.x, v.y, v.x, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XYYX(this Vector2 v) => new(v.x, v.y, v.y, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XYYY(this Vector2 v) => new(v.x, v.y, v.y, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YXXX(this Vector2 v) => new(v.y, v.x, v.x, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YXXY(this Vector2 v) => new(v.y, v.x, v.x, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YXYX(this Vector2 v) => new(v.y, v.x, v.y, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YXYY(this Vector2 v) => new(v.y, v.x, v.y, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YYXX(this Vector2 v) => new(v.y, v.y, v.x, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YYXY(this Vector2 v) => new(v.y, v.y, v.x, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YYYX(this Vector2 v) => new(v.y, v.y, v.y, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YYYY(this Vector2 v) => new(v.y, v.y, v.y, v.y);
    }
}