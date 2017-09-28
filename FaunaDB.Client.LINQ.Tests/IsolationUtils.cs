using System.Reflection;
using System.Threading.Tasks;
using FaunaDB.Extensions;
using FaunaDB.Query;
using FaunaDB.Types;
using Moq;

namespace FaunaDB.Client.LINQ.Tests
{
    public static class IsolationUtils
    {
        internal delegate void TestAction(IFaunaClient client, ref Expr lastExpr);

        internal static void FakeClient(TestAction test, string json = "{}")
        {
            var mock = new Mock<IFaunaClient>();
            Expr lastQuery = null;
            mock.Setup(a => a.Query(It.IsAny<Expr>())).Returns((Expr q) =>
            {
                lastQuery = q;
                var result = typeof(FaunaClient).GetMethod("FromJson", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { json });
                return Task.FromResult((Value)result);
            });

            test(mock.Object, ref lastQuery);
        }
    }
}