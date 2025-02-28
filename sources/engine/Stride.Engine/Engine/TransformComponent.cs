// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.ComponentModel;
using System.Runtime.Serialization;
using Stride.Core;
using Stride.Core.Collections;
using Stride.Core.Mathematics;
using Stride.Core.Serialization;
using Stride.Engine.Design;
using Stride.Engine.Processors;
using DataContractAttribute = Stride.Core.DataContractAttribute;
using DataMemberAttribute = Stride.Core.DataMemberAttribute;
using DataMemberIgnoreAttribute = Stride.Core.DataMemberIgnoreAttribute;

namespace Stride.Engine
{
    /// <summary>
    /// Defines Position, Rotation and Scale of its <see cref="Entity"/>.
    /// </summary>
    [DataContract("TransformComponent")]
    [DataSerializerGlobal(null, typeof(FastCollection<TransformComponent>))]
    [DefaultEntityComponentProcessor(typeof(TransformProcessor))]
    [Display("Transform", Expand = ExpandRule.Once)]
    [ComponentOrder(0)]
    public sealed class TransformComponent : EntityComponent //, IEnumerable<TransformComponent> Check why this is not working
    {
        private static readonly TransformOperation[] EmptyTransformOperations = new TransformOperation[0];

        // When false, transformation should be computed in TransformProcessor (no dependencies).
        // When true, transformation is computed later by another system.
        // This is useful for scenario such as binding a node to a bone, where it first need to run TransformProcessor for the hierarchy,
        // run MeshProcessor to update ModelViewHierarchy, copy Node/Bone transformation to another Entity with special root and then update its children transformations.
        private bool useTRS = true;
        private TransformComponent parent;

        internal bool IsMovingInsideRootScene;

        /// <summary>
        /// This is where we can register some custom work to be done after world matrix has been computed, such as updating model node hierarchy or physics for local node.
        /// </summary>
        [DataMemberIgnore]
        public FastListStruct<TransformOperation> PostOperations = new FastListStruct<TransformOperation>(EmptyTransformOperations);

        /// <summary>
        /// The world matrix.
        /// Its value is automatically recomputed at each frame from the local and the parent matrices.
        /// One can use <see cref="UpdateWorldMatrix"/> to force the update to happen before next frame.
        /// </summary>
        /// <remarks>The setter should not be used and is accessible only for performance purposes.</remarks>
        [DataMemberIgnore]
        public Matrix WorldMatrix = Matrix.Identity;

        /// <summary>
        /// The local matrix.
        /// Its value is automatically recomputed at each frame from the position, rotation and scale.
        /// One can use <see cref="UpdateLocalMatrix"/> to force the update to happen before next frame.
        /// </summary>
        /// <remarks>The setter should not be used and is accessible only for performance purposes.</remarks>
        [DataMemberIgnore]
        public Matrix LocalMatrix = Matrix.Identity;

        /// <summary>
        /// The translation relative to the parent transformation.
        /// </summary>
        /// <userdoc>The translation of the entity with regard to its parent</userdoc>
        [DataMember(10)]
        public Vector3 Position;

        /// <summary>
        /// The rotation relative to the parent transformation.
        /// </summary>
        /// <userdoc>The rotation of the entity with regard to its parent</userdoc>
        [DataMember(20)]
        public Quaternion Rotation;

        /// <summary>
        /// The scaling relative to the parent transformation.
        /// </summary>
        /// <userdoc>The scale of the entity with regard to its parent</userdoc>
        [DataMember(30)]
        public Vector3 Scale;


        public Vector3 Forward => Transform.WorldMatrix.Forward;
        public Vector3 Back => Transform.WorldMatrix.Backward;
        public Vector3 Up => Transform.WorldMatrix.Up;
        public Vector3 Down => Transform.WorldMatrix.Down;
        public Vector3 Left => Transform.WorldMatrix.Left;
        public Vector3 Right => Transform.WorldMatrix.Right;

        /// <summary>
        /// phr00t
        /// Gets the world position.
        /// Default call does not recalcuate the position. It just gets the last frame's position quickly.
        /// If you pass true to this function, it will update the world position (which is a costly procedure) to get the most up-to-date position.
        /// </summary>
        public Vector3 GetWorldPosition(bool recalculate = false)
        {
            if (recalculate) UpdateWorldMatrix(true, false);
            return parent == null ? Position : WorldMatrix.TranslationVector;
        }

