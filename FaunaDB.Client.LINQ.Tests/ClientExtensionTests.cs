using System;
using System.Reflection;
using FaunaDB.LINQ;
using FaunaDB.LINQ.Extensions;
using FaunaDB.LINQ.Query;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;
using static FaunaDB.Query.Language;

namespace FaunaDB.Client.LINQ.Tests
{
    [TestFixture]
    public class ClientExtensionTests
    {
        public ClientExtensionTests()
        {
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.None,
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
        }

        [Test]
        public void CompositeWithArgsTest()
        {
            IsolationUtils.FakeAttributeClient(CompositeWithArgsTest_Run);
            IsolationUtils.FakeManualClient(CompositeWithArgsTest_Run);
        }

        private static void CompositeWithArgsTest_Run(IDbContext client, ref Expr lastQuery)
        {
            var q = client.Query<ReferenceModel>(a => a.CompositeIndex, "test1", "test2");

            q.Provider.Execute<object>(q.Expression);
            var parsed = lastQuery;

            var manual = Map(Match(Index("composite_index"), "test1", "test2"), Lambda("arg0", Get(Var("arg0"))));

            Assert.IsTrue(JsonConvert.SerializeObject(parsed) == JsonConvert.SerializeObject(manual));
        } 

        [Test]
        public void CompositeWithTupleTest()
        {
            IsolationUtils.FakeAttributeClient(CompositeWithTupleTest_Run);
            IsolationUtils.FakeManualClient(CompositeWithTupleTest_Run);
        }

        private static void CompositeWithTupleTest_Run(IDbContext client, ref Expr lastQuery)
        {
            var q = client.Query<ReferenceModel>(a => a.CompositeIndex == Tuple.Create("test1", "test2"));

            var manual = Map(Match(Index("composite_index"), "test1", "test2"), Lambda("arg0", Get(Var("arg0"))));
            q.Provider.Execute<object>(q.Expression);

            Assert.IsTrue(JsonConvert.SerializeObject(lastQuery) == JsonConvert.SerializeObject(manual));
        }

        [Test]
        public void SingleBooleanSelectorTest()
        {
            IsolationUtils.FakeAttributeClient(SingleBooleanSelectorTest_Run);
            IsolationUtils.FakeManualClient(SingleBooleanSelectorTest_Run);
        }

        private static void SingleBooleanSelectorTest_Run(IDbContext client, ref Expr lastQuery)
        {
            var q = client.Query<ReferenceModel>(a => a.Indexed1 == "test2");
            q.Provider.Execute<object>(q.Expression);
            var parsed = lastQuery;
            var manual = Map(Match(Index("index_1"), Arr("test2")), Lambda("arg0", Get(Var("arg0"))));

            Assert.IsTrue(JsonConvert.SerializeObject(parsed) == JsonConvert.SerializeObject(manual));
        }

        [Test]
        public void MultiSelectorTest()
        {
            IsolationUtils.FakeAttributeClient(MultiSelectorTest_Run);
            IsolationUtils.FakeManualClient(MultiSelectorTest_Run);
        }

        private static void MultiSelectorTest_Run(IDbContext client, ref Expr lastQuery)
        {
            var q1 = client.Query<ReferenceModel>(a => a.Indexed1 == "test1" && a.Indexed2 == "test2");
            var q2 = client.Query<ReferenceModel>(a => a.Indexed1 == "test1" || a.Indexed2 == "test2");

            var manual1 = Map(Intersection(Match(Index("index_1"), Arr("test1")), Match(Index("index_2"), Arr("test2"))), Lambda("arg0", Get(Var("arg0"))));
            var manual2 = Map(Union(Match(Index("index_1"), Arr("test1")), Match(Index("index_2"), Arr("test2"))), Lambda("arg0", Get(Var("arg0"))));

            q1.Provider.Execute<object>(q1.Expression);
            Assert.IsTrue(JsonConvert.SerializeObject(lastQuery) == JsonConvert.SerializeObject(manual1));

            q2.Provider.Execute<object>(q2.Expression);
            Assert.IsTrue(JsonConvert.SerializeObject(lastQuery) == JsonConvert.SerializeObject(manual2));
        }

        [Test]
        public void RefQueryTest()
        {
            IsolationUtils.FakeAttributeClient(RefQueryTest_Run);
            IsolationUtils.FakeManualClient(RefQueryTest_Run);
        }

        private static void RefQueryTest_Run(IDbContext client, ref Expr lastQuery)
        {
            var q = client.Query<ReferenceModel>("ref1");

            var manual = Get(Ref("ref1"));

            q.Provider.Execute<object>(q.Expression);
            Assert.IsTrue(JsonConvert.SerializeObject(lastQuery) == JsonConvert.SerializeObject(manual));
        }

        [Test]
        public void CreateTest()
        {
            IsolationUtils.FakeAttributeClient<ReferenceModel>(CreateTest_Run);
            IsolationUtils.FakeManualClient<ReferenceModel>(CreateTest_Run);
        }

