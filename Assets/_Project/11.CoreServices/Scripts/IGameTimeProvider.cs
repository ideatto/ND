using System;

namespace ND.Framework
{
    public interface IGameTimeProvider
    {
        DateTime CurrentUtc { get; }
    }
}
