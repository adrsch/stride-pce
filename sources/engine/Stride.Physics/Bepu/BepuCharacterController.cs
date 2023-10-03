using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using BepuPhysics.Collidables;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using Stride.Rendering.Images;

namespace Stride.Physics.Bepu
{
    /// <summary>
    /// Helper object for handling character movement with a base physical body (and optional child camera/VR).
    /// </summary>
    public class BepuCharacterController
    {
        /// <summary>
        /// Generated rigidbody
        /// </summary>
        public BepuRigidbodyComponent Body { get; internal set; }

        /// <summary>
        /// Camera, if found off of the baseBody
        /// </summary>
        public CameraComponent Camera { get; internal set; }

        private static Game internalGame;

        public float Height { get; internal set; }
        public float Radius { get; internal set; }

        private static ConcurrentDictionary<Vector2, Capsule> CapsuleCache = new ConcurrentDictionary<Vector2, Capsule>();

        private Capsule getCapsule(float radius, float height)
        {
            var key = new Vector2(radius, height);
            if (CapsuleCache.TryGetValue(key, out var cap))
                return cap;

            float capsule_len = height - radius * 2f;
            if (capsule_len <= 0f)
                throw new ArgumentOutOfRangeException("Height cannot be less than 2*radius for capsule shape (BepuCharacterController for " + Body.Entity.Name);

            cap = new BepuPhysics.Collidables.Capsule(radius, capsule_len);
            CapsuleCache[key] = cap;

            return cap;
        }

        /// <summary>
        /// Make a new BepuCharacterController helper for an entity, also useful for VR. Automatically will break off VR-tracked from Camera to base if using VR
        /// </summary>
        public BepuCharacterController(Entity baseBody, float height = 1.7f, float radius = 0.5f, CollisionFilterGroups physics_group = CollisionFilterGroups.CharacterFilter,
                                       CollisionFilterGroupFlags collides_with = CollisionFilterGroupFlags.StaticFilter | CollisionFilterGroupFlags.KinematicFilter |
                                       CollisionFilterGroupFlags.AIFilter | CollisionFilterGroupFlags.CharacterFilter, HashSet<Entity> AdditionalVREntitiesToDisconnectFromCamera = null)
        {
            Height = height;
            Radius = radius;

            Body = baseBody.Get<BepuRigidbodyComponent>();

            if (Body == null)
            {
                Body = new BepuRigidbodyComponent(getCapsule(radius, height));
                baseBody.Add(Body);
            }
            else if (!(Body.ColliderShape is Capsule))
                throw new ArgumentException(baseBody.Name + " already has a rigidbody, but it isn't a Capsule shape!");

            Body.CollisionGroup = physics_group;
            Body.CanCollideWith = collides_with;

            if (AdditionalVREntitiesToDisconnectFromCamera == null)
                AdditionalVREntitiesToDisconnectFromCamera = new HashSet<Entity>();

            // can we find an attached camera?
            foreach (Entity e in baseBody.GetChildren())
            {
                var camCheck = e.Get<CameraComponent>();
                if (camCheck != null)
                {
                    Camera = camCheck;
                    break;
                }
            }

            Body.AttachEntityAtBottom = true;
            Body.IgnorePhysicsRotation = true;
            Body.RotationLock = true;
            Body.ActionPerSimulationTick = UpdatePerSimulationTick;

            if (internalGame == null) internalGame = ServiceRegistry.instance?.GetService<IGame>() as Game;

        }

        /// <summary>
        /// If flying is true, gravity is set to zero and Donttouch_Y is set to false
        /// </summary>
        public bool Flying
        {
            get => Body.OverrideGravity;
            set
            {
                Body.OverrideGravity = value;
                Body.Gravity = Vector3.Zero;
                DontTouch_Y = !value;
            }
        }

        /// <summary>
        /// This uses some CPU, but can monitor things like OnGround() functionality
        /// </summary>
        public bool TrackCollisions
        {
            set
            {
                Body.CollectCollisionMaximumCount = value ? 8 : 0;
                Body.CollectCollisions = value;
                Body.SleepThreshold = value ? -1f : 0.01f;
                if (value) Body.IsActive = true;
            }
            get => Body.CollectCollisions;
        }

        /// <summary>
        /// Returns a contact if this is considered on the ground. Requires TrackCollisions to be true
        /// </summary>
        /// <param name="threshold"></param>
        /// <returns></returns>
        public BepuContact? OnGround(float threshold = 0.75f)
        {
            if (TrackCollisions == false)
                throw new InvalidOperationException("You need to set TrackCollisions to true for OnGround to work for CharacterCollision: " + Body.Entity.Name);

            try
            {
                Vector3 reverseGravity = -(Body.OverrideGravity ? Body.Gravity : BepuSimulation.instance.Gravity);
                reverseGravity.Normalize();
                for (int i = 0; i < Body.CurrentPhysicalContactsCount; i++)
                {
                    var contact = Body.CurrentPhysicalContacts[i];
                    if (Vector3.Dot(contact.Normal, reverseGravity) > threshold) return contact;
                }
            }
            catch (Exception) { }
            return null;
        }

