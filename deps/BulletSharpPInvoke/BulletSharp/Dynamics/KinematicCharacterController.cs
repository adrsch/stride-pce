using BulletSharp.Math;
using System;

namespace BulletSharp
{
    public interface ICharacterMovement
    {
        void OnPhysicsUpdate(float dt);
        void OnReset();
    }
    public struct CharacterSweepCallback
    {
        public bool Succeeded;
        public Vector3 Point;
        public Vector3 Normal;
        public float HitFraction;
    }
    public class KinematicCharacterController : ICharacterController, IDisposable
    {
        protected float m_halfHeight;

        protected PairCachingGhostObject m_ghostObject;
        protected ConvexShape m_convexShape; //is also in m_ghostObject, but it needs to be convex, so we store it here to avoid upcast

        protected float m_maxPenetrationDepth;
        protected float m_verticalVelocity;
        protected float m_verticalOffset;

        protected float m_maxSlopeRadians; // Slope angle that is set (used for returning the exact value)
        protected float m_maxSlopeCosine;  // Cosine equivalent of m_maxSlopeRadians (calculated once when set, for optimization)
        protected float m_gravity;

        protected float m_turnAngle;

        protected float m_addedMargin; //@to do: remove this and fix the code

        ///this is the desired walk direction, set by the user
        protected Vector3 m_walkDirection;
        protected Vector3 m_normalizedDirection;
        protected Vector3 m_AngVel;

        //some internal variables
        protected Vector3 m_currentPosition;
        protected float m_currentStepOffset;
        protected Vector3 m_targetPosition;

        protected Quaternion m_currentOrientation;
        protected Quaternion m_targetOrientation;

        ///keep track of the contact manifolds
        protected AlignedManifoldArray m_manifoldArray = new AlignedManifoldArray();

        protected bool m_touchingContact;
        protected Vector3 m_touchingNormal;

        protected float m_linearDamping;
        protected float m_angularDamping;

        protected bool m_useGhostObjectSweepTest;
        protected bool m_useWalkDirection;
        protected float m_velocityTimeInterval;
        protected Vector3 m_up;

        protected bool m_interpolateUp;
        protected bool full_drop;
        protected bool bounce_fix;

        protected static Vector3 GetNormalizedVector(ref Vector3 v)
        {
            if (v.Length < MathUtil.SIMD_EPSILON)
            {
                return Vector3.Zero;
            }
            return Vector3.Normalize(v);
        }

        protected Vector3 ComputeReflectionDirection(ref Vector3 direction, ref Vector3 normal)
        {
            float dot;
            Vector3.Dot(ref direction, ref normal, out dot);
            return direction - (2.0f * dot) * normal;
        }

        protected Vector3 ParallelComponent(ref Vector3 direction, ref Vector3 normal)
        {
            float magnitude;
            Vector3.Dot(ref direction, ref normal, out magnitude);
            return normal * magnitude;
        }

        protected Vector3 PerpindicularComponent(ref Vector3 direction, ref Vector3 normal)
        {
            return direction - ParallelComponent(ref direction, ref normal);
        }

