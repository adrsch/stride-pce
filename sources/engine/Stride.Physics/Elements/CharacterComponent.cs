// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;

namespace Stride.Physics
{
    [DataContract("CharacterComponent")]
    [Display("Character")]
    public sealed class CharacterComponent : PhysicsComponent
    {
        public CharacterComponent()
        {
            Orientation = Quaternion.Identity;
           // StepHeight = 0.1f;
        }

        private AngleSingle maxSlope = new AngleSingle(45, AngleType.Degree);

        float margin;
        public float Margin
        {
            get { return margin; } set {  margin = value;
                if (ColliderShape != null)
                {
                    ColliderShape.Margin = value;
                }
            }
        }
        
        /// <summary>
        /// Gets or sets if this character element max slope
        /// </summary>
        /// <value>
        /// true, false
        /// </value>
        /// <userdoc>
        /// The max slope this character can climb
        /// </userdoc>
        [Display("Maximum Slope")]
        [DataMember(85)]
        public AngleSingle MaxSlope
        {
            get
            {
                return maxSlope;
            }
            set
            {
                maxSlope = value;

                if (KinematicCharacter != null)
                {
                    KinematicCharacter.MaxSlope = value.Radians;
                }
            }
        }
        
        /// <summary>
        /// Gets the linear velocity from the kinematic character
        /// </summary>
        /// <value>
        /// Vector3
        /// </value>
        /// <userdoc>
        /// The linear speed of the character component
        /// </userdoc>
        [DataMemberIgnore]
        public Vector3 LinearVelocity
        {
            get
            {
                return KinematicCharacter != null ? KinematicCharacter.LinearVelocity : Vector3.Zero;
            }
        }

        public bool IsGrounded => KinematicCharacter?.OnGround ?? false;

        public void SetUseGhost(bool useGhost) => KinematicCharacter?.SetUseGhostSweepTest(useGhost);
        /// <summary>
        /// Teleports the specified target position.
        /// </summary>
        /// <param name="targetPosition">The target position.</param>
        public void Teleport(Vector3 targetPosition)
        {
            if (KinematicCharacter == null)
            {
                throw new InvalidOperationException("Attempted to call a Physics function that is avaliable only when the Entity has been already added to the Scene.");
            }

            //we assume that the user wants to teleport in world/entity space
            var entityPos = Entity.Transform.Position;
            var physPos = PhysicsWorldTransform.TranslationVector;
            var diff = physPos - entityPos;
            BulletSharp.Math.Vector3 bV3 = targetPosition + diff;
            KinematicCharacter.Warp(ref bV3);
        }

        /// <summary>
        /// Sets or gets the orientation of the Entity attached to this character controller
        /// </summary>
        /// <remarks>This orientation has no impact in the physics simulation</remarks>
        [DataMemberIgnore]
        public Quaternion Orientation { get; set; }

        [DataMemberIgnore]
        internal BulletSharp.KinematicCharacterController KinematicCharacter;

        public void SetCharacterMovement(BulletSharp.ICharacterMovement velocityUpdater) => KinematicCharacter?.SetCharacterMovement(velocityUpdater);

        public BulletSharp.CharacterSweepCallback DoSweep(BulletSharp.Math.Vector3 start, BulletSharp.Math.Vector3 end) => KinematicCharacter?.DoSweep(start, end) ?? default;

        public void ApplyPosition(Vector3 position, bool doSweep = false, bool recoverFromPenetration = true) => KinematicCharacter?.ApplyPosition(position, doSweep, recoverFromPenetration);

        public BulletSharp.Math.Vector3 GetPhysicsPosition() => KinematicCharacter?.GetCurrentPosition() ?? Vector3.zero;
        public BulletSharp.Math.Vector3 GetPhysicsVelocity() => KinematicCharacter?.GetCurrentVelocity() ?? Vector3.zero;

        public void SetPhysicsPosition(BulletSharp.Math.Vector3 v) => KinematicCharacter?.SetCurrentPosition(v);
        public void SetPhysicsVelocity(BulletSharp.Math.Vector3 v) => KinematicCharacter?.SetCurrentVelocity(v);

        public void RecoverFromPenetration() => KinematicCharacter?.RecoverFromPenetration();

        public void UpdateShape()
        {
            KinematicCharacter.SetConvexShape((BulletSharp.ConvexShape)ColliderShape.InternalShape);
        }

        protected override void OnAttach()
        {
            NativeCollisionObject = new BulletSharp.PairCachingGhostObject
            {
                CollisionShape = ColliderShape.InternalShape,
                UserObject = this,
            };

            NativeCollisionObject.CollisionFlags |= BulletSharp.CollisionFlags.CharacterObject;

            if (ColliderShape.NeedsCustomCollisionCallback)
            {
                NativeCollisionObject.CollisionFlags |= BulletSharp.CollisionFlags.CustomMaterialCallback;
            }

            NativeCollisionObject.ContactProcessingThreshold = !Simulation.CanCcd ? 1e18f : 1e30f;

            BulletSharp.Math.Vector3 unitY = new BulletSharp.Math.Vector3(0f, 1f, 0f);
            KinematicCharacter = new BulletSharp.KinematicCharacterController((BulletSharp.PairCachingGhostObject)NativeCollisionObject, (BulletSharp.ConvexShape)ColliderShape.InternalShape, 0.5f, ref unitY);

            base.OnAttach();

            Margin = margin;
            MaxSlope = maxSlope;

            UpdatePhysicsTransformation(); //this will set position and rotation of the collider

            Simulation.AddCharacter(this, (CollisionFilterGroupFlags)CollisionGroup, CanCollideWith);
        }

        protected override void OnDetach()
        {
            if (KinematicCharacter == null) return;

            Simulation.RemoveCharacter(this);

            KinematicCharacter.Dispose();
            KinematicCharacter = null;

            base.OnDetach();
        }
    }
}
