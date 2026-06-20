using System;
namespace Content.Shared._Starlight.Abstract.Codegen;

[AttributeUsage(AttributeTargets.Class)]
public sealed class GenerateLocalSubscriptionsAttribute<T> : Attribute
{
}
