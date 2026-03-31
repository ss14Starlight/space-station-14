using System.Diagnostics.CodeAnalysis;
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

        /// Throws an exception. For typechecking, returns <typeparamref name="T"/> for use in expressions, though it definitely never returns.
        [MethodImpl(MethodImplOptions.AggressiveInlining), DoesNotReturn]
        public T Unreachable<T>(string msg)
        {
            throw new Exception(msg);
        }

        /// Throws an exception.
        [MethodImpl(MethodImplOptions.AggressiveInlining), DoesNotReturn]
        public void Unreachable(string msg)
        {
            throw new Exception(msg);
        }
    }
}
