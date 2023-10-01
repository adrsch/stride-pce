using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuUtilities;
using BepuUtilities.Collections;
using BepuUtilities.Memory;
using Stride.Engine;
using Stride.Physics.Bepu;
using Stride.Physics;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using Stride.Core.Threading;
using Stride.Engine;

namespace Stride.Physics.Bepu
{
    /// <summary>
    /// Shows one way of handling collision queries that require contact-level test accuracy.
    /// </summary>
    public class BepuShapeTest
    {
        /// <summary>
        /// Provides callbacks for filtering and data collection to the CollisionBatcher we'll be using to test query shapes against the detected environment.
        /// </summary>
        public struct BatcherCallbacks : ICollisionCallbacks
        {
            public List<BepuContact> contactList;

            //These callbacks provide filtering and reporting for pairs being processed by the collision batcher.
            //"Pair id" refers to the identifier given to the pair when it was added to the batcher.
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool AllowCollisionTesting(int pairId, int childA, int childB)
            {
                //If you wanted to filter based on the children of an encountered nonconvex object, here would be the place to do it.
                //The pairId could be used to look up the involved objects and any metadata necessary for filtering.
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void OnChildPairCompleted(int pairId, int childA, int childB, ref ConvexContactManifold manifold)
            {
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void OnPairCompleted<TManifold>(int pairId, ref TManifold manifold) where TManifold : unmanaged, IContactManifold<TManifold>
            {
                if (manifold.Count > 0)
                {
                    contactList.Add(new BepuContact()
                    {
                        Normal = BepuHelpers.ToXenko(manifold.GetNormal(ref manifold, 0)),
                        Offset = BepuHelpers.ToXenko(manifold.GetOffset(ref manifold, 0))
                    });
                }
            }
        }

        /// <summary>
        /// Called by the BroadPhase.GetOverlaps to collect all encountered collidables.
        /// </summary>
        struct BroadPhaseOverlapEnumerator : IBreakableForEach<CollidableReference>
        {
            public List<BepuPhysicsComponent> components;
            public uint lookingFor;

            public bool LoopBody(CollidableReference reference)
            {
                BepuPhysicsComponent bpc = BepuSimulation.getFromReference(reference);
                if (((uint)bpc.CollisionGroup & lookingFor) != 0) components.Add(bpc);
                return true;
            }
        }
        /*
        /// <summary>
        /// Adds a shape query to the collision batcher.
        /// </summary>
        /// <param name="queryShapeType">Type of the shape to test.</param>
        /// <param name="queryShapeData">Shape data to test.</param>
        /// <param name="queryShapeSize">Size of the shape data in bytes.</param>
        /// <param name="queryBoundsMin">Minimum of the query shape's bounding box.</param>
        /// <param name="queryBoundsMax">Maximum of the query shape's bounding box.</param>
        /// <param name="queryPose">Pose of the query shape.</param>
        /// <param name="queryId">Id to use to refer to this query when the collision batcher finishes processing it.</param>
        /// <param name="batcher">Batcher to add the query's tests to.</param>
        static private unsafe void RunQuery(int queryShapeType, void* queryShapeData, int queryShapeSize, in Vector3 queryBoundsMin,
                                                  in Vector3 queryBoundsMax, Vector3 queryPos, System.Numerics.Quaternion queryRot,
                                                  ref CollisionBatcher<BatcherCallbacks> batcher, CollisionFilterGroupFlags lookingFor)
        {
            var broadPhaseEnumerator = new BroadPhaseOverlapEnumerator { components = new List<BepuPhysicsComponent>(), lookingFor = (uint)lookingFor };
            BepuSimulation.instance.internalSimulation.BroadPhase.GetOverlaps(queryBoundsMin, queryBoundsMax, ref broadPhaseEnumerator);
            BepuUtilities.Memory.Buffer<byte>[] shapeMemories = new BepuUtilities.Memory.Buffer<byte>[broadPhaseEnumerator.components.Count];
            for (int overlapIndex = 0; overlapIndex < broadPhaseEnumerator.components.Count; ++overlapIndex)
            {
                BepuPhysicsComponent bpc = broadPhaseEnumerator.components[overlapIndex];
                if (bpc == null || bpc.ColliderShape == null) continue;
                batcher.CacheShapeB(bpc.ColliderShape.TypeId, queryShapeType, queryShapeData, queryShapeSize, out var cachedQueryShapeData);
                int size = bpc.ColliderShape.GetSize();
                lock (batcher.Pool)
                {
                    batcher.Pool.Take<byte>(size, out shapeMemories[overlapIndex]);
                }
                bpc.ColliderShape.CopyData(shapeMemories[overlapIndex].Memory);
                batcher.AddDirectly(bpc.ColliderShape.TypeId, queryShapeType,
                                    shapeMemories[overlapIndex].Memory, cachedQueryShapeData,
                                    queryPos - BepuHelpers.ToBepu(bpc.Position), queryRot, BepuHelpers.ToBepu(bpc.Rotation), 0, new PairContinuation(0));
            }
            // do it if we haven't already
            batcher.Flush();
            // free memory used
            for (int i = 0; i < shapeMemories.Length; i++)
                if (shapeMemories[i].Allocated) batcher.Pool.Return<byte>(ref shapeMemories[i]);
        }*/
        /*
        /// <summary>
        /// Adds a shape query to the collision batcher.
        /// </summary>
        /// <typeparam name="TShape">Type of the query shape.</typeparam>
        /// <param name="shape">Shape to use in the query.</param>
        /// <param name="pose">Pose of the query shape.</param>
        /// <param name="queryId">Id to use to refer to this query when the collision batcher finishes processing it.</param>
        /// <param name="batcher">Batcher to add the query's tests to.</param>
        static public unsafe List<BepuContact> SingleQuery<TShape>(TShape shape, Stride.Core.Mathematics.Vector3 position, Stride.Core.Mathematics.Quaternion rotation, CollisionFilterGroupFlags lookingFor) where TShape : struct, IConvexShape
        {
            List<BepuContact> contacts = new List<BepuContact>();
            var batcher = new CollisionBatcher<BatcherCallbacks>(BepuSimulation.safeBufferPool, BepuSimulation.instance.internalSimulation.Shapes,
                                                                 BepuSimulation.instance.internalSimulation.NarrowPhase.CollisionTaskRegistry, 0f, new BatcherCallbacks() { contactList = contacts });
            System.Numerics.Quaternion q = BepuHelpers.ToBepu(rotation);
            Vector3 v = BepuHelpers.ToBepu(position);
            shape.ComputeBounds(q, out var boundingBoxMin, out var boundingBoxMax);
            boundingBoxMin += v;
            boundingBoxMax += v;
            using (BepuSimulation.instance.simulationLocker.ReadLock())
            {
                RunQuery(shape.TypeId, Unsafe.AsPointer(ref shape), shape.GetSize(), boundingBoxMin, boundingBoxMax, v, q, ref batcher, lookingFor);
            }
            return contacts;
        }
        */
    }
}

