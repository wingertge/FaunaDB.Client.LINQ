using System.Threading.Tasks;
using FaunaDB.LINQ.Query;

namespace FaunaDB.LINQ.Client
{
    public interface IFaunaClient
    {
        Task<T> Query<T>(Expr query);
    }
}