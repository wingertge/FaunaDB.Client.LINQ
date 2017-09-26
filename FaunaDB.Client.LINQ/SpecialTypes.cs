using System;
using FaunaDB.Query;

namespace FaunaDB.Extensions
{
    public class Ref : IEquatable<string>
    {
        public string Value { get; set; }

        public static implicit operator string(Ref @ref) => @ref.Value;
        public static implicit operator Ref(string s) => new Ref(s);
        public static implicit operator Expr(Ref @ref) => Language.Ref(@ref.Value);

        public Ref() { }

        public Ref(string s)
        {
            Value = s;
        }

        public bool Equals(string other)
        {
            return Value == other;
        }
    }
}