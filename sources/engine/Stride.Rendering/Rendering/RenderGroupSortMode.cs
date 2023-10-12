// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;

namespace Stride.Rendering
{
    /// <summary>
    /// Sort elements according to the pattern: 
    /// [RenderFeature Sort Key 8 bits] 
    /// [order 8 bits] 
    /// [Distance back to front 16 bits] 
    /// [RenderObject states 24 bits]
    /// </summary>
    [DataContract("RenderGroupSortMode")]
    public class RenderGroupSortMode : SortModeDistance
    {
        public RenderGroupSortMode() : base(true)
        {
            distancePosition = 24;
            distancePrecision = 32;

            statePrecision = 24;
            useGroup = true;
        }/*: base(true)
        {
            distancePosition = 24;
          //  distancePrecision = 19;
          //  distancePrecision = 0;

            statePrecision = 0;
          //  statePrecision = 24;

            //orderPosition = 43;
            orderPosition = 32;
            // orderPrecision = 5;
            useGroup = true;
        }*/
    }
}
