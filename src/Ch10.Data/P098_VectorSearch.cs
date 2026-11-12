using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;

namespace MasteringModernCSharp.Ch10;

public class Article
{
    public int Id { get; set; }
    public required string Title { get; set; }

    // SQL Server 2025 / Azure SQL native vector type. The size
    // must match your embedding model (1536 for many models).
    [Column(TypeName = "vector(1536)")]
    public required SqlVector<float> Embedding { get; set; }
}

public class KnowledgeBaseDb(DbContextOptions<KnowledgeBaseDb> options)
    : DbContext(options)
{
    public DbSet<Article> Articles => Set<Article>();
}

public static class SemanticSearch
{
    // The closest articles to a query embedding, computed by the
    // database with VECTOR_DISTANCE.
    public static IQueryable<string> Nearest(
        KnowledgeBaseDb db, float[] queryEmbedding, int count = 5)
    {
        var query = new SqlVector<float>(queryEmbedding);
        return db.Articles
            .OrderBy(a => EF.Functions.VectorDistance(
                "cosine", a.Embedding, query))
            .Take(count)
            .Select(a => a.Title);
    }
}
