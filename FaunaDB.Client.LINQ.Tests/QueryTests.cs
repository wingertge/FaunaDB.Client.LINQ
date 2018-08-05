using System;
using System.Linq;
using FaunaDB.LINQ;
using FaunaDB.LINQ.Extensions;
using FaunaDB.LINQ.Query;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;
using static FaunaDB.Query.Language;
using ListSortDirection = FaunaDB.LINQ.Types.ListSortDirection;

namespace FaunaDB.Client.LINQ.Tests
{
    [TestFixture]
    public class QueryTests
    {
        public QueryTests()
        {
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.None,
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            typeof(QueryTests).GetProperties();
        }

        [Test]
        public void SimplePaginateTest()
        {
            IsolationUtils.FakeAttributeClient(SimplePaginateTest_Run);
            IsolationUtils.FakeManualClient(SimplePaginateTest_Run);
        }

        private static void SimplePaginateTest_Run(IDbContext client, ref Expr lastQuery)
        {
            var q = client.Query<ReferenceModel>(a => a.Indexed1 == "test1").Paginate(size: 5);
            var manual = Paginate(Map(Match(Index("index_1"), Arr("test1")), arg0 => Get(arg0)), size: 5);

            q.Provider.Execute<object>(q.Expression);

            Assert.IsTrue(JsonConvert.SerializeObject(lastQuery) == JsonConvert.SerializeObject(manual));
        }

        [Test]
        public void FromRefPaginateTest()
        {
            IsolationUtils.FakeAttributeClient(FromRefPaginateTest_Run);
            IsolationUtils.FakeManualClient(FromRefPaginateTest_Run);
        }

        private static void FromRefPaginateTest_Run(IDbContext client, ref Expr lastQuery)
        {
            var q = client.Query<ReferenceModel>(a => a.Indexed1 == "test1").Paginate(size: 5, fromRef: "testRef");
            var manual = Paginate(Map(Match(Index("index_1"), Arr("test1")), arg0 => Get(arg0)), size: 5, after: Ref("testRef"));

            q.Provider.Execute<object>(q.Expression);

            Assert.IsTrue(JsonConvert.SerializeObject(lastQuery) == JsonConvert.SerializeObject(manual));
        }

        [Test]
        public void SortDirectionPaginateTest()
        {
            IsolationUtils.FakeAttributeClient(SortDirectionPaginateTest_Run);
            IsolationUtils.FakeManualClient(SortDirectionPaginateTest_Run);
        }

        private static void SortDirectionPaginateTest_Run(IDbContext client, ref Expr lastQuery)
        {
            var q = client.Query<ReferenceModel>(a => a.Indexed1 == "test1").Paginate(size: 5, fromRef: "testRef",
                sortDirection: ListSortDirection.Descending);

            var manual = Paginate(Map(Match(Index("index_1"), Arr("test1")), arg0 => Get(arg0)), size: 5, before: Ref("testRef"));

            q.Provider.Execute<object>(q.Expression);

            Assert.IsTrue(JsonConvert.SerializeObject(lastQuery) == JsonConvert.SerializeObject(manual));
        }

        [Test]
        public void DateTimePaginateTest()
        {
            IsolationUtils.FakeAttributeClient(DateTimePaginateTest_Run);
            IsolationUtils.FakeManualClient(DateTimePaginateTest_Run);
        }

        private static void DateTimePaginateTest_Run(IDbContext client, ref Expr lastQuery)
        {
            var q = client.Query<ReferenceModel>(a => a.Indexed1 == "test1").Paginate(size: 5, timeStamp: new DateTime(2017, 1, 1));

            var manual = Paginate(Map(Match(Index("index_1"), Arr("test1")), arg0 => Get(arg0)), size: 5, ts: Time(new DateTime(2017, 1, 1).ToString("O")));

            q.Provider.Execute<object>(q.Expression);

            Assert.IsTrue(JsonConvert.SerializeObject(lastQuery) == JsonConvert.SerializeObject(manual));
        }

        [Test]
        public void AllOptionsPaginateTest()
        {
            IsolationUtils.FakeAttributeClient(AllOptionsPaginateTest_Run);
            IsolationUtils.FakeManualClient(AllOptionsPaginateTest_Run);
        }

