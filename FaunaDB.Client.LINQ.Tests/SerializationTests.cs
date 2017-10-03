using System;
using System.Collections.Generic;
using FaunaDB.LINQ.Client;
using FaunaDB.LINQ.Extensions;
using FaunaDB.LINQ.Query;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using static FaunaDB.Query.Language;

namespace FaunaDB.Client.LINQ.Tests
{
    [TestClass]
    public class SerializationTests
    {
        public SerializationTests()
        {
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.None,
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
        }

        [TestMethod]
        public void BasicReferenceTypeSerializationTest()
        {
            var model = new ReferenceModel { Indexed1 = "test1", Indexed2 = "test2" }; // Indexed1: name attr "indexed1" | Indexed2: default name

            var manual = Obj("indexed1", "test1", "indexed2", "test2");
            var auto = model.ToFaunaObj();

            Assert.IsTrue(JsonConvert.SerializeObject(manual) == JsonConvert.SerializeObject(auto));
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

            var dict = new Dictionary<string, Query.Expr>
            {
                {"string_val", "test1"},
                {"int_val", 234},
                {"boolean_val", true},
                {"byte_val", 10},
                {"long_val", 123},
                {"float_val", 234.5f},
                {"double_val", 123.4},
                {"date_time_val", Time(DateTime.MaxValue.ToString("O"))},
                {"short_val", 5},
                {"u_short_val", 3},
                {"u_int_val", 2},
                {"u_long_val", 4}, // bizarre issue with conversion, ulong will implicit to double for some reason
                {"s_byte_val", 1},
                {"char_val", "a"},
            };

            var manual = Obj(dict);
            var auto = model.ToFaunaObj();

            Assert.IsTrue(JsonConvert.SerializeObject(manual) == JsonConvert.SerializeObject(auto));
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

            var manual = Obj("value_model", Obj("value1", "test1", "value2", "test2"), "value_models1",
                Arr(Obj("value1", "test3", "value2", "test4"), Obj("value1", "test5", "value2", "test6")),
                "value_models2", Arr(Obj("value1", "test7", "value2", "test8"), Obj("value1", "test9", "value2", "test10")));

            Assert.IsTrue(JsonConvert.SerializeObject(manual) == JsonConvert.SerializeObject(model.ToFaunaObj()));
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

            var manual = Obj("reference_model", Ref("test1"), 
                "reference_models1", Arr(Ref("test3"), Ref("test5")),
                "reference_models2", Arr(Ref("test7"), Ref("test9")));

            Assert.IsTrue(JsonConvert.SerializeObject(manual) == JsonConvert.SerializeObject(model.ToFaunaObj()));
        }

        [TestMethod]
        public void SimpleDecodeTest()
        {
            var json = "{\"data\":{\"indexed1\":\"test1\",\"indexed2\":\"test2\"}}";

            IsolationUtils.FakeClient<ReferenceModel>(SimpleDecodeTest_Run, json);
        }

        private static void SimpleDecodeTest_Run(IFaunaClient client, ref Expr lastQuery)
        {
            var model = new ReferenceModel { Indexed1 = "test1", Indexed2 = "test2" };

            var result = client.Get<ReferenceModel>("testRef").Result;

            Assert.AreEqual(model, result);
        }

        [TestMethod]
        public void ReferenceTypeWithAllPrimitivesDecodeTest()
        {
            var dict = new Dictionary<string, Query.Expr>
            {
                {"string_val", "test1"},
                {"int_val", 234},
                {"boolean_val", true},
                {"byte_val", 10},
                {"long_val", 123},
                {"float_val", 234.5f},
                {"double_val", 123.4},
                {"date_time_val", Ts(new DateTime(2010, 1, 1))},
                {"short_val", 5},
                {"u_short_val", 3},
                {"u_int_val", 2},
                {"u_long_val", 4}, // bizarre issue with conversion, ulong will implicit to double for some reason
                {"s_byte_val", 1},
                {"char_val", "a"},
            };

            IsolationUtils.FakeClient<PrimitivesReferenceModel>(ReferenceTypeWithAllPrimitivesDecodeTest_Run, JsonConvert.SerializeObject(new { data = dict }));
        }

        private static void ReferenceTypeWithAllPrimitivesDecodeTest_Run(IFaunaClient client, ref Expr lastQuery)
        {
            var model = new PrimitivesReferenceModel
            {
                BooleanVal = true,
                ByteVal = 10,
                CharVal = 'a',
                DateTimeVal = new DateTime(2010, 1, 1),
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

            var result = client.Get<PrimitivesReferenceModel>("test1").Result;

            Assert.AreEqual(model, result);
        }
    }
}