using System;

namespace Kill_1.Common
{
    public class RateLimiteException : Exception
    {
        public RateLimiteException(string msg) : base(msg)
        {

        }

        public RateLimiteException(string msg, Exception e) : base(msg, e)
        {

        }
    }
}