using System;

namespace ND.Framework
{
    internal static class GameCalendarSeed
    {
        public static uint Create()
        {
            var bytes = Guid.NewGuid().ToByteArray();
            uint hash = 2166136261u;
            for (var index = 0; index < bytes.Length; index++)
            {
                hash ^= bytes[index];
                hash *= 16777619u;
            }

            return hash == 0 ? 1u : hash;
        }
    }
}
