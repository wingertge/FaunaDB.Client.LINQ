using System;

namespace FaunaDB.LINQ.Errors
{
    public class UnsupportedMethodException : Exception
    {
        public UnsupportedMethodException(string methodName, string message = null) : base($"Unsupported method {methodName}: {message}") { }
    }
}