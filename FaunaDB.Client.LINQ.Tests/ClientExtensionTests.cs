using System;
using FaunaDB.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static FaunaDB.Extensions.Language;

namespace FaunaDB.Client.LINQ.Tests
{
    [TestClass]
    public class ClientExtensionTests
    {
        [TestMethod]
        public void CompositeWithArgsTest()
        {
            IsolationUtils.FakeClient(CompositeWithArgsTest_Run);
        }

        private static void CompositeWithArgsTest_Run(IFaunaClient client, ref Expr lastQuery)
        {
            var q = client.Query<ReferenceModel>(a => a.CompositeIndex, "test1", "test2");

            q.Provider.Execute<object>(q.Expression);
            var parsed = lastQuery;

            var manual = Map(Match(Index("composite_index"), "test1", "test2"), Lambda("arg0", Get(Var("arg0"))));

            Assert.IsTrue(parsed.Equals(manual));
        } 

        [TestMethod]
        public void CompositeWithTupleTest()
        {
            IsolationUtils.FakeClient(CompositeWithTupleTest_Run);
        }

        private static void CompositeWithTupleTest_Run(IFaunaClient client, ref Expr lastQuery)
        {
            var q = client.Query<ReferenceModel>(a => a.CompositeIndex == Tuple.Create("test1", "test2"));

            var manual = Map(Match(Index("composite_index"), "test1", "test2"), Lambda("arg0", Get(Var("arg0"))));
            q.Provider.Execute<object>(q.Expression);

            Assert.IsTrue(lastQuery.Equals(manual));
        }

        [TestMethod]
        public void SingleBooleanSelectorTest()
        {
            IsolationUtils.FakeClient(SingleBooleanSelectorTest_Run);
        }

        private static void SingleBooleanSelectorTest_Run(IFaunaClient client, ref Expr lastQuery)
        {
            var q = client.Query<ReferenceModel>(a => a.Indexed1 == "test2");
            q.Provider.Execute<object>(q.Expression);
            var parsed = lastQuery;
            var manual = Map(Match(Index("index_1"), "test2"), Lambda("arg0", Get(Var("arg0"))));

            Assert.IsTrue(parsed.Equals(manual));
        }

        [TestMethod]
        public void MultiSelectorTest()
        {
            IsolationUtils.FakeClient(MultiSelectorTest_Run);
        }

        private static void MultiSelectorTest_Run(IFaunaClient client, ref Expr lastQuery)
        {
            var q1 = client.Query<ReferenceModel>(a => a.Indexed1 == "test1" && a.Indexed2 == "test2");
            var q2 = client.Query<ReferenceModel>(a => a.Indexed1 == "test1" || a.Indexed2 == "test2");

            var manual1 = Map(Intersection(Match(Index("index_1"), "test1"), Match(Index("index_2"), "test2")), Lambda("arg0", Get(Var("arg0"))));
            var manual2 = Map(Union(Match(Index("index_1"), "test1"), Match(Index("index_2"), "test2")), Lambda("arg0", Get(Var("arg0"))));

            q1.Provider.Execute<object>(q1.Expression);
            Assert.IsTrue(lastQuery.Equals(manual1));

            q2.Provider.Execute<object>(q2.Expression);
            Assert.IsTrue(lastQuery.Equals(manual2));
        }

        [TestMethod]
        public void RefQueryTest()
        {
            IsolationUtils.FakeClient(RefQueryTest_Run);
        }

        private static void RefQueryTest_Run(IFaunaClient client, ref Expr lastQuery)
        {
            var q = client.Query<ReferenceModel>("ref1");

            var manual = Get(Ref("ref1"));

            q.Provider.Execute<object>(q.Expression);
            Assert.IsTrue(lastQuery.Equals(manual));
        }

        [TestMethod]
        public void CreateTest()
        {
            IsolationUtils.FakeClient(CreateTest_Run);
        }

