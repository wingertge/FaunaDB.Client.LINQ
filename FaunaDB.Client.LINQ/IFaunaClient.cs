using System.Threading.Tasks;

namespace FaunaDB.Extensions
{
    public interface IFaunaClient
    {
        Task<object> Query(Expr query);
        Task<T> Query<T>(Expr query);
    }
}