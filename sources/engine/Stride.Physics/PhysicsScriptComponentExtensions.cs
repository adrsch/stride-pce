// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Engine;
using Stride.Physics.Bepu;

namespace Stride.Physics
{
    /// <summary>
    /// Extension methods for the <see cref="ScriptComponent"/> related to phystics
    /// </summary>
    public static class PhysicsScriptComponentExtensions
    {
        /// <summary>
        /// Gets the curent <see cref="Bullet2Simulation"/>.
        /// </summary>
        /// <param name="scriptComponent">The script component to query physics from</param>
        /// <returns>The simulation object or null if there are no simulation running for the current scene.</returns>
        public static Bullet2Simulation GetBullet2Simulation(this ScriptComponent scriptComponent)
        {
            return scriptComponent.SceneSystem.SceneInstance.GetProcessor<PhysicsProcessor>()?.Simulation;
        }

    }
}
