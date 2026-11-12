using System.Text;
using System.Text.Json;
using MasteringModernCSharp.Ch06;

namespace MasteringModernCSharp.Ch06.Tests;

public class P046_JsonSourceGenerationTests
{
    [Fact]
    public void Round_trips_with_camel_case()
    {
        var json = CatalogJson.Serialize(new("A1", "Desk", 99.5m, ["office"]));
        Assert.Equal(
            """{"sku":"A1","name":"Desk","price":99.5,"tags":["office"]}""", json);
        var list = CatalogJson.DeserializeList($"[{json}]");
        Assert.Equal("Desk", list![0].Name);
    }
}

public class P047_StrictJsonTests
{
    private const string Good =
        """{"fromAccount":"A","toAccount":"B","amount":10}""";

    [Fact]
    public void Valid_input_parses() =>
        Assert.Equal(10m, StrictJson.ParseStrict(Good)!.Amount);

    [Theory]
    [InlineData("""{"fromAccount":"A","toAccount":"B","amount":10,"amount":9999}""")]
    [InlineData("""{"fromAccount":"A","toAccount":"B","amount":10,"isAdmin":true}""")]
    [InlineData("""{"FromAccount":"A","toAccount":"B","amount":10}""")]
    public void Strict_rejects_suspicious_input(string json) =>
        Assert.Throws<JsonException>(() => StrictJson.ParseStrict(json));

    [Fact]
    public void Duplicate_check_alone_rejects_repeated_keys() =>
        Assert.Throws<JsonException>(() => StrictJson.ParseNoDuplicates(
            """{"fromAccount":"A","toAccount":"B","amount":1,"amount":2}"""));
}

public class P048_PolymorphicJsonTests
{
    [Fact]
    public void Type_discriminator_round_trips()
    {
        Notification[] items =
            [new EmailNotification("a@b.c", "Hi"), new SmsNotification("555", "Yo")];
        var json = NotificationJson.Serialize(items);
        Assert.Contains("\"type\":\"email\"", json);

        var back = NotificationJson.Deserialize(json);
        Assert.IsType<EmailNotification>(back[0]);
        Assert.Equal("Yo", ((SmsNotification)back[1]).Text);
    }
}

public class P049_NullableAndRequiredTests
{
    [Fact]
    public void Valid_form_parses() =>
        Assert.Equal("a@b.c", SignupJson.Parse("""{"email":"a@b.c"}""").Email);

    [Fact]
    public void Missing_required_property_fails() =>
        Assert.Throws<JsonException>(() => SignupJson.Parse("""{"displayName":"Ada"}"""));

    [Fact]
    public void Null_for_non_nullable_property_fails() =>
        Assert.Throws<JsonException>(() =>
            SignupJson.Parse("""{"email":"a@b.c","displayName":null}"""));

    [Fact]
    public void Null_for_nullable_property_is_fine() =>
        Assert.Null(SignupJson.Parse("""{"email":"a@b.c","referrer":null}""").Referrer);
}

public class P050_StreamingJsonTests
{
    [Fact]
    public async Task Writes_and_reads_a_stream()
    {
        var ct = TestContext.Current.CancellationToken;
        using var buffer = new MemoryStream();
        await LogStream.WriteAsync(buffer, Produce(), ct);
        buffer.Position = 0;
        Assert.Equal(500, await LogStream.CountErrorsAsync(buffer, ct));

        static async IAsyncEnumerable<LogEntry> Produce()
        {
            for (var i = 0; i < 1000; i++)
            {
                await Task.Yield();
                yield return new(DateTime.UnixEpoch, i % 2 == 0 ? "Error" : "Info", $"#{i}");
            }
        }
    }
}

public class P051_JsonSchemaTests
{
    [Fact]
    public void Schema_lists_properties_types_and_descriptions()
    {
        var schema = JsonDocument.Parse(SchemaExport.For<WeatherQuery>()).RootElement;
        var props = schema.GetProperty("properties");
        Assert.Equal("City name, e.g. Austin",
            props.GetProperty("city").GetProperty("description").GetString());
        Assert.Equal("integer", props.GetProperty("days").GetProperty("type").GetString());
        Assert.Contains("city", schema.GetProperty("required").EnumerateArray()
            .Select(e => e.GetString()));
    }
}

public class P052_GeneratedRegexTests
{
    [Fact]
    public void Validates_and_extracts()
    {
        Assert.True(Validators.LooksLikeIban("GB82 WEST 1234 5698 7654 32"));
        Assert.False(Validators.LooksLikeIban("hello"));
        Assert.Equal([new DateOnly(2026, 11, 11)],
            Validators.FindDates("Due 2026-11-11, not 2026-13-45."));
    }
}

public class P053_SearchValuesTests
{
    [Fact]
    public void Finds_any_of_many_words_or_chars()
    {
        Assert.True(ContentFilter.ContainsSecret("my API_KEY is..."));
        Assert.False(ContentFilter.ContainsSecret("hello world"));
        Assert.Equal(4, ContentFilter.FirstUnsafeChar("safe<b>"));
    }
}
