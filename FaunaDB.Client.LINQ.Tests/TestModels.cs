using System;
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