        protected bool DoRecoverFromPenetration(CollisionWorld collisionWorld)
        {
            // Here we must refresh the overlapping paircache as the penetrating movement itself or the
            // previous recovery iteration might have used setWorldTransform and pushed us into an object
            // that is not in the previous cache contents from the last timestep, as will happen if we
            // are pushed into a new AABB overlap. Unhandled this means the next convex sweep gets stuck.
            //
            // Do this by calling the broadphase's setAabb with the moved AABB, this will update the broadphase
            // paircache and the ghostobject's internal paircache at the same time.    /BW

            Vector3 minAabb, maxAabb;
            m_convexShape.GetAabb(m_ghostObject.WorldTransform, out minAabb, out maxAabb);
            collisionWorld.Broadphase.SetAabbRef(m_ghostObject.BroadphaseHandle,
                         ref minAabb,
                         ref maxAabb,
                         collisionWorld.Dispatcher);

            bool penetration = false;

            collisionWorld.Dispatcher.DispatchAllCollisionPairs(m_ghostObject.OverlappingPairCache, collisionWorld.DispatchInfo, collisionWorld.Dispatcher);

            m_currentPosition = m_ghostObject.WorldTransform.Origin;

            //  btScalar maxPen = btScalar(0.0);
            for (int i = 0; i < m_ghostObject.OverlappingPairCache.NumOverlappingPairs; i++)
            {
                m_manifoldArray.Clear();

                BroadphasePair collisionPair = m_ghostObject.OverlappingPairCache.OverlappingPairArray[i];

                CollisionObject obj0 = collisionPair.Proxy0.ClientObject as CollisionObject;
                CollisionObject obj1 = collisionPair.Proxy1.ClientObject as CollisionObject;

                if ((obj0 != null && !obj0.HasContactResponse) || (obj1 != null && !obj1.HasContactResponse))
                    continue;

                if (!NeedsCollision(obj0, obj1))
                    continue;

                if (collisionPair.Algorithm != null)
                    collisionPair.Algorithm.GetAllContactManifolds(m_manifoldArray);

                for (int j = 0; j < m_manifoldArray.Count; j++)
                {
                    PersistentManifold manifold = m_manifoldArray[j];
                    float directionSign = manifold.Body0 == m_ghostObject ? -1.0f : 1.0f;
                    for (int p = 0; p < manifold.NumContacts; p++)
                    {
                        ManifoldPoint pt = manifold.GetContactPoint(p);

                        float dist = pt.m_distance1;

                        if (dist < -m_maxPenetrationDepth)
                        {
                            // to do: cause problems on slopes, not sure if it is needed
                            //if (dist < maxPen)
                            //{
                            //  maxPen = dist;
                            //  m_touchingNormal = pt.m_normalWorldOnB * directionSign;//??

                            //}
                            m_currentPosition += pt.m_normalWorldOnB.value * directionSign * dist * 0.2f;
                            penetration = true;
                        }
                        else
                        {
                            //System.Console.WriteLine("touching " + dist);
                        }
                    }

                    //manifold.ClearManifold();
                }
            }
            Matrix newTrans = m_ghostObject.WorldTransform;
            newTrans.Origin = m_currentPosition;
            m_ghostObject.WorldTransform = newTrans;
            //System.Console.WriteLine("m_touchingNormal = " + m_touchingNormal);
            return penetration;
        }

        protected void UpdateTargetPositionBasedOnCollision(ref Vector3 hitNormal, float tangentMag = 0f, float normalMag = 1f)
        {
            Vector3 movementDirection = m_targetPosition - m_currentPosition;
            float movementLength = movementDirection.Length;
            if (movementLength > MathUtil.SIMD_EPSILON)
            {
                movementDirection.Normalize();

                Vector3 reflectDir = ComputeReflectionDirection(ref movementDirection, ref hitNormal);
                reflectDir.Normalize();

                Vector3 parallelDir, perpindicularDir;

                parallelDir = ParallelComponent(ref reflectDir, ref hitNormal);
                perpindicularDir = PerpindicularComponent(ref reflectDir, ref hitNormal);

                m_targetPosition = m_currentPosition;
                if (false) //tangentMag != 0.0)
                {
                    //Vector3 parComponent = parallelDir * (tangentMag * movementLength);
                    //System.Console.WriteLine("parComponent=" + parComponent);
                    //m_targetPosition += parComponent;
                }

                if (normalMag != 0.0f)
                {
                    Vector3 perpComponent = perpindicularDir * (normalMag * movementLength);
                    //System.Console.WriteLine("perpComponent=" + perpComponent);
                    m_targetPosition += perpComponent;
                }
            }
            else
            {
                //System.Console.WriteLine("movementLength don't normalize a zero vector");
            }
        }