        public void SetWorldPosition(Vector3 p)
        {
            if (parent == null) Position = p;
            else WorldMatrix.TranslationVector = p;
        }

        [Stride.Core.DataMemberIgnore]
        public Vector3 WorldPosition { get => parent == null ? Position : WorldMatrix.TranslationVector; set => SetWorldPosition(value); }

        /// <summary>
        /// phr00t
        /// Gets the world scale.
        /// Default call does not recalcuate the scale. It just gets the last frame's scale quickly.
        /// If you pass true to this function, it will update the world position (which is a costly procedure) to get the most up-to-date scale.
        /// </summary>
        public Vector3 GetWorldScale(bool recalculate = false)
        {
            if (recalculate) UpdateWorldMatrix(true, false);
            if (parent == null) return Scale;
            WorldMatrix.GetScale(out Vector3 scale);
            return scale;
        }

        [Stride.Core.DataMemberIgnore]
        public Vector3 WorldScale => GetWorldScale(false);

        /// <summary>
        /// phr00t
        /// Gets the world rotation.
        /// Default call does not recalcuate the rotation. It just gets the last frame's rotation (relatively) quickly.
        /// If you pass true to this function, it will update the world position (which is a costly procedure) to get the most up-to-date rotation.
        /// </summary>
        public Quaternion GetWorldRotation(bool recalculate = false)
        {
            if (recalculate) UpdateWorldMatrix(true, false);
            if (parent != null && WorldMatrix.GetRotationQuaternion(out Quaternion q))
            {
                return q;
            }
            else
            {
                return Rotation;
            }
        }

        [Stride.Core.DataMemberIgnore]
        public Quaternion WorldRotation { get => GetWorldRotation(false); }


        [DataMemberIgnore]
        public TransformLink TransformLink;

        /// <summary>
        /// Initializes a new instance of the <see cref="TransformComponent" /> class.
        /// </summary>
        public TransformComponent()
        {
            Children = new TransformChildrenCollection(this);

            UseTRS = true;
            Scale = Vector3.One;
            Rotation = Quaternion.Identity;
        }

        /// <summary>
        /// Gets or sets a value indicating whether to use the Translation/Rotation/Scale.
        /// </summary>
        /// <value><c>true</c> if [use TRS]; otherwise, <c>false</c>.</value>
        [DataMemberIgnore]
        [Display(Browsable = false)]
        [DefaultValue(true)]
        public bool UseTRS
        {
            get { return useTRS; }
            set { useTRS = value; }
        }

        /// <summary>
        /// Gets the children of this <see cref="TransformComponent"/>.
        /// </summary>
        public FastCollection<TransformComponent> Children { get; }

