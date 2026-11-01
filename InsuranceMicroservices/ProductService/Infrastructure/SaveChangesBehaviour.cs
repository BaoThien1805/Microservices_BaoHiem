using MediatR;
using ProductService.DataAccess.EF;
namespace ProductService.Infrastructure;
public sealed class SaveChangesBehaviour<TRequest, TResponse>(ProductDbContext context)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var result = await next();
        if (context.ChangeTracker.HasChanges()) await context.SaveChangesAsync(cancellationToken);
        return result;
    }
}
