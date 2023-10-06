using BulletSharp.Math;

namespace BulletSharp
{
    public interface ICharacterController : IAction
    {
        void Reset(CollisionWorld collisionWorld);
        void Warp(ref Vector3 origin);

        void PreStep(CollisionWorld collisionWorld);

        void SetUpInterpolate(bool value);
    }
}
