using AdNoir.Domain.Abstractions.Repositories;

namespace AdNoir.Sample.Domain.SampleItems;

/// <summary>
/// Repository contract for <see cref="SampleItem"/> (interface in Domain,
/// implementation in Infrastructure).
/// </summary>
public interface ISampleItemRepository : IRepository<SampleItem>
{
    // The base interface provides all necessary CRUD operations. No additional methods are
    // required for the demonstration service.
}
