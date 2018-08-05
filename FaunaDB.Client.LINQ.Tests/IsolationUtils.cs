using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FaunaDB.LINQ;
using FaunaDB.LINQ.Client;
using FaunaDB.LINQ.Extensions;
using FaunaDB.LINQ.Modeling;
using FaunaDB.LINQ.Query;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FaunaDB.Client.LINQ.Tests
{
    public static class IsolationUtils
    {
        internal delegate void TestAction(IDbContext context, ref Expr lastExpr);

        private static readonly Dictionary<Type, TypeConfiguration> AttributeMappings;
        private static readonly Dictionary<Type, TypeConfiguration> ManualMappings;
        private static readonly IFaunaClient Client;

        static IsolationUtils()
        {
            var mockClient = new Mock<IFaunaClient>();
            var context = CreateAttributeContext(mockClient.Object);
            var manualContext = CreateMappingContext(mockClient.Object);
            AttributeMappings = context.Mappings;
            ManualMappings = manualContext.Mappings;
            Client = mockClient.Object;
        }

        internal static void FakeAttributeClient(TestAction test, string json = "{}")
        {
            var mock = new Mock<DbContext>(Client, AttributeMappings) {CallBase = true};
            Expr lastQuery = null;
            mock.Setup(a => a.Query<object>(It.IsAny<Expr>())).Returns((Expr q) =>
            {
                lastQuery = q;
                return Task.FromResult(SerializationExtensions.Decode(json, typeof(object), mock.Object));
            });

            test(mock.Object, ref lastQuery);
        }

        internal static void FakeManualClient(TestAction test, string json = "{}")
        {
            var mock = new Mock<DbContext>(Client, ManualMappings) { CallBase = true };
            Expr lastQuery = null;
            mock.Setup(a => a.Query<object>(It.IsAny<Expr>())).Returns((Expr q) =>
            {
                lastQuery = q;
                return Task.FromResult(SerializationExtensions.Decode(json, typeof(object), mock.Object));
            });

            test(mock.Object, ref lastQuery);
        }

        internal static void FakeAttributeClient<T>(TestAction test, string json = "{}")
        {
            var mock = new Mock<DbContext>(Client, AttributeMappings) { CallBase = true };
            Expr lastQuery = null;
            mock.Setup(a => a.Query<T>(It.IsAny<Expr>())).Returns((Expr q) =>
            {
                lastQuery = q;
                return Task.FromResult(JObject.Parse(json).Decode(typeof(T), mock.Object));
            });

            test(mock.Object, ref lastQuery);
        }

        internal static void FakeManualClient<T>(TestAction test, string json = "{}")
        {
            var mock = new Mock<DbContext>(Client, ManualMappings) { CallBase = true };
            Expr lastQuery = null;
            mock.Setup(a => a.Query<T>(It.IsAny<Expr>())).Returns((Expr q) =>
            {
                lastQuery = q;
                return Task.FromResult(JObject.Parse(json).Decode(typeof(T), mock.Object));
            });

            test(mock.Object, ref lastQuery);
        }

        private static IDbContext CreateAttributeContext(IFaunaClient mock)
        {
            var builder = DbContext.StartBuilding(mock);
            builder.RegisterReferenceModel<ReferenceModel>();
            builder.RegisterReferenceModel<PrimitivesReferenceModel>();
            builder.RegisterReferenceModel<ReferenceTypesReferenceModel>();
            builder.RegisterReferenceModel<ValueTypesReferenceModel>();
            return builder.Build();
        }

        private static IDbContext CreateMappingContext(IFaunaClient mock)
        {
            var builder = DbContext.StartBuilding(mock);
            builder.RegisterMapping<ReferenceModelMapping, ReferenceModel>();
            builder.RegisterMapping<PrimitivesReferenceModelMapping, PrimitivesReferenceModel>();
            builder.RegisterMapping<ReferenceTypesReferenceModelMapping, ReferenceTypesReferenceModel>();
            builder.RegisterMapping<ValueTypesReferenceModelMapping, ValueTypesReferenceModel>();
            return builder.Build();
        }
    }
}