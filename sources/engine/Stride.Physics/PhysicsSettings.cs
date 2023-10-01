// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using Stride.Core;
using Stride.Data;

namespace Stride.Physics
{
    [DataContract]
    [Display("Physics")]
    public class PhysicsSettings : Configuration
    {
        [DataMember(10)]
        public PhysicsEngineFlags Flags;

        /// <userdoc>
        /// FOR BEPU
        /// The maximum number of simulations the physics engine can run in a frame to compensate for slowdown
        /// </userdoc>
        [DataMember(20)]
      //  [Obsolete($"Value is ignored, use {nameof(MaxTickDuration)} instead")]
        public int MaxSubSteps = 1;

        [DataMember(21)]
        public int VelocityIterationCount = 2;
        [DataMember(22)]
        public int FallbackBatchThreshold = 64;

        /// <userdoc>
        /// The length in seconds of a physics simulation frame. The default is 0.016667 (one sixtieth of a second)
        /// </userdoc>
        [DataMember(30)]
        public float FixedTimeStep = 1.0f / 60.0f;

        /// <userdoc>
        /// BULLET2 ONLY
        /// Amount of time in seconds allotted to update the physics simulation when the update rate is lower than <see cref="FixedTimeStep"/>.
        /// When the whole game takes longer than <see cref="FixedTimeStep"/> to display one frame, the simulation has to tick multiple times to catch up.
        /// Those additional ticks may themselves make the current frame take longer, leading to a negative feedback loop for your game's performances.
        /// This variable will 'slow down' the simulation instead.
        /// </userdoc>
        [DataMember(40)]
        public float MaxTickDuration = 1f / 120f;

        /// <summary>
        /// If set to true, forcefully disable any Bullet physics processing.
        /// </summary>
        [DataMember]
        public bool OnlyUseBepu = false;
    }
}