        public enum RESIZE_POSITION_OPTION
        {
            RepositionNone = 0,
            RepositionAll = 1,
            RepositionBaseEntityOnly = 2
        }

        /// <summary>
        /// This can change the shape of the rigidbody easily
        /// </summary>
        public void Resize(float? height, float? radius = null, RESIZE_POSITION_OPTION reposition = RESIZE_POSITION_OPTION.RepositionAll)
        {
            float useh = height ?? Height;
            float user = radius ?? Radius;

            if (useh == Height && user == Radius) return;

            Body.ColliderShape = getCapsule(user, useh);

            Height = useh;
            Radius = user;

            switch (reposition)
            {
                case RESIZE_POSITION_OPTION.RepositionAll:
                    SetPosition(Body.Entity.Transform.Position);
                    break;
                case RESIZE_POSITION_OPTION.RepositionBaseEntityOnly:
                    SetPosition(Body.Entity.Transform.Position, false);
                    break;
            }
        }

        /// <summary>
        /// Jump! Will set ApplySingleImpulse (overwriting anything that was there already)
        /// </summary>
        public void Jump(float amount)
        {
            ApplySingleImpulse = new Vector3(0f, amount, 0f);
        }

        /// <summary>
        /// Set how you want this character to move
        /// </summary>
        public Vector3 DesiredMovement;

        /// <summary>
        /// How to dampen the different axis during updating? Defaults to (15,0,15)
        /// </summary>
        public Vector3? MoveDampening = new Vector3(15f, 0f, 15f);

        /// <summary>
        /// Applying a single impulse to this (useful for jumps or pushes)
        /// </summary>
        public Vector3? ApplySingleImpulse;

        /// <summary>
        /// Only operate on X/Z in all situations? Useful for non-flying characters
        /// </summary>
        public bool DontTouch_Y = true;

        /// <summary>
        /// Even if we are flying, should we ignore VR headset Y changes for physics positioning? You generally should leave this as true.
        /// </summary>
        public bool IgnoreVRHeadsetYPhysics = true;

        /// <summary>
        /// Push the character with forces (true) or set velocity directly (false)
        /// </summary>
        public bool UseImpulseMovement = true;

        /// <summary>
        /// Multiplier for the impulse movement (defaults to 100)
        /// </summary>
        public float ImpulseMovementMultiplier = 125f;

        /// <summary>
        /// Multiplier for velocity movement (defaults to 3)
        /// </summary>
        public float VelocityMovementMultiplier = 3f;

        /// <summary>
        /// How height to set the camera when positioning, if using camera?
        /// </summary>
        public float CameraHeightPercent = 0.95f;

        /// <summary>
        /// If you'd like to perform an additional physics tick action on this rigidbody, use this
        /// </summary>
        public Action<BepuRigidbodyComponent, float> AdditionalPerPhysicsAction = null;

        private Vector3 oldPos;

        private void UpdatePerSimulationTick(BepuRigidbodyComponent _body, float frame_time)
        {
            // make sure we are awake if we want to be moving
            if (Body.IsActive == false)
                Body.IsActive = DesiredMovement != Vector3.Zero || ApplySingleImpulse.HasValue;

            if (Body.IgnorePhysicsPosition)
            {
                // use the last velocity to move our base
                Body.Entity.Transform.Position += (Body.Position - oldPos);
                oldPos = Body.Position;
            }

            // try to push our body
            if (UseImpulseMovement)
            {
                // get rid of y if we are not operating on it
                if (DontTouch_Y) DesiredMovement.Y = 0f;
                Body.InternalBody.ApplyLinearImpulse(BepuHelpers.ToBepu(DesiredMovement * frame_time * Body.Mass * ImpulseMovementMultiplier));
            }
            else if (DontTouch_Y)
            {
                Vector3 originalVel = Body.LinearVelocity;
                Vector3 newmove = new Vector3(DesiredMovement.X * VelocityMovementMultiplier, originalVel.Y, DesiredMovement.Z * VelocityMovementMultiplier);
                Body.InternalBody.Velocity.Linear = BepuHelpers.ToBepu(newmove);
            }
            else Body.InternalBody.Velocity.Linear = BepuHelpers.ToBepu(DesiredMovement * VelocityMovementMultiplier);

            // single impulse to apply?
            if (ApplySingleImpulse.HasValue)
            {
                Body.InternalBody.ApplyLinearImpulse(BepuHelpers.ToBepu(ApplySingleImpulse.Value));
                ApplySingleImpulse = null;
            }

            // apply MoveDampening, if any
            if (MoveDampening != null)
            {
                var vel = Body.InternalBody.Velocity.Linear;
                vel.X *= 1f - frame_time * MoveDampening.Value.X;
                vel.Y *= 1f - frame_time * MoveDampening.Value.Y;
                vel.Z *= 1f - frame_time * MoveDampening.Value.Z;
                Body.InternalBody.Velocity.Linear = vel;
            }

            if (AdditionalPerPhysicsAction != null)
                AdditionalPerPhysicsAction(_body, frame_time);
        }