        private static void CreateTest_Run(IFaunaClient client, ref Expr lastQuery)
        {
            var model = new ReferenceModel {Indexed1 = "test1", Indexed2 = "test2"};
            var q = client.Create(model).Result;

            var manual = Create(Class("reference_model"), Obj("data", model.ToFaunaObj()));

            Assert.IsTrue(lastQuery.Equals(manual));
        }

        [TestMethod]
        public void UpdateTest()
        {
            IsolationUtils.FakeClient(UpdateTest_Run);
        }

        private static void UpdateTest_Run(IFaunaClient client, ref Expr lastQuery)
        {
            var model = new ReferenceModel {Id = "testId", Indexed1 = "test1", Indexed2 = "test2"};
            var q = client.Update(model).Result;

            var manual = Update(Ref(model.Id), model.ToFaunaObj());

            Assert.IsTrue(lastQuery.Equals(manual));
        }

        [TestMethod]
        public void UpsertTest()
        {
            IsolationUtils.FakeClient(UpsertTest_Run);
        }

        private static void UpsertTest_Run(IFaunaClient client, ref Expr lastQuery)
        {
            var model = new ReferenceModel { Id = "testId", Indexed1 = "test1", Indexed2 = "test2" };
            var q = client.Upsert(model);

            var manual = If(Exist(Ref(model.Id)), Update(Ref(model.Id), model.ToFaunaObj()),
                Create(Class("reference_model"), model.ToFaunaObj()));

            Assert.IsTrue(lastQuery.Equals(manual));
        }

        [TestMethod]
        public void DeleteTest()
        {
            IsolationUtils.FakeClient(DeleteTest_Run);
        }

        private static void DeleteTest_Run(IFaunaClient client, ref Expr lastQuery)
        {
            var model = new ReferenceModel { Id = "testId", Indexed1 = "test1", Indexed2 = "test2" };
            var q = client.Delete(model);

            var manual = Delete(Ref(model.Id));

            Assert.IsTrue(lastQuery.Equals(manual));
        }

        [TestMethod]
        public void GetTest()
        {
            IsolationUtils.FakeClient(GetTest_Run);   
        }

        private static void GetTest_Run(IFaunaClient client, ref Expr lastQuery)
        {
            var q = client.Get<ReferenceModel>("test1");

            var manual = Get(Ref("test1"));

            Assert.IsTrue(lastQuery.Equals(manual));
        }

        [TestMethod]
        public void UpsertCompositeIndexWithArgsTest()
        {
            IsolationUtils.FakeClient(UpsertCompositeIndexWithArgsTest_Run);
        }

        private static void UpsertCompositeIndexWithArgsTest_Run(IFaunaClient client, ref Expr lastQuery)
        {
            var model = new ReferenceModel { Indexed1 = "test1", Indexed2 = "test2" };
            var q = client.Upsert(model, a => a.CompositeIndex, "test1", "test2");

            var matchExpr = Match(Index("composite_index"), "test1", "test2");
            var manual = If(Exist(matchExpr), Map(matchExpr, Lambda("arg0", Update(Var("arg0"), model.ToFaunaObj()))), Create(model.GetClassRef(), model.ToFaunaObj()));

            Assert.IsTrue(lastQuery.Equals(manual));
        }

        [TestMethod]
        public void UpsertBooleanExpressionTest()
        {
            IsolationUtils.FakeClient(UpsertBooleanExpressionTest_Run);
        }

        private static void UpsertBooleanExpressionTest_Run(IFaunaClient client, ref Expr lastQuery)
        {
            var model = new ReferenceModel { Indexed1 = "test1", Indexed2 = "test2" };
            var q = client.Upsert(model, a => a.Indexed1 == "test1");

            var matchExpr = Match(Index("index_1"), "test1");
            var manual = If(Exist(matchExpr), Map(matchExpr, Lambda("arg0", Update(Var("arg0"), model.ToFaunaObj()))),
                Create(model.GetClassRef(), model.ToFaunaObj()));

            Assert.IsTrue(lastQuery.Equals(manual));
        }
    }
}
