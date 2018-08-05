using System;

namespace FaunaDB.LINQ
{
    public class InvalidMappingException : Exception
    {
        public InvalidMappingException(string message) : base(message)
        {
        }
    }
}