using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using BulletSharp;

namespace Stride.Physics
{
    public static class BulletExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BulletSharp.Math.Vector3 FromXenko(this BulletSharp.Math.Vector3 x) => x * 10;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BulletSharp.Math.Vector3 ToXenko(this BulletSharp.Math.Vector3 x) => (x * 0.1f);
    }
}
