using System.Runtime.CompilerServices;

namespace MasteringModernCSharp.Ch07;

public record Page<T>(IReadOnlyList<T> Items, int? NextPage);

public static class PagedApi
{
    // Hides paging: callers see one flat stream of items, and
    // the next page is fetched only when they ask for more.
    public static async IAsyncEnumerable<T> GetAllAsync<T>(
        Func<int, CancellationToken, Task<Page<T>>> fetchPage,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        int? page = 1;
        while (page is int current)
        {
            var result = await fetchPage(current, ct);
            foreach (var item in result.Items)
            {
                yield return item;
            }
            page = result.NextPage;
        }
    }
}