        protected void StepForwardAndStrafe(CollisionWorld collisionWorld, ref Vector3 walkMove)
        {
            //System.Console.WriteLine("m_normalizedDirection=" + m_normalizedDirection);
            // phase 2: forward and strafe
            Matrix start = Matrix.Identity;
            Matrix end = Matrix.Identity;

            m_targetPosition = m_currentPosition + walkMove;

            float fraction = 1.0f;
            float distance2 = (m_currentPosition - m_targetPosition).LengthSquared;
            //System.Console.WriteLine("distance2=" + distance2);

            int maxIter = 10;

            while (fraction > 0.01f && maxIter-- > 0)
            {
                start.Origin = m_currentPosition;
                end.Origin = m_targetPosition;
                Vector3 sweepDirNegative = m_currentPosition - m_targetPosition;

                start.SetRotation(m_currentOrientation, out start);
                end.SetRotation(m_targetOrientation, out end);

                using (KinematicClosestNotMeConvexResultCallback callback = new KinematicClosestNotMeConvexResultCallback(m_ghostObject, sweepDirNegative, 0.0f))
                {
                    callback.CollisionFilterGroup = GhostObject.BroadphaseHandle.CollisionFilterGroup;
                    callback.CollisionFilterMask = GhostObject.BroadphaseHandle.CollisionFilterMask;

                    float margin = m_convexShape.Margin;
                    m_convexShape.Margin = margin + m_addedMargin;

                    if (start != end)
                    {
                        if (m_useGhostObjectSweepTest)
                        {
                            m_ghostObject.ConvexSweepTest(m_convexShape, start, end, callback, collisionWorld.DispatchInfo.AllowedCcdPenetration);
                        }
                        else
                        {
                            collisionWorld.ConvexSweepTest(m_convexShape, start, end, callback, collisionWorld.DispatchInfo.AllowedCcdPenetration);
                        }
                    }
                    m_convexShape.Margin = margin;

                    fraction -= callback.ClosestHitFraction;

                    if (callback.HasHit && GhostObject.HasContactResponse && NeedsCollision(m_ghostObject, callback.HitCollisionObject))
                    {
                        // we moved only a fraction
                        //float hitDistance = (callback.HitPointWorld - m_currentPosition).Length;

                        //Vector3.Lerp(ref m_currentPosition, ref m_targetPosition, callback.ClosestHitFraction, out m_currentPosition);
                        Vector3 hitNormalWorld = callback.HitNormalWorld;
                        UpdateTargetPositionBasedOnCollision(ref hitNormalWorld);
                        Vector3 currentDir = m_targetPosition - m_currentPosition;
                        distance2 = currentDir.LengthSquared;
                        if (distance2 > MathUtil.SIMD_EPSILON)
                        {
                            currentDir.Normalize();
                            /* See Quake2: "If velocity is against original velocity, stop ead to avoid tiny oscilations in sloping corners." */
                            if (currentDir.Dot(m_normalizedDirection) <= 0.0f)
                            {
                                break;
                            }
                        }
                        else
                        {
                            //System.Console.WriteLine("currentDir: don't normalize a zero vector");
                            break;
                        }
                    }
                    else
                    {
                        m_currentPosition = m_targetPosition;
                    }
                }
            }
        }


        protected virtual bool NeedsCollision(CollisionObject body0, CollisionObject body1)
        {
            bool collides = (body0.BroadphaseHandle.CollisionFilterGroup & body1.BroadphaseHandle.CollisionFilterMask) != 0;
            collides = collides && (body1.BroadphaseHandle.CollisionFilterGroup & body0.BroadphaseHandle.CollisionFilterMask) != 0;
            return collides;
        }


        protected void SetUpVector(ref Vector3 up)
        {
            if (m_up == up)
                return;

            Vector3 u = m_up;

            if (up.LengthSquared > 0)
                m_up = Vector3.Normalize(up);
            else
                m_up = Vector3.Zero;

            if (m_ghostObject == null) return;
            Quaternion rot = GetRotation(ref m_up, ref u);

            //set orientation with new up
            Matrix xform;
            xform = m_ghostObject.WorldTransform;
            Quaternion orn = rot.Inverse * xform.GetRotation();
            xform.SetRotation(orn, out xform);
            m_ghostObject.WorldTransform = xform;
        }


        protected Quaternion GetRotation(ref Vector3 v0, ref Vector3 v1)
        {
            if (v0.LengthSquared == 0.0f || v1.LengthSquared == 0.0f)
            {
                Quaternion q = new Quaternion();
                return q;
            }

            return MathUtil.ShortestArcQuat(ref v0, ref v1);
        }

