using FaunaDB.Extensions;
using FaunaDB.Types;

namespace FaunaDB.Client.LINQ.Tests
{
    public class ReferenceModel : IReferenceType
    {
        [FaunaField("@ref")]
        public string Id { get; set; }
        [FaunaField("data.indexed1"), Indexed(Name = "index_1")]
        public string Indexed1 { get; set; }
        [Indexed(Name = "index_2")]
        public string Indexed2 { get; set; }

        [Indexed(Name = "composite_index")]
        public CompositeIndex<string, string> CompositeIndex { get; set; } 
    }
}