        private float desiredPitch, pitch, yaw, desiredYaw;
        private bool shouldFlickTurn = true;

        public float MouseSensitivity = 3f;
        public bool InvertY = false;

        private void SetRotateButKeepCameraPos(Quaternion rotation)
        {
            var existingPosition = Camera.Entity.Transform.GetWorldPosition();
            Body.Entity.Transform.Rotation = rotation;
            var newPosition = Camera.Entity.Transform.GetWorldPosition(true);
            Body.Entity.Transform.Position -= (newPosition - existingPosition);
        }

        private void RotateButKeepCameraPos(Quaternion rotation)
        {
            var existingPosition = Camera.Entity.Transform.GetWorldPosition();
            Body.Entity.Transform.Rotation *= rotation;
            var newPosition = Camera.Entity.Transform.GetWorldPosition(true);
            Body.Entity.Transform.Position -= (newPosition - existingPosition);
        }

        /// <summary>
        /// Sets how the camera is looking for a player character. Has no effect in VR
        /// </summary>
        public void SetPlayerLook(float? yaw = null, float? pitch = null)
        {
            this.yaw = this.desiredYaw = yaw ?? this.yaw;
            this.pitch = this.desiredPitch = pitch ?? this.pitch;
            Camera.Entity.Transform.Rotation = Quaternion.RotationYawPitchRoll(this.yaw, this.pitch, 0f);
        }

        /// <summary>
        /// Makes this character look at the target. flattenY will always be true in VR
        /// </summary>
        public void LookAt(Vector3 target, bool flattenY = false)
        {
            Vector3 myPos = Camera != null ? Camera.Entity.Transform.GetWorldPosition() : Body.Position;
            Vector3 diff = target - myPos;
            if (flattenY) diff.Y = 0f;
            if (Camera != null)
            {
                    Quaternion.LookAt(ref Camera.Entity.Transform.Rotation, diff);
                    var ypr = Camera.Entity.Transform.Rotation.YawPitchRoll;
                    yaw = desiredYaw = ypr.X;
                    pitch = desiredPitch = ypr.Y;
            }
            else Quaternion.LookAt(ref Body.Entity.Transform.Rotation, diff);
        }

        /// <summary>
        /// Use this to handle mouse/VR look, which operates on a camera (if found)
        /// </summary>
        public void HandleMouseAndVRLook()
        {
            float frame_time = (float)internalGame.UpdateTime.Elapsed.TotalSeconds;

            if (Camera == null)
                throw new ArgumentNullException("No camera to look with!");

           
            Vector2 rotationDelta = internalGame.Input.MouseDelta;

            // Take shortest path
            float deltaPitch = desiredPitch - pitch;
            float deltaYaw = (desiredYaw - yaw) % MathUtil.TwoPi;
            if (deltaYaw < 0) deltaYaw += MathUtil.TwoPi;
            if (deltaYaw > MathUtil.Pi) deltaYaw -= MathUtil.TwoPi;
            desiredYaw = yaw + deltaYaw;

            // Perform orientation transition
            yaw = Math.Abs(deltaYaw) < frame_time ? desiredYaw : yaw + frame_time * Math.Sign(deltaYaw);
            pitch = Math.Abs(deltaPitch) < frame_time ? desiredPitch : pitch + frame_time * Math.Sign(deltaPitch);

            desiredYaw = yaw -= 1.333f * rotationDelta.X * MouseSensitivity; // we want to rotate faster Horizontally and Vertically
            desiredPitch = pitch = MathUtil.Clamp(pitch - rotationDelta.Y * (InvertY ? -MouseSensitivity : MouseSensitivity), -MathUtil.PiOverTwo + 0.05f, MathUtil.PiOverTwo - 0.05f);

            Camera.Entity.Transform.Rotation = Quaternion.RotationYawPitchRoll(yaw, pitch, 0);
        }

        /// <summary>
        /// Set our position and center the camera (if used) on this
        /// </summary>
        public void SetPosition(Vector3 position, bool updateCamera = true)
        {
            if (Camera != null && updateCamera)
            {
                Camera.Entity.Transform.Position.X = 0f;
                Camera.Entity.Transform.Position.Y = Height * CameraHeightPercent;
                Camera.Entity.Transform.Position.Z = 0f;
            }
            Body.Entity.Transform.Position = position;
            position.Y += Height * 0.5f;
            Body.Position = position;
            oldPos = position;
        }
    }
}