        /// <summary>
        /// Gets or sets the euler rotation, with XYZ order.
        /// Not stable: setting value and getting it again might return different value as it is internally encoded as a <see cref="Quaternion"/> in <see cref="Rotation"/>.
        /// </summary>
        /// <value>
        /// The euler rotation.
        /// </value>
        [DataMemberIgnore]
        public Vector3 RotationEulerXYZ
        {
            // Unfortunately it is not possible to factorize the following code with Quaternion.RotationYawPitchRoll because Z axis direction is inversed
            get
            {
                var rotation = Rotation;
                Vector3 rotationEuler;

                // Equivalent to:
                //  Matrix rotationMatrix;
                //  Matrix.Rotation(ref cachedRotation, out rotationMatrix);
                //  rotationMatrix.DecomposeXYZ(out rotationEuler);

                float xx = rotation.X * rotation.X;
                float yy = rotation.Y * rotation.Y;
                float zz = rotation.Z * rotation.Z;
                float xy = rotation.X * rotation.Y;
                float zw = rotation.Z * rotation.W;
                float zx = rotation.Z * rotation.X;
                float yw = rotation.Y * rotation.W;
                float yz = rotation.Y * rotation.Z;
                float xw = rotation.X * rotation.W;

                float M11 = 1.0f - (2.0f * (yy + zz));
                float M12 = 2.0f * (xy + zw);
                float M13 = 2.0f * (zx - yw);
                //float M21 = 2.0f * (xy - zw);
                float M22 = 1.0f - (2.0f * (zz + xx));
                float M23 = 2.0f * (yz + xw);
                //float M31 = 2.0f * (zx + yw);
                float M32 = 2.0f * (yz - xw);
                float M33 = 1.0f - (2.0f * (yy + xx));

                /*** Refer to Matrix.DecomposeXYZ(out Vector3 rotation) for code and license ***/
                if (MathUtil.IsOne(Math.Abs(M13)))
                {
                    if (M13 >= 0)
                    {
                        // Edge case where M13 == +1
                        rotationEuler.Y = -MathUtil.PiOverTwo;
                        rotationEuler.Z = MathF.Atan2(-M32, M22);
                        rotationEuler.X = 0;
                    }
                    else
                    {
                        // Edge case where M13 == -1
                        rotationEuler.Y = MathUtil.PiOverTwo;
                        rotationEuler.Z = -MathF.Atan2(-M32, M22);
                        rotationEuler.X = 0;
                    }
                }
                else
                {
                    // Common case
                    rotationEuler.Y = MathF.Asin(-M13);
                    rotationEuler.Z = MathF.Atan2(M12, M11);
                    rotationEuler.X = MathF.Atan2(M23, M33);
                }
                return rotationEuler;
            }
            set
            {
                // Equivalent to:
                //  Quaternion quatX, quatY, quatZ;
                //
                //  Quaternion.RotationX(value.X, out quatX);
                //  Quaternion.RotationY(value.Y, out quatY);
                //  Quaternion.RotationZ(value.Z, out quatZ);
                //
                //  rotation = quatX * quatY * quatZ;

                var halfAngles = value * 0.5f;

                var fSinX = MathF.Sin(halfAngles.X);
                var fCosX = MathF.Cos(halfAngles.X);
                var fSinY = MathF.Sin(halfAngles.Y);
                var fCosY = MathF.Cos(halfAngles.Y);
                var fSinZ = MathF.Sin(halfAngles.Z);
                var fCosZ = MathF.Cos(halfAngles.Z);

                var fCosXY = fCosX * fCosY;
                var fSinXY = fSinX * fSinY;

                Rotation.X = fSinX * fCosY * fCosZ - fSinZ * fSinY * fCosX;
                Rotation.Y = fSinY * fCosX * fCosZ + fSinZ * fSinX * fCosY;
                Rotation.Z = fSinZ * fCosXY - fSinXY * fCosZ;
                Rotation.W = fCosZ * fCosXY + fSinXY * fSinZ;
            }
        }

        /// <summary>
        /// Gets or sets the parent of this <see cref="TransformComponent"/>.
        /// </summary>
        /// <value>
        /// The parent.
        /// </value>
        [DataMemberIgnore]
        public TransformComponent Parent
        {
            get { return parent; }
            set
            {
                var oldParent = Parent;
                if (oldParent == value)
                    return;

                // SceneValue must be null if we have a parent
                if( Entity.SceneValue != null )
                    Entity.Scene = null;

                var previousScene = oldParent?.Entity?.Scene;
                var newScene = value?.Entity?.Scene;

                // Get to root scene
                while (previousScene?.Parent != null)
                    previousScene = previousScene.Parent;
                while (newScene?.Parent != null)
                    newScene = newScene.Parent;

                // Check if root scene didn't change
                bool moving = (newScene != null && newScene == previousScene);
                if (moving)
                    IsMovingInsideRootScene = true;

                // Add/Remove
                oldParent?.Children.Remove(this);
                value?.Children.Add(this);

                if (moving)
                    IsMovingInsideRootScene = false;
            }
        }

        /// <summary>
        /// Updates the local matrix.
        /// If <see cref="UseTRS"/> is true, <see cref="LocalMatrix"/> will be updated from <see cref="Position"/>, <see cref="Rotation"/> and <see cref="Scale"/>.
        /// </summary>
        public void UpdateLocalMatrix()
        {
            if (UseTRS)
            {
                Matrix.Transformation(ref Scale, ref Rotation, ref Position, out LocalMatrix);
            }
        }