        private static void CreateTest_Run(IDbContext client, ref Expr lastQuery)
        {
            var model = new ReferenceModel {Indexed1 = "test1", Indexed2 = "test2"};
            var q = client.Create(model).Result;

            var manual = Create(Class("reference_model"), Obj("data", Obj("indexed1", "test1", "indexed2", "test2")));

            Assert.IsTrue(JsonConvert.SerializeObject(lastQuery) == JsonConvert.SerializeObject(manual));
        }

        [Test]
        public void UpdateTest()
        {
            IsolationUtils.FakeAttributeClient<ReferenceModel>(UpdateTest_Run);
            IsolationUtils.FakeManualClient<ReferenceModel>(UpdateTest_Run);
        }

        private static void UpdateTest_Run(IDbContext client, ref Expr lastQuery)
        {
            var model = new ReferenceModel {Id = "testId", Indexed1 = "test1", Indexed2 = "test2"};
            var q = client.Update(model).Result;

            var manual = Language.Update(Language.Ref(model.Id), model.ToFaunaObj(client));

            Assert.IsTrue(JsonConvert.SerializeObject(lastQuery) == JsonConvert.SerializeObject(manual));
        }

        [Test]
        public void UpsertTest()
        {
            IsolationUtils.FakeAttributeClient<ReferenceModel>(UpsertTest_Run);
            IsolationUtils.FakeManualClient<ReferenceModel>(UpsertTest_Run);
        }

        private static void UpsertTest_Run(IDbContext client, ref Expr lastQuery)
        {
            var model = new ReferenceModel { Id = "testId", Indexed1 = "test1", Indexed2 = "test2" };
            var q = client.Upsert(model);

            var manual = If(Exists(Ref(model.Id)), Update(Ref(model.Id), Obj("indexed1", "test1", "indexed2", "test2")),
                Create(Class("reference_model"), Obj("indexed1", "test1", "indexed2", "test2")));

            Assert.IsTrue(JsonConvert.SerializeObject(lastQuery) == JsonConvert.SerializeObject(manual));
        }

        [Test]
        public void DeleteTest()
        {
            IsolationUtils.FakeAttributeClient(DeleteTest_Run);
            IsolationUtils.FakeManualClient(DeleteTest_Run);
        }

        private static void DeleteTest_Run(IDbContext client, ref Expr lastQuery)
        {
            var model = new ReferenceModel { Id = "testId", Indexed1 = "test1", Indexed2 = "test2" };
            var q = client.Delete(model);

            var manual = Delete(Ref(model.Id));

            Assert.IsTrue(JsonConvert.SerializeObject(lastQuery) == JsonConvert.SerializeObject(manual));
        }

        [Test]
        public void GetTest()
        {
            IsolationUtils.FakeAttributeClient<ReferenceModel>(GetTest_Run);
            IsolationUtils.FakeManualClient<ReferenceModel>(GetTest_Run);
        }

        private static void GetTest_Run(IDbContext client, ref Expr lastQuery)
        {
            var q = client.Get<ReferenceModel>("test1");

            var manual = Get(Ref("test1"));

            Assert.IsTrue(JsonConvert.SerializeObject(lastQuery) == JsonConvert.SerializeObject(manual));
        }

        [Test]
        public void UpsertCompositeIndexWithArgsTest()
        {
            IsolationUtils.FakeAttributeClient<ReferenceModel>(UpsertCompositeIndexWithArgsTest_Run);
            IsolationUtils.FakeManualClient<ReferenceModel>(UpsertCompositeIndexWithArgsTest_Run);
        }

        private static void UpsertCompositeIndexWithArgsTest_Run(IDbContext client, ref Expr lastQuery)
        {
            var model = new ReferenceModel { Indexed1 = "test1", Indexed2 = "test2" };
            var q = client.Upsert(model, a => a.CompositeIndex, "test1", "test2");

            var obj = Obj("indexed1", "test1", "indexed2", "test2");
            var matchExpr = Match(Index("composite_index"), "test1", "test2");
            var manual = If(Exists(matchExpr), Map(matchExpr, Lambda("arg0", Update(Var("arg0"), obj))), Create(Class("reference_model"), obj));

            Assert.IsTrue(JsonConvert.SerializeObject(lastQuery) == JsonConvert.SerializeObject(manual));
        }

        [Test]
        public void UpsertBooleanExpressionTest()
        {
            IsolationUtils.FakeAttributeClient<ReferenceModel>(UpsertBooleanExpressionTest_Run);
            IsolationUtils.FakeManualClient<ReferenceModel>(UpsertBooleanExpressionTest_Run);
        }

        private static void UpsertBooleanExpressionTest_Run(IDbContext client, ref Expr lastQuery)
        {
            var model = new ReferenceModel { Indexed1 = "test1", Indexed2 = "test2" };
            var q = client.Upsert(model, a => a.Indexed1 == "test1");

            var obj = Obj("indexed1", "test1", "indexed2", "test2");
            var matchExpr = Match(Index("index_1"), Arr("test1"));
            var manual = If(Exists(matchExpr), Map(matchExpr, Lambda("arg0", Update(Var("arg0"), obj))),
                Create(Class("reference_model"), obj));

            Assert.IsTrue(JsonConvert.SerializeObject(lastQuery) == JsonConvert.SerializeObject(manual));
        }
    }
}
