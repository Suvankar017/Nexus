using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Provides GLSL/HLSL-style swizzle extension methods for <see cref="Vector3"/>.
    ///
    /// Swizzle methods allow individual components of a Vector3 to be selected,
    /// reordered, and repeated to construct scalar, Vector2, Vector3, or Vector4 values.
    ///
    /// The letters X, Y, and Z correspond to the source vector's x, y, and z
    /// components respectively. The order of the letters determines the order
    /// of the resulting components.
    ///
    /// Examples:
    /// <code>
    /// Vector3 v = new Vector3(1, 2, 3);
    ///
    /// v.X()     // 1
    /// v.XY()    // (1, 2)
    /// v.ZYX()   // (3, 2, 1)
    /// v.XXZ()   // (1, 1, 3)
    /// v.XYZZ()  // (1, 2, 3, 3)
    /// </code>
    ///
    /// All ordered combinations of X, Y, and Z are provided for lengths
    /// one through four, including repeated components.
    /// </summary>
    public static class Vector3SwizzleExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float X(this Vector3 v) => v.x;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Y(this Vector3 v) => v.y;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Z(this Vector3 v) => v.z;


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 XX(this Vector3 v) => new(v.x, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 XY(this Vector3 v) => new(v.x, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 XZ(this Vector3 v) => new(v.x, v.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 YX(this Vector3 v) => new(v.y, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 YY(this Vector3 v) => new(v.y, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 YZ(this Vector3 v) => new(v.y, v.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 ZX(this Vector3 v) => new(v.z, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 ZY(this Vector3 v) => new(v.z, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 ZZ(this Vector3 v) => new(v.z, v.z);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 XXX(this Vector3 v) => new(v.x, v.x, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 XXY(this Vector3 v) => new(v.x, v.x, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 XXZ(this Vector3 v) => new(v.x, v.x, v.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 XYX(this Vector3 v) => new(v.x, v.y, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 XYY(this Vector3 v) => new(v.x, v.y, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 XYZ(this Vector3 v) => new(v.x, v.y, v.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 XZX(this Vector3 v) => new(v.x, v.z, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 XZY(this Vector3 v) => new(v.x, v.z, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 XZZ(this Vector3 v) => new(v.x, v.z, v.z);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 YXX(this Vector3 v) => new(v.y, v.x, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 YXY(this Vector3 v) => new(v.y, v.x, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 YXZ(this Vector3 v) => new(v.y, v.x, v.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 YYX(this Vector3 v) => new(v.y, v.y, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 YYY(this Vector3 v) => new(v.y, v.y, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 YYZ(this Vector3 v) => new(v.y, v.y, v.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 YZX(this Vector3 v) => new(v.y, v.z, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 YZY(this Vector3 v) => new(v.y, v.z, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 YZZ(this Vector3 v) => new(v.y, v.z, v.z);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ZXX(this Vector3 v) => new(v.z, v.x, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ZXY(this Vector3 v) => new(v.z, v.x, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ZXZ(this Vector3 v) => new(v.z, v.x, v.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ZYX(this Vector3 v) => new(v.z, v.y, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ZYY(this Vector3 v) => new(v.z, v.y, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ZYZ(this Vector3 v) => new(v.z, v.y, v.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ZZX(this Vector3 v) => new(v.z, v.z, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ZZY(this Vector3 v) => new(v.z, v.z, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ZZZ(this Vector3 v) => new(v.z, v.z, v.z);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XXXX(this Vector3 v) => new(v.x, v.x, v.x, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XXXY(this Vector3 v) => new(v.x, v.x, v.x, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XXXZ(this Vector3 v) => new(v.x, v.x, v.x, v.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XXYX(this Vector3 v) => new(v.x, v.x, v.y, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XXYY(this Vector3 v) => new(v.x, v.x, v.y, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XXYZ(this Vector3 v) => new(v.x, v.x, v.y, v.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XXZX(this Vector3 v) => new(v.x, v.x, v.z, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XXZY(this Vector3 v) => new(v.x, v.x, v.z, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XXZZ(this Vector3 v) => new(v.x, v.x, v.z, v.z);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XYXX(this Vector3 v) => new(v.x, v.y, v.x, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XYXY(this Vector3 v) => new(v.x, v.y, v.x, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XYXZ(this Vector3 v) => new(v.x, v.y, v.x, v.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XYYX(this Vector3 v) => new(v.x, v.y, v.y, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XYYY(this Vector3 v) => new(v.x, v.y, v.y, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XYYZ(this Vector3 v) => new(v.x, v.y, v.y, v.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XYZX(this Vector3 v) => new(v.x, v.y, v.z, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XYZY(this Vector3 v) => new(v.x, v.y, v.z, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XYZZ(this Vector3 v) => new(v.x, v.y, v.z, v.z);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XZXX(this Vector3 v) => new(v.x, v.z, v.x, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XZXY(this Vector3 v) => new(v.x, v.z, v.x, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XZXZ(this Vector3 v) => new(v.x, v.z, v.x, v.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XZYX(this Vector3 v) => new(v.x, v.z, v.y, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XZYY(this Vector3 v) => new(v.x, v.z, v.y, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XZYZ(this Vector3 v) => new(v.x, v.z, v.y, v.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XZZX(this Vector3 v) => new(v.x, v.z, v.z, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XZZY(this Vector3 v) => new(v.x, v.z, v.z, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 XZZZ(this Vector3 v) => new(v.x, v.z, v.z, v.z);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YXXX(this Vector3 v) => new(v.y, v.x, v.x, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YXXY(this Vector3 v) => new(v.y, v.x, v.x, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YXXZ(this Vector3 v) => new(v.y, v.x, v.x, v.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YXYX(this Vector3 v) => new(v.y, v.x, v.y, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YXYY(this Vector3 v) => new(v.y, v.x, v.y, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YXYZ(this Vector3 v) => new(v.y, v.x, v.y, v.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YXZX(this Vector3 v) => new(v.y, v.x, v.z, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YXZY(this Vector3 v) => new(v.y, v.x, v.z, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YXZZ(this Vector3 v) => new(v.y, v.x, v.z, v.z);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YYXX(this Vector3 v) => new(v.y, v.y, v.x, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YYXY(this Vector3 v) => new(v.y, v.y, v.x, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YYXZ(this Vector3 v) => new(v.y, v.y, v.x, v.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YYYX(this Vector3 v) => new(v.y, v.y, v.y, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YYYY(this Vector3 v) => new(v.y, v.y, v.y, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YYYZ(this Vector3 v) => new(v.y, v.y, v.y, v.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YYZX(this Vector3 v) => new(v.y, v.y, v.z, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YYZY(this Vector3 v) => new(v.y, v.y, v.z, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YYZZ(this Vector3 v) => new(v.y, v.y, v.z, v.z);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YZXX(this Vector3 v) => new(v.y, v.z, v.x, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YZXY(this Vector3 v) => new(v.y, v.z, v.x, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YZXZ(this Vector3 v) => new(v.y, v.z, v.x, v.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YZYX(this Vector3 v) => new(v.y, v.z, v.y, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YZYY(this Vector3 v) => new(v.y, v.z, v.y, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YZYZ(this Vector3 v) => new(v.y, v.z, v.y, v.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YZZX(this Vector3 v) => new(v.y, v.z, v.z, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YZZY(this Vector3 v) => new(v.y, v.z, v.z, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 YZZZ(this Vector3 v) => new(v.y, v.z, v.z, v.z);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ZXXX(this Vector3 v) => new(v.z, v.x, v.x, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ZXXY(this Vector3 v) => new(v.z, v.x, v.x, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ZXXZ(this Vector3 v) => new(v.z, v.x, v.x, v.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ZXYX(this Vector3 v) => new(v.z, v.x, v.y, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ZXYY(this Vector3 v) => new(v.z, v.x, v.y, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ZXYZ(this Vector3 v) => new(v.z, v.x, v.y, v.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ZXZX(this Vector3 v) => new(v.z, v.x, v.z, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ZXZY(this Vector3 v) => new(v.z, v.x, v.z, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ZXZZ(this Vector3 v) => new(v.z, v.x, v.z, v.z);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ZYXX(this Vector3 v) => new(v.z, v.y, v.x, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ZYXY(this Vector3 v) => new(v.z, v.y, v.x, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ZYXZ(this Vector3 v) => new(v.z, v.y, v.x, v.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ZYYX(this Vector3 v) => new(v.z, v.y, v.y, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ZYYY(this Vector3 v) => new(v.z, v.y, v.y, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ZYYZ(this Vector3 v) => new(v.z, v.y, v.y, v.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ZYZX(this Vector3 v) => new(v.z, v.y, v.z, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ZYZY(this Vector3 v) => new(v.z, v.y, v.z, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ZYZZ(this Vector3 v) => new(v.z, v.y, v.z, v.z);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ZZXX(this Vector3 v) => new(v.z, v.z, v.x, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ZZXY(this Vector3 v) => new(v.z, v.z, v.x, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ZZXZ(this Vector3 v) => new(v.z, v.z, v.x, v.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ZZYX(this Vector3 v) => new(v.z, v.z, v.y, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ZZYY(this Vector3 v) => new(v.z, v.z, v.y, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ZZYZ(this Vector3 v) => new(v.z, v.z, v.y, v.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ZZZX(this Vector3 v) => new(v.z, v.z, v.z, v.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ZZZY(this Vector3 v) => new(v.z, v.z, v.z, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ZZZZ(this Vector3 v) => new(v.z, v.z, v.z, v.z);
    }
}