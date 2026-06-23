using System.Runtime.CompilerServices;
using Robust.Shared.Utility;

namespace Content.Shared._Moffstation.Extensions;

public static class EntitySystemExt
{
    extension(EntitySystem entSys)
    {
        /// Throws a debug assert and logs the given <paramref name="message"/> to the system's error logger.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AssertOrLogError(string message)
        {
            DebugTools.Assert(message);
            entSys.Log.Error(message);
        }

        /// <see cref="AssertOrLogError"/>, but returns <paramref name="ret"/> instead of void.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T AssertOrLogError<T>(string message, T ret)
        {
            entSys.AssertOrLogError(message);
            return ret;
        }
    }
}