        /// <summary>
        /// Updates the local matrix based on the world matrix and the parent entity's or containing scene's world matrix.
        /// </summary>
        public void UpdateLocalFromWorld()
        {
            if (Parent == null)
            {
                var scene = Entity?.Scene;
                if (scene != null)
                {
                    Matrix.Invert(ref scene.WorldMatrix, out var inverseSceneTransform);
                    Matrix.Multiply(ref WorldMatrix, ref inverseSceneTransform, out LocalMatrix);
                }
                else
                {
                    LocalMatrix = WorldMatrix;
                }
            }
            else
            {
                //We are not root so we need to derive the local matrix as well
                Matrix.Invert(ref Parent.WorldMatrix, out var inverseParent);
                Matrix.Multiply(ref WorldMatrix, ref inverseParent, out LocalMatrix);
            }
        }

        /// <summary>
        //phr00t
        /// Updates the world matrix.
        /// It will first call <see cref="UpdateLocalMatrix"/> on self, and <see cref="UpdateWorldMatrix"/> on <see cref="Parent"/> if not null.
        /// Then <see cref="WorldMatrix"/> will be updated by multiplying <see cref="LocalMatrix"/> and parent <see cref="WorldMatrix"/> (if any).
        /// </summary>
        public void UpdateWorldMatrix(bool recursive = true, bool postProcess = true)
        {
            UpdateLocalMatrix();
            UpdateWorldMatrixInternal(recursive, postProcess);
        }

        //phr00t
        public void UpdateWorldMatrixInternal(bool recursive, bool postProcess = true)
        {
            if (TransformLink != null)
            {
                Matrix linkMatrix;
                TransformLink.ComputeMatrix(recursive, out linkMatrix);
                Matrix.Multiply(ref LocalMatrix, ref linkMatrix, out WorldMatrix);
            }
            else if (Parent != null)
            {
                if (recursive)
                    Parent.UpdateWorldMatrix(true, postProcess);
                Matrix.Multiply(ref LocalMatrix, ref Parent.WorldMatrix, out WorldMatrix);
            }
            else
            {
                var scene = Entity?.Scene;
                if (scene != null)
                {
                    if (recursive)
                    {
                        scene.UpdateWorldMatrix();
                    }

                    Matrix.Multiply(ref LocalMatrix, ref scene.WorldMatrix, out WorldMatrix);
                }
                else
                {
                    WorldMatrix = LocalMatrix;
                }
            }
            if (postProcess)
            {
                foreach (var transformOperation in PostOperations)
                {
                    transformOperation.Process(this);
                }
            }
        }

        [DataContract]
        public class TransformChildrenCollection : FastCollection<TransformComponent>
        {
            TransformComponent transform;
            Entity Entity => transform.Entity;

            public TransformChildrenCollection(TransformComponent transformParam)
            {
                transform = transformParam;
            }

            private void OnTransformAdded(TransformComponent item)
            {
                if (item.Parent != null)
                    throw new InvalidOperationException("This TransformComponent already has a Parent, detach it first.");

                item.parent = transform;

                Entity?.EntityManager?.OnHierarchyChanged(item.Entity);
                Entity?.EntityManager?.GetProcessor<TransformProcessor>().NotifyChildrenCollectionChanged(item, true);
            }
            private void OnTransformRemoved(TransformComponent item)
            {
                if (item.Parent != transform)
                    throw new InvalidOperationException("This TransformComponent's parent is not the expected value.");

                item.parent = null;

                Entity?.EntityManager?.OnHierarchyChanged(item.Entity);
                Entity?.EntityManager?.GetProcessor<TransformProcessor>().NotifyChildrenCollectionChanged(item, false);
            }

            /// <inheritdoc/>
            protected override void InsertItem(int index, TransformComponent item)
            {
                base.InsertItem(index, item);
                OnTransformAdded(item);
            }

            /// <inheritdoc/>
            protected override void RemoveItem(int index)
            {
                OnTransformRemoved(this[index]);
                base.RemoveItem(index);
            }

            /// <inheritdoc/>
            protected override void ClearItems()
            {
                for (var i = Count - 1; i >= 0; --i)
                    OnTransformRemoved(this[i]);
                base.ClearItems();
            }

            /// <inheritdoc/>
            protected override void SetItem(int index, TransformComponent item)
            {
                OnTransformRemoved(this[index]);

                base.SetItem(index, item);

                OnTransformAdded(this[index]);
            }
        }
    }
}
