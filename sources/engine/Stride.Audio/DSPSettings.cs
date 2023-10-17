using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Stride.Core.Serialization;
using Stride.Core.Serialization.Contents;

namespace Stride.Audio
{
    [DataContract]
    [ContentSerializer(typeof(DataContentSerializer<DSPSettings>))]
    public class DSPSettings
    {
        [DataMember]
        public float ReverbLevel;
        [DataMember]
        public float LpfDirect;
        [DataMember]
        public float LpfReverb;
        [DataMember]
        public float[] DelayTimes = new float[2];
    }
}
