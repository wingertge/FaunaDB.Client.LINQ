using FaunaDB.Types;

namespace FaunaDB.Extensions
{
    internal class FaunaResult<T>
    {
        [FaunaField("@ref")]
        public string Ref { get; set; }
        [FaunaField("ts")]
        public long TimeStamp { get; set; }
        [FaunaField("data")]
        public T Data { get; set; }
    }
}