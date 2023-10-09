using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stride.Core;
using Stride.Core.Serialization;
using Stride.Core.Serialization.Serializers;

namespace Stride.Engine
{
    [DataContract("SeqId")]
    [DataSerializer(typeof(Serializer))]
    public struct SeqId : IComparable<SeqId>, IEquatable<SeqId>
    {
        private string _id;
        public int Hash;
        public string Id
        {
            get => _id;
            set
            {
                _id = value;
                Hash = value.GetHashCode();
            }
        }

        public static readonly SeqId Empty = new SeqId();

        public SeqId()
        {
            _id = Guid.NewGuid().ToString();

            Hash = _id.GetHashCode();
        }

        public SeqId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                _id = Guid.NewGuid().ToString();
            else
                _id = id;

            Hash = _id.GetHashCode();
        }

        public static explicit operator SeqId(string id)
        {
            return new SeqId(id);
        }



        public static SeqId New()
        {
            return new SeqId(Guid.NewGuid().ToString());
        }

        /// <summary>
        /// Implements the ==.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result of the operator.</returns>
        public static bool operator ==(SeqId left, SeqId right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Implements the !=.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result of the operator.</returns>
        public static bool operator !=(SeqId left, SeqId right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc/>
        public bool Equals(SeqId other)
        {
            return Hash == other.Hash;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is SeqId id && Equals(id);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return Hash;
        }

        /// <inheritdoc/>
        public int CompareTo(SeqId other)
        {
            return _id.CompareTo(other._id);
        }

        public static bool TryParse(string input, out SeqId result)
        {
            result = new SeqId(input);
            return true;
        }

        public static SeqId Parse(string input)
        {
            return new SeqId(input);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return _id;
        }

        internal class Serializer : DataSerializer<SeqId>
        {
            // private DataSerializer<Guid> guidSerialier;
            private DataSerializer<string> stringSerialier;

            public override void Initialize(SerializerSelector serializerSelector)
            {
                base.Initialize(serializerSelector);
                stringSerialier = serializerSelector.GetSerializer<string>();
                //   guidSerialier = serializerSelector.GetSerializer<Guid>();
            }

            public override void Serialize(ref SeqId obj, ArchiveMode mode, SerializationStream stream)
            {
                //     var guid = obj.guid;
                var id = obj._id;
                stringSerialier.Serialize(ref id, mode, stream);
                //       guidSerialier.Serialize(ref guid, mode, stream);
                if (mode == ArchiveMode.Deserialize)
                    obj = new SeqId(id);
                //       obj = new SeqId(guid);
            }
        }
    }
}