        public KinematicCharacterController(PairCachingGhostObject ghostObject, ConvexShape convexShape, float stepHeight, ref Vector3 up)
        {
            m_ghostObject = ghostObject;
            m_addedMargin = 0.02f;
            m_useGhostObjectSweepTest = true;
            m_convexShape = convexShape;
            m_useWalkDirection = true; // use walk direction by default, legacy behavior
            m_gravity = 9.8f * 3.0f; // 3G acceleration.
            m_interpolateUp = true;
            m_maxPenetrationDepth = 0.2f;
        }

        public void SetConvexShape(PairCachingGhostObject ghostObject)
        {
            m_ghostObject = ghostObject;
        }

        public void SetConvexShape(ConvexShape convexShape)
        {
            m_convexShape = convexShape;
        }

        ICharacterMovement CharacterMovement;
        public void SetCharacterMovement(ICharacterMovement vu)
        {
            CharacterMovement = vu;
        }

        Vector3 m_currentVelocity;
        // IAction interface
        CollisionWorld LastWorld;
        public virtual void UpdateAction(CollisionWorld collisionWorld, float deltaTime)
        {
            LastWorld = collisionWorld;

            if (CharacterMovement == null)
                return;

            PreStep(collisionWorld);

            CharacterMovement.OnPhysicsUpdate(deltaTime);

            Matrix xform = m_ghostObject.WorldTransform;
            xform.Origin = m_currentPosition;
            m_ghostObject.WorldTransform = xform;
        }


        public CharacterSweepCallback DoSweep(Vector3 startPosition, Vector3 endPosition)
        {
            CharacterSweepCallback res = new CharacterSweepCallback();
            Matrix start = Matrix.Identity;
            Matrix end = Matrix.Identity;

            float fraction = 1.0f;

            start.Origin = startPosition;
            end.Origin = endPosition;
            Vector3 sweepDirNegative = startPosition - endPosition;

            start.SetRotation(m_currentOrientation, out start);
            end.SetRotation(m_targetOrientation, out end);

            using (KinematicClosestNotMeConvexResultCallback callback = new KinematicClosestNotMeConvexResultCallback(m_ghostObject, sweepDirNegative, 0.0f))
            {
                callback.CollisionFilterGroup = GhostObject.BroadphaseHandle.CollisionFilterGroup;
                callback.CollisionFilterMask = GhostObject.BroadphaseHandle.CollisionFilterMask;

                float margin = m_convexShape.Margin;
                m_convexShape.Margin = margin + m_addedMargin;

                if (start != end)
                {
                    if (m_useGhostObjectSweepTest)
                    {
                        m_ghostObject.ConvexSweepTest(m_convexShape, start, end, callback, LastWorld.DispatchInfo.AllowedCcdPenetration);
                    }
                    else
                    {
                        LastWorld.ConvexSweepTest(m_convexShape, start, end, callback, LastWorld.DispatchInfo.AllowedCcdPenetration);
                    }
                }
                m_convexShape.Margin = margin;

                fraction -= callback.ClosestHitFraction;

                if (callback.HasHit && GhostObject.HasContactResponse && NeedsCollision(m_ghostObject, callback.HitCollisionObject))
                {
                    res.Normal = callback.HitNormalWorld;
                    res.HitFraction = callback.ClosestHitFraction;
                    res.Succeeded = true;
                    res.Point = callback.HitPointWorld;
                }
                else
                {
                    res.Succeeded = false;
                    res.HitFraction = 1;
                    res.Point = endPosition;
                }
            }
            return res;
        }

        // IAction interface
        public void DebugDraw(DebugDraw debugDrawer)
        {
        }

        public Vector3 Up
        {
            get => m_up;
            set
            {
                if (value.LengthSquared > 0 && m_gravity > 0.0f)
                {
                    Gravity = -m_gravity * Vector3.Normalize(value);
                    return;
                }

                SetUpVector(ref value);
            }
        }

        public virtual Vector3 AngularVelocity
        {
            get => m_AngVel;
            set => m_AngVel = value;
        }

