﻿using System;
using System.Collections.Generic;
using FaunaDB.LINQ.Modeling;
using FaunaDB.LINQ.Types;
using FaunaDB.Types;
using Newtonsoft.Json;

namespace FaunaDB.Client.LINQ.Tests
{
    public class ReferenceModel : IReferenceType
    {
        [JsonProperty("ref")]
        public string Id { get; set; }
        [Indexed("index_1")]
        public string Indexed1 { get; set; }
        [Indexed("index_2")]
        public string Indexed2 { get; set; }
        [JsonProperty("ts")]
        public DateTime TimeStamp { get; set; }

        [Indexed("composite_index")]
        public CompositeIndex<string, string> CompositeIndex { get; set; }

        protected bool Equals(ReferenceModel other)
        {
            return string.Equals(Indexed1, other.Indexed1) && string.Equals(Indexed2, other.Indexed2);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((ReferenceModel) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Indexed1 != null ? Indexed1.GetHashCode() : 0) * 397) ^ (Indexed2 != null ? Indexed2.GetHashCode() : 0);
            }
        }
    }

    public class PrimitivesReferenceModel : IReferenceType
    {
        [JsonProperty("ref")]
        public string Id { get; set; }
        public string StringVal { get; set; }
        public int IntVal { get; set; }
        public bool BooleanVal { get; set; }
        public byte ByteVal { get; set; }
        public long LongVal { get; set; }
        public float FloatVal { get; set; }
        public double DoubleVal { get; set; }
        public DateTime DateTimeVal { get; set; }
        public short ShortVal { get; set; }
        public ushort UShortVal { get; set; }
        public uint UIntVal { get; set; }
        public ulong ULongVal { get; set; }
        public sbyte SByteVal { get; set; }
        public char CharVal { get; set; }

        protected bool Equals(PrimitivesReferenceModel other)
        {
            return string.Equals(StringVal, other.StringVal) && IntVal == other.IntVal &&
                   BooleanVal == other.BooleanVal && ByteVal == other.ByteVal && LongVal == other.LongVal &&
                   FloatVal.Equals(other.FloatVal) && DoubleVal.Equals(other.DoubleVal) &&
                   DateTimeVal.Equals(other.DateTimeVal) && ShortVal == other.ShortVal &&
                   UShortVal == other.UShortVal && UIntVal == other.UIntVal && ULongVal == other.ULongVal &&
                   SByteVal == other.SByteVal && CharVal == other.CharVal;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((PrimitivesReferenceModel) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (StringVal != null ? StringVal.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ IntVal;
                hashCode = (hashCode * 397) ^ BooleanVal.GetHashCode();
                hashCode = (hashCode * 397) ^ ByteVal.GetHashCode();
                hashCode = (hashCode * 397) ^ LongVal.GetHashCode();
                hashCode = (hashCode * 397) ^ FloatVal.GetHashCode();
                hashCode = (hashCode * 397) ^ DoubleVal.GetHashCode();
                hashCode = (hashCode * 397) ^ DateTimeVal.GetHashCode();
                hashCode = (hashCode * 397) ^ ShortVal.GetHashCode();
                hashCode = (hashCode * 397) ^ UShortVal.GetHashCode();
                hashCode = (hashCode * 397) ^ (int) UIntVal;
                hashCode = (hashCode * 397) ^ ULongVal.GetHashCode();
                hashCode = (hashCode * 397) ^ SByteVal.GetHashCode();
                hashCode = (hashCode * 397) ^ CharVal.GetHashCode();
                return hashCode;
            }
        }
    }

    public class ValueTypesReferenceModel : IReferenceType
    {
        [JsonProperty("ref")]
        public string Id { get; set; }

        public ValueModel ValueModel { get; set; }
        public List<ValueModel> ValueModels1 { get; set; }
        public ValueModel[] ValueModels2 { get; set; }
    }

    public class ReferenceTypesReferenceModel : IReferenceType
    {
        [JsonProperty("ref")]
        public string Id { get; set; }

        [Reference]
        public ReferenceModel ReferenceModel { get; set; }
        [Reference]
        public List<ReferenceModel> ReferenceModels1 { get; set; }
        [Reference]
        public ReferenceModel[] ReferenceModels2 { get; set; }
    }

    public class ValueModel
    {
        public string Value1 { get; set; }
        public string Value2 { get; set; }
    }
}