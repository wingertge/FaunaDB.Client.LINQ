using System.Reflection;
using System.Threading.Tasks;
using FaunaDB.Client.Fakes;
using FaunaDB.Query;
using FaunaDB.Types;
using FaunaDB.Types.Fakes;
using Microsoft.QualityTools.Testing.Fakes;

namespace FaunaDB.Client.LINQ.Tests
{
    public static class IsolationUtils
    {
        internal delegate void TestAction(FaunaClient client, ref Expr lastExpr);

        internal delegate Task AsyncTestAction(FaunaClient client, ref Expr lastExpr);

        internal static void FakeClient(TestAction test, string json = "{}")
        {
            /*var client = Mock.Create<FaunaClient>();
            Expr lastQuery = null;

            Mock.Arrange(() => client.Query(Arg.IsAny<Expr>())).Returns<Expr>(a =>
            {
                lastQuery = a;
                return Task.FromResult(Mock.Create<Value>());
            });

            test(client, ref lastQuery);*/

            using (ShimsContext.Create())
            {
                Expr queryObj = null;
                ShimFaunaClient.AllInstances.QueryExpr = (c, q) =>
                {
                    queryObj = q;
                    var result = typeof(FaunaClient).GetMethod("FromJson", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[]{ json });
                    return Task.FromResult((Value)result);
                };

                test(new FaunaClient(""), ref queryObj);
            }
        }

        internal static Task FakeClientAsync(AsyncTestAction test)
        {
            using (ShimsContext.Create())
            {
                Expr queryObj = null;
                ShimFaunaClient.AllInstances.QueryExpr = (c, q) =>
                {
                    queryObj = q;
                    return Task.FromResult<Value>(new StubValue());
                };

                return test(new FaunaClient(""), ref queryObj);
            }
        }
    }
}