        public virtual Vector3 LinearVelocity
        {
            get => m_walkDirection;//+ (m_verticalVelocity * m_up);
            set
            {
                Vector3 velocity = value;
                m_walkDirection = velocity;

                // HACK: if we are moving in the direction of the up, treat it as a jump :(
                if (m_walkDirection.LengthSquared > 0)
                {
                    Vector3 w = Vector3.Normalize(velocity);
                    float c = w.Dot(m_up);
                    if (c != 0)
                    {
                        //there is a component in walkdirection for vertical velocity
                        Vector3 upComponent = m_up * ((float)System.Math.Sin(MathUtil.SIMD_HALF_PI - System.Math.Acos(c)) * m_walkDirection.Length);
                        m_walkDirection -= upComponent;
                        m_verticalVelocity = (c < 0.0f ? -1 : 1) * upComponent.Length;
                    }
                }
                else
                    m_verticalVelocity = 0.0f;
            }
        }

        public float LinearDamping
        {
            get => m_linearDamping;
            set => m_linearDamping = value > 1f ? 1f : value < 0f ? 0f : value;
        }

        public float AngularDamping
        {
            get => m_angularDamping;
            set => m_angularDamping = value > 1f ? 1f : value < 0f ? 0f : value;
        }

        public void Reset(CollisionWorld collisionWorld)
        {
            m_verticalVelocity = 0.0f;
            m_verticalOffset = 0.0f;
            m_walkDirection = Vector3.Zero;
            m_velocityTimeInterval = 0.0f;


            //clear pair cache
            HashedOverlappingPairCache cache = m_ghostObject.OverlappingPairCache;
            while (cache.OverlappingPairArray.Count > 0)
            {
                cache.RemoveOverlappingPair(cache.OverlappingPairArray[0].Proxy0, cache.OverlappingPairArray[0].Proxy1, collisionWorld.Dispatcher);
            }

            m_currentVelocity = Vector3.Zero;
            if (CharacterMovement != null)
                CharacterMovement.OnReset();
        }

        public void Warp(ref Vector3 origin)
        {
            Matrix xform;
            xform = Matrix.Identity;
            xform.Origin = origin;
            m_ghostObject.WorldTransform = xform;
            m_currentPosition = origin;
            m_currentVelocity = Vector3.Zero;
        }

        public void PreStep(CollisionWorld collisionWorld)
        {
            m_currentPosition = m_ghostObject.WorldTransform.Origin;
            m_targetPosition = m_currentPosition;

            m_ghostObject.WorldTransform.Decompose(out _, out m_currentOrientation, out _);
            m_targetOrientation = m_currentOrientation;
            //System.Console.WriteLine("m_targetPosition=" + m_targetPosition);
        }

        public Vector3 ApplyPosition(Vector3 position, bool doSweep = false, bool recoverFromPenetration = true)
        {
            if (doSweep)
            {
                var walk = position - m_currentPosition;
                StepForwardAndStrafe(LastWorld, ref walk);
            }
            else
            {
                m_currentPosition = position;
            }
            Matrix xform = m_ghostObject.WorldTransform;
            xform.Origin = m_currentPosition;
            m_ghostObject.WorldTransform = xform;

            if (recoverFromPenetration)
            {
                int numPenetrationLoops = 0;
                m_touchingContact = false;
                while (DoRecoverFromPenetration(LastWorld))
                {
                    numPenetrationLoops++;
                    m_touchingContact = true;
                    if (numPenetrationLoops > 4)
                    {
                        //System.Console.WriteLine("character could not recover from penetration, numPenetrationLoops=" + numPenetrationLoops);
                        break;
                    }
                }
            }
            return m_currentPosition;
        }

        public void RecoverFromPenetration()
        {
            int numPenetrationLoops = 0;
            m_touchingContact = false;
            while (DoRecoverFromPenetration(LastWorld))
            {
                numPenetrationLoops++;
                m_touchingContact = true;
                if (numPenetrationLoops > 4)
                {
                    //System.Console.WriteLine("character could not recover from penetration, numPenetrationLoops=" + numPenetrationLoops);
                    break;
                }
            }
        }

