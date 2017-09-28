using System.Threading.Tasks;
using FaunaDB.Query;
using FaunaDB.Types;

namespace FaunaDB.Extensions
{
    public interface IFaunaClient
    {
        Task<Value> Query(Expr query);
    }
}