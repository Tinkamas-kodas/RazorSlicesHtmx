using Apskaita.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Tests;

/// <summary>
/// Creates isolated <see cref="ApskaitaDbContext"/> instances backed by the EF In-Memory provider.
/// Each call returns a context with its own store; use a single instance across arrange/act/assert
/// so the in-memory data is shared within a test.
/// </summary>
public static class TestDb
{
    public static ApskaitaDbContext New() =>
        new(new DbContextOptionsBuilder<ApskaitaDbContext>()
            .UseInMemoryDatabase($"apskaita-{Guid.NewGuid():N}")
            .Options);
}