        public Vector3 GetCurrentPosition() {  return m_currentPosition; }
        public Vector3 GetCurrentVelocity() { return m_currentVelocity; }
        public void SetCurrentPosition(Vector3 v) { m_currentPosition = v; }
        public void SetCurrentVelocity(Vector3 v) { m_currentVelocity = v; }

        public Vector3 Gravity
        {
            get => -m_gravity * m_up;
            set
            {
                Vector3 gravity = value;
                m_gravity = gravity.Length;
                if (gravity.LengthSquared > 0)
                {
                    gravity = -gravity;
                    SetUpVector(ref gravity);
                }
            }
        }

        /// <summary>
        /// The max slope determines the maximum angle that the controller can walk up.
        /// The slope angle is measured in radians.
        /// </summary>
        public float MaxSlope
        {
            get => m_maxSlopeRadians;
            set
            {
                m_maxSlopeRadians = value;
                m_maxSlopeCosine = (float)System.Math.Cos(value);
            }
        }

        public float MaxPenetrationDepth
        {
            get => m_maxPenetrationDepth;
            set => m_maxPenetrationDepth = value;
        }

        public PairCachingGhostObject GhostObject => m_ghostObject;

        public void SetUseGhostSweepTest(bool useGhostObjectSweepTest)
        {
            m_useGhostObjectSweepTest = useGhostObjectSweepTest;
        }

        public bool OnGround => System.Math.Abs(m_verticalVelocity) < MathUtil.SIMD_EPSILON && System.Math.Abs(m_verticalOffset) < MathUtil.SIMD_EPSILON;

        public void SetUpInterpolate(bool v)
        {
            m_interpolateUp = v;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                m_manifoldArray.Dispose();
            }
        }

        ~KinematicCharacterController()
        {
           Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    ///@todo Interact with dynamic objects,
    ///Ride kinematicly animated platforms properly
    ///More realistic (or maybe just a config option) falling
    /// -> Should integrate falling velocity manually and use that in stepDown()
    ///Support jumping
    ///Support ducking
    public class KinematicClosestNotMeRayResultCallback : ClosestRayResultCallback
    {
        static Vector3 zero = new Vector3();

        public KinematicClosestNotMeRayResultCallback(CollisionObject me)
            : base(ref zero, ref zero)
        {
            _me = me;
        }

        public override float AddSingleResult(ref LocalRayResult rayResult, bool normalInWorldSpace)
        {
            if (rayResult.CollisionObject == _me)
                return 1.0f;

            return base.AddSingleResult(ref rayResult, normalInWorldSpace);
        }

        protected CollisionObject _me;
    }

    public class KinematicClosestNotMeConvexResultCallback : ClosestConvexResultCallback
    {
        static Vector3 zero = new Vector3();

        protected CollisionObject _me;
        protected Vector3 _up;
        protected float _minSlopeDot;

        public KinematicClosestNotMeConvexResultCallback(CollisionObject me, Vector3 up, float minSlopeDot)
            : base(ref zero, ref zero)
        {
            _me = me;
            _up = up;
            _minSlopeDot = minSlopeDot;
        }

        public override float AddSingleResult(ref LocalConvexResult convexResult, bool normalInWorldSpace)
        {
            if (convexResult.HitCollisionObject == _me)
            {
                return 1.0f;
            }

            if (!convexResult.HitCollisionObject.HasContactResponse)
            {
                return 1.0f;
            }

            Vector3 hitNormalWorld;
            if (normalInWorldSpace)
            {
                hitNormalWorld = convexResult.m_hitNormalLocal;
            }
            else
            {
                // need to transform normal into worldspace
                hitNormalWorld = Vector3.TransformCoordinate(convexResult.m_hitNormalLocal, convexResult.HitCollisionObject.WorldTransform.Basis);
            }

            float dotUp;
            Vector3.Dot(ref _up, ref hitNormalWorld, out dotUp);
            if (dotUp < _minSlopeDot)
            {
                return 1.0f;
            }

            return base.AddSingleResult(ref convexResult, normalInWorldSpace);
        }
    }
}
