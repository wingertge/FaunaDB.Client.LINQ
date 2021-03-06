﻿using System;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using FaunaDB.Extensions;
using FaunaDB.Query;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static FaunaDB.Query.Language;

namespace FaunaDB.Client.LINQ.Tests
{
    [TestClass]
    public class QueryTests
    {
        [TestMethod]
        public void SimplePaginateTest()
        {
            IsolationUtils.FakeClient(SimplePaginateTest_Run);
        }

        private static void SimplePaginateTest_Run(IFaunaClient client, ref Expr lastQuery)
        {
            var q = client.Query<ReferenceModel>(a => a.Indexed1 == "test1").Paginate(size: 5);
            var manual = Paginate(Map(Match(Index("index_1"), "test1"), arg0 => Get(arg0)), size: 5);

            q.Provider.Execute<object>(q.Expression);

            Assert.IsTrue(lastQuery.Equals(manual));
        }

        [TestMethod]
        public void FromRefPaginateTest()
        {
            IsolationUtils.FakeClient(FromRefPaginateTest_Run);
        }

        private static void FromRefPaginateTest_Run(IFaunaClient client, ref Expr lastQuery)
        {
            var q = client.Query<ReferenceModel>(a => a.Indexed1 == "test1").Paginate(size: 5, fromRef: "testRef");
            var manual = Paginate(Map(Match(Index("index_1"), "test1"), arg0 => Get(arg0)), size: 5, after: Ref("testRef"));

            q.Provider.Execute<object>(q.Expression);

            Assert.IsTrue(lastQuery.Equals(manual));
        }

        [TestMethod]
        public void SortDirectionPaginateTest()
        {
            IsolationUtils.FakeClient(SortDirectionPaginateTest_Run);
        }

        private static void SortDirectionPaginateTest_Run(IFaunaClient client, ref Expr lastQuery)
        {
            var q = client.Query<ReferenceModel>(a => a.Indexed1 == "test1").Paginate(size: 5, fromRef: "testRef",
                sortDirection: ListSortDirection.Descending);

            var manual = Paginate(Map(Match(Index("index_1"), "test1"), arg0 => Get(arg0)), size: 5, before: Ref("testRef"));

            q.Provider.Execute<object>(q.Expression);

            Assert.IsTrue(lastQuery.Equals(manual));
        }

        [TestMethod]
        public void DateTimePaginateTest()
        {
            IsolationUtils.FakeClient(DateTimePaginateTest_Run);
        }

        private static void DateTimePaginateTest_Run(IFaunaClient client, ref Expr lastQuery)
        {
            var q = client.Query<ReferenceModel>(a => a.Indexed1 == "test1").Paginate(size: 5, timeStamp: new DateTime(2017, 1, 1));

            var manual = Paginate(Map(Match(Index("index_1"), "test1"), arg0 => Get(arg0)), size: 5, ts: Time(new DateTime(2017, 1, 1).ToString("O")));

            q.Provider.Execute<object>(q.Expression);

            Assert.IsTrue(lastQuery.Equals(manual));
        }

        [TestMethod]
        public void AllOptionsPaginateTest()
        {
            IsolationUtils.FakeClient(AllOptionsPaginateTest_Run);
        }

        private static void AllOptionsPaginateTest_Run(IFaunaClient client, ref Expr lastQuery)
        {
            var q = client.Query<ReferenceModel>(a => a.Indexed1 == "test1").Paginate(size: 5, timeStamp: new DateTime(2017, 1, 1), fromRef: "testRef", sortDirection: ListSortDirection.Descending);

            var manual = Paginate(Map(Match(Index("index_1"), "test1"), arg0 => Get(arg0)), size: 5, ts: Time(new DateTime(2017, 1, 1).ToString("O")), before: Ref("testRef"));

            q.Provider.Execute<object>(q.Expression);

            Assert.IsTrue(lastQuery.Equals(manual));
        }

        [TestMethod]
        public void CatchAllWhereTest()
        {
            IsolationUtils.FakeClient(CatchAllWhereTest_Run);
        }

        private static void CatchAllWhereTest_Run(IFaunaClient client, ref Expr lastQuery)
        {
            var i1 = 1;
            var i2 = 2;
            var q = client.Query<ReferenceModel>(a => a.Indexed1 == "test1").Where(a =>
                (a.Indexed1 == "test1" && a.Indexed2 != "test2") ||
                (i2 > i1 && i1 < i2 && i1 <= i2 && i2 >= i1));

            var manual = Filter(Map(Match(Index("index_1"), "test1"), arg0 => Get(arg0)), arg1 => Or(
                And(
                    EqualsFn(
                        Select(Arr("data", "indexed1"), arg1), "test1"
                    ),
                    Not(EqualsFn(Select(Arr("data", "Indexed2"), arg1), "test2"))
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

            Assert.IsTrue(lastQuery.Equals(manual));
        }

        [TestMethod]
        public void SelectStringConcatTest()
        {
            IsolationUtils.FakeClient(SelectStringConcatTest_Run);
        }

        private static void SelectStringConcatTest_Run(IFaunaClient client, ref Expr lastQuery)
        {
            var q = client.Query<ReferenceModel>(a => a.Indexed1 == "test1").Select(a => a.Indexed1 + "concat");

            var manual = Map(Map(Match(Index("index_1"), "test1"), arg0 => Get(arg0)),
                arg1 => Concat(Arr(Select(Arr("data", "indexed1"), arg1), "concat")));

            q.Provider.Execute<object>(q.Expression);

            Assert.IsTrue(lastQuery.Equals(manual));
        }

        [TestMethod]
        public void MemberInitTest()
        {
            IsolationUtils.FakeClient(MemberInitTest_Run);
        }

        private static void MemberInitTest_Run(IFaunaClient client, ref Expr lastQuery)
        {
            var q = client.Query<ReferenceModel>(a => a.Indexed1 == "test1").Where(a => a == new ReferenceModel {Indexed1 = "test1"});
            var manual = Filter(Map(Match(Index("index_1"), "test1"), arg0 => Get(arg0)), arg1 => EqualsFn(arg1, Obj("indexed1", "test1", "Indexed2", Null())));

            q.Provider.Execute<object>(q.Expression);

            Assert.IsTrue(lastQuery.Equals(manual));
        }

        [TestMethod]
        public void ChainedQueryTest()
        {
            IsolationUtils.FakeClient(ChainedQueryTest_Run);
        }

        public static void ChainedQueryTest_Run(IFaunaClient client, ref Expr lastQuery)
        {
            var q = client.Query<ReferenceModel>(a => a.Indexed1 == "test1").Where(a => a.Indexed1 == "test1").Select(a => a.Indexed1);

            var selectorManual = Map(Match(Index("index_1"), "test1"), arg0 => Get(arg0));
            var filterManual = Filter(selectorManual, arg1 => EqualsFn(Select(Arr("data", "indexed1"), arg1), "test1"));
            var selectManual = Map(filterManual, arg2 => Select(Arr("data", "indexed1"), arg2));
            var manual = selectManual;

            q.Provider.Execute<object>(q.Expression);

            Assert.IsTrue(manual.Equals(lastQuery));
        }
    }
}