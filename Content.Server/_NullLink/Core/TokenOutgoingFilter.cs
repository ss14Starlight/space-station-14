using System.Threading.Tasks;
using Orleans;
using Orleans.Runtime;

namespace Content.Server._NullLink.Core;

public sealed class TokenOutgoingFilter(TokenHolder opt) : IOutgoingGrainCallFilter
{
    private readonly string _token = opt.Token;
    public Task Invoke(IOutgoingGrainCallContext ctx)
    {
        RequestContext.Set("token", _token);
        return ctx.Invoke();
    }
}