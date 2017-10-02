using System.Threading.Tasks;
using FaunaDB.Extensions;
using Moq;
using Newtonsoft.Json;

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
                return Task.FromResult(JsonConvert.DeserializeObject(json));
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
                return Task.FromResult(JsonConvert.DeserializeObject<T>(json));
            });

            test(mock.Object, ref lastQuery);
        }
    }
}