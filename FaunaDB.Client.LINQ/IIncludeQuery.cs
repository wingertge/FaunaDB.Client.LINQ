using System.Linq;

namespace FaunaDB.Extensions
{
    public interface IIncludeQuery<out TQuery, TSelected> : IQueryable<TQuery>
    {
        
    }
}