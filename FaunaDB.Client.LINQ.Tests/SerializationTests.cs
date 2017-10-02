using System;
using System.Collections.Generic;
using FaunaDB.Extensions;
using FaunaDB.Query;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static FaunaDB.Extensions.Language;

namespace FaunaDB.Client.LINQ.Tests
{
    [TestClass]
    public class SerializationTests
    {
        [TestMethod]
        public void BasicReferenceTypeSerializationTest()
        {
            var model = new ReferenceModel { Indexed1 = "test1", Indexed2 = "test2" }; // Indexed1: name attr "indexed1" | Indexed2: default name

            var manual = Obj("indexed1", "test1", "Indexed2", "test2");

            Assert.IsTrue(manual.Equals(model.ToFaunaObj()));
        }

        [TestMethod]
        public void ReferenceTypeWithAllPrimitiveTypesTest()
        {
            var model = new PrimitivesReferenceModel
            {
                BooleanVal = true,
                ByteVal = 10,
                CharVal = 'a',
                DateTimeVal = DateTime.MaxValue,
                DoubleVal = 123.4,
                FloatVal = 234.5f,
                LongVal = 123,
                IntVal = 234,
                SByteVal = 1,
                UIntVal = 2,
                UShortVal = 3,
                ULongVal = 4,
                ShortVal = 5,
                StringVal = "test1"
            };

            var dict = new Dictionary<string, object>
            {
                {"BooleanVal", true},
                {"ByteVal", 10},
                {"CharVal", "a"},
                {"DateTimeVal", Time(DateTime.MaxValue.ToString("O"))},
                {"DoubleVal", 123.4},
                {"FloatVal", 234.5f},
                {"LongVal", 123},
                {"IntVal", 234},
                {"SByteVal", 1},
                {"UIntVal", 2},
                {"UShortVal", 3},
                {"ULongVal", 4.0}, // bizarre issue with conversion, ulong will implicit to double for some reason
                {"ShortVal", 5},
                {"StringVal", "test1"}
            };

            var manual = Obj(dict);
            Assert.IsTrue(manual.Equals(model.ToFaunaObj()));
        }

        [TestMethod]
        public void ReferenceTypeWithValueTypesTest()
        {
            var model = new ValueTypesReferenceModel
            {
                ValueModel = new ValueModel { Value1 = "test1", Value2 = "test2" },
                ValueModels1 = new List<ValueModel> { new ValueModel { Value1 = "test3", Value2 = "test4" }, new ValueModel { Value1 = "test5", Value2 = "test6" } },
                ValueModels2 = new[] { new ValueModel { Value1 = "test7", Value2 = "test8" }, new ValueModel { Value1 = "test9", Value2 = "test10" } }
            };

            var manual = Obj("ValueModel", Obj("Value1", "test1", "Value2", "test2"), "ValueModels1",
                new[]{Obj("Value1", "test3", "Value2", "test4"), Obj("Value1", "test5", "Value2", "test6")},
                "ValueModels2", new[]{Obj("Value1", "test7", "Value2", "test8"), Obj("Value1", "test9", "Value2", "test10")});

            Assert.IsTrue(manual.Equals(model.ToFaunaObj()));
        }

        [TestMethod]
        public void ReferenceTypeWithReferenceTypesTest()
        {
            var model = new ReferenceTypesReferenceModel
            {
                ReferenceModel = new ReferenceModel { Id = "test1", Indexed2 = "test2" },
                ReferenceModels1 = new List<ReferenceModel> { new ReferenceModel { Id = "test3", Indexed2 = "test4"}, new ReferenceModel { Id = "test5", Indexed2 = "test6" } },
                ReferenceModels2 = new [] { new ReferenceModel { Id = "test7", Indexed2 = "test8"}, new ReferenceModel { Id = "test9", Indexed2 = "test10" } }
            };

            var manual = Obj("ReferenceModel", Ref("test1"), 
                "ReferenceModels1", new[]{Ref("test3"), Ref("test5")},
                "ReferenceModels2", new[]{Ref("test7"), Ref("test9")});

            Assert.IsTrue(manual.Equals(model.ToFaunaObj()));
        }
    }
}