        private static void AllOptionsPaginateTest_Run(IDbContext client, ref Expr lastQuery)
        {
            var q = client.Query<ReferenceModel>(a => a.Indexed1 == "test1").Paginate(size: 5, timeStamp: new DateTime(2017, 1, 1), fromRef: "testRef", sortDirection: ListSortDirection.Descending);

            var manual = Paginate(Map(Match(Index("index_1"), Arr("test1")), arg0 => Get(arg0)), size: 5, ts: Time(new DateTime(2017, 1, 1).ToString("O")), before: Ref("testRef"));

            q.Provider.Execute<object>(q.Expression);

            Assert.IsTrue(JsonConvert.SerializeObject(lastQuery) == JsonConvert.SerializeObject(manual));
        }

        [Test]
        public void CatchAllWhereTest()
        {
            IsolationUtils.FakeAttributeClient(CatchAllWhereTest_Run);
            IsolationUtils.FakeManualClient(CatchAllWhereTest_Run);
        }

        private static void CatchAllWhereTest_Run(IDbContext client, ref Expr lastQuery)
        {
            var i1 = 1;
            var i2 = 2;
            var q = client.Query<ReferenceModel>(a => a.Indexed1 == "test1").Where(a =>
                (a.Indexed1 == "test1" && a.Indexed2 != "test2") ||
                (i2 > i1 && i1 < i2 && i1 <= i2 && i2 >= i1));

            var manual = Filter(Map(Match(Index("index_1"), Arr("test1")), arg0 => Get(arg0)), arg1 => Or(
                And(
                    EqualsFn(
                        Select(Arr("data", "indexed1"), arg1), "test1"
                    ),
                    Not(EqualsFn(Select(Arr("data", "indexed2"), arg1), "test2"))
                ),
                And(
                    And(
                        And(
                            GT(2, 1),
                            LT(1, 2)
                        ),
                        LTE(1, 2)
                    ),
                    GTE(2, 1)
                )
            ));

            q.Provider.Execute<object>(q.Expression);

            Assert.IsTrue(JsonConvert.SerializeObject(lastQuery) == JsonConvert.SerializeObject(manual));
        }

        [Test]
        public void SelectStringConcatTest()
        {
            IsolationUtils.FakeAttributeClient(SelectStringConcatTest_Run);
            IsolationUtils.FakeManualClient(SelectStringConcatTest_Run);
        }

        private static void SelectStringConcatTest_Run(IDbContext client, ref Expr lastQuery)
        {
            var q = client.Query<ReferenceModel>(a => a.Indexed1 == "test1").Select(a => a.Indexed1 + "concat");

            var manual = Map(Map(Match(Index("index_1"), Arr("test1")), arg0 => Get(arg0)),
                arg1 => Concat(Arr(Select(Arr("data", "indexed1"), arg1), "concat")));

            q.Provider.Execute<object>(q.Expression);

            Assert.IsTrue(JsonConvert.SerializeObject(lastQuery) == JsonConvert.SerializeObject(manual));
        }

        [Test]
        public void MemberInitTest()
        {
            IsolationUtils.FakeAttributeClient(MemberInitTest_Run);
            IsolationUtils.FakeManualClient(MemberInitTest_Run);
        }

        private static void MemberInitTest_Run(IDbContext client, ref Expr lastQuery)
        {
            var q = client.Query<ReferenceModel>(a => a.Indexed1 == "test1").Where(a => a == new ReferenceModel {Indexed1 = "test1"});
            var manual = Filter(Map(Match(Index("index_1"), Arr("test1")), arg0 => Get(arg0)), arg1 => EqualsFn(arg1, Obj("indexed1", "test1", "indexed2", Null())));

            q.Provider.Execute<object>(q.Expression);

            Assert.IsTrue(JsonConvert.SerializeObject(lastQuery) == JsonConvert.SerializeObject(manual));
        }

        [Test]
        public void ChainedQueryTest()
        {
            IsolationUtils.FakeAttributeClient(ChainedQueryTest_Run);
            IsolationUtils.FakeManualClient(ChainedQueryTest_Run);
        }

        public static void ChainedQueryTest_Run(IDbContext client, ref Expr lastQuery)
        {
            var q = client.Query<ReferenceModel>(a => a.Indexed1 == "test1").Where(a => a.Indexed1 == "test1").Select(a => a.Indexed1);

            var selectorManual = Map(Match(Index("index_1"), Arr("test1")), arg0 => Get(arg0));
            var filterManual = Filter(selectorManual, arg1 => EqualsFn(Select(Arr("data", "indexed1"), arg1), "test1"));
            var selectManual = Map(filterManual, arg2 => Select(Arr("data", "indexed1"), arg2));
            var manual = selectManual;

            q.Provider.Execute<object>(q.Expression);

            Assert.IsTrue(JsonConvert.SerializeObject(lastQuery) == JsonConvert.SerializeObject(manual));
        }
    }
}