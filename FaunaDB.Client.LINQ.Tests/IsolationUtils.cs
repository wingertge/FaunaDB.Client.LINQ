using System.Threading.Tasks;
using FaunaDB.LINQ.Client;
using FaunaDB.LINQ.Extensions;
using FaunaDB.LINQ.Query;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FaunaDB.Client.LINQ.Tests
{
    public static class IsolationUtils
    {
        internal delegate void TestAction(IFaunaClient client, ref Expr lastExpr);

        internal static void FakeClient(TestAction test, string json = "{}")
        {
            var mock = new Mock<IFaunaClient>();
            Expr lastQuery = null;
            mock.Setup(a => a.Query<object>(It.IsAny<Expr>())).Returns((Expr q) =>
            {
                lastQuery = q;
                return Task.FromResult(SerializationExtensions.Decode(json, typeof(object)));
            });

            test(mock.Object, ref lastQuery);
        }

        internal static void FakeClient<T>(TestAction test, string json = "{}")
        {
            var mock = new Mock<IFaunaClient>();
            Expr lastQuery = null;
            mock.Setup(a => a.Query<T>(It.IsAny<Expr>())).Returns((Expr q) =>
            {
                lastQuery = q;
                return Task.FromResult(JObject.Parse(json).Decode(typeof(T)));
            });

            test(mock.Object, ref lastQuery);
        }
    }
}