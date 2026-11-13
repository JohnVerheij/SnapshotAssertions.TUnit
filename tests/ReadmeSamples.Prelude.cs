using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using SnapshotAssertions;

namespace MyLib
{
    /// <summary>A stand-in for a type of the library whose public API the README samples pin.</summary>
    public sealed class Foo
    {
    }
}

namespace ReadmeSamples
{

/// <summary>A stand-in for a protobuf message in the README samples.</summary>
public sealed class MyProto
{
}

/// <summary>A stand-in for a decoded wire message in the README samples.</summary>
public sealed class Envelope
{
    /// <summary>Gets the parser a generated protobuf message type exposes.</summary>
    public static EnvelopeParser Parser { get; } = new();

    /// <summary>Gets the message type name.</summary>
    public string TypeName { get; init; } = string.Empty;

    /// <summary>Gets the message id.</summary>
    public int Id { get; init; }

    /// <summary>Gets the flags.</summary>
    public int Flags { get; init; }

    /// <summary>Gets the payload bytes.</summary>
    public byte[] Payload { get; init; } = [];
}

/// <summary>The parser of <see cref="Envelope"/>.</summary>
public sealed class EnvelopeParser
{
    /// <summary>Decodes wire bytes.</summary>
    /// <param name="bytes">The wire bytes.</param>
    /// <returns>The decoded message.</returns>
    public Envelope ParseFrom(byte[] bytes) => new() { Payload = bytes };
}

/// <summary>A stand-in for a domain order in the README samples.</summary>
public sealed class Order
{
    /// <summary>Gets the id.</summary>
    public int Id { get; init; }

    /// <summary>Gets the status.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Gets the total.</summary>
    public decimal Total { get; init; }

    /// <summary>Gets the items.</summary>
    public IReadOnlyList<OrderItem> Items { get; init; } = [];

    /// <summary>Confirms the order.</summary>
    public void Confirm()
    {
    }

    /// <summary>Ships the order.</summary>
    public void Ship()
    {
    }

    /// <summary>Renders the state of the order.</summary>
    /// <returns>The state text.</returns>
    public string RenderState() => Status;
}

/// <summary>Renders the audit log of an order in the README samples.</summary>
public static class AuditLogRenderer
{
    /// <summary>Renders the log.</summary>
    /// <param name="order">The order.</param>
    /// <returns>The log text.</returns>
    public static string Render(Order order) => order.Status;
}

/// <summary>A stand-in for the custom scrubber of the README cookbook.</summary>
public sealed class OtelTraceIdScrubber : SnapshotScrubber
{
    /// <inheritdoc/>
    public override string Apply(string input, SnapshotScrubberState state) => input;
}

/// <summary>A stand-in for the custom scrubber of the README cookbook.</summary>
public sealed class NumericTokenScrubber(string prefix) : SnapshotScrubber
{
    /// <inheritdoc/>
    public override string Apply(string input, SnapshotScrubberState state) => input + prefix;
}

/// <summary>A line of an <see cref="Order"/>.</summary>
public sealed class OrderItem
{
    /// <summary>Gets the stock keeping unit.</summary>
    public string Sku { get; init; } = string.Empty;
}

/// <summary>A stand-in for a domain event in the README samples.</summary>
public sealed class TimelineEvent
{
    /// <summary>Gets the time stamp.</summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>Gets the kind.</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>Gets the message.</summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>The formatter the README samples call to render a <see cref="MyProto"/>.</summary>
public static class Formatter
{
    /// <summary>Renders a message.</summary>
    /// <param name="value">The message.</param>
    /// <returns>The text.</returns>
    public static string Format(MyProto value) => value.ToString() ?? string.Empty;
}

/// <summary>The JSON context the README samples serialize <see cref="Order"/> with.</summary>
[JsonSerializable(typeof(Order))]
public sealed partial class MyJsonContext : JsonSerializerContext
{
}

/// <summary>Fields and helpers the README samples use without declaring them; every sample class derives from it.</summary>
public partial class Prelude
{
    /// <summary>A captured HTTP log.</summary>
    protected string httpLog = string.Empty;

    /// <summary>Some text with ticket ids.</summary>
    protected string text = string.Empty;

    /// <summary>A pattern for ticket ids.</summary>
    protected Regex TicketIdPattern = new("ticket-[0-9]+", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);

    /// <summary>A pattern for correlation ids.</summary>
    protected Regex CorrelationIdPattern = new("corr-[0-9]+", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);

    /// <summary>A value of the stand-in message type.</summary>
    protected MyProto myProto = new();

    /// <summary>Wire bytes.</summary>
    protected byte[] wireBytes = [];

    /// <summary>An order.</summary>
    protected Order order = new();

    /// <summary>Text under test.</summary>
    protected string actual = string.Empty;

    /// <summary>Text before a change.</summary>
    protected string before = string.Empty;

    /// <summary>Text after a change.</summary>
    protected string after = string.Empty;

    /// <summary>Text before rendering.</summary>
    protected string beforeRender = string.Empty;

    /// <summary>Text after rendering.</summary>
    protected string afterRender = string.Empty;

    /// <summary>A payload.</summary>
    protected string payload = string.Empty;

    /// <summary>A response body.</summary>
    protected string body = string.Empty;

    /// <summary>A JSON response.</summary>
    protected string jsonResponse = string.Empty;

    /// <summary>A server log.</summary>
    protected string serverLog = string.Empty;

    /// <summary>A diagnostic.</summary>
    protected string diagnostic = string.Empty;

    /// <summary>Gets the repository root the samples read golden files from.</summary>
    protected static string RepoRoot => Directory.GetCurrentDirectory();

    /// <summary>Renders a diff.</summary>
    /// <param name="before">The text before.</param>
    /// <param name="after">The text after.</param>
    /// <returns>The diff text.</returns>
    protected static string RenderDiff(string before, string after) => before + after;

    /// <summary>Canonicalizes text.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The canonical text.</returns>
    protected static string Canonicalize(string text) => text;

    /// <summary>Renders a response.</summary>
    /// <param name="route">The route.</param>
    /// <param name="statusCode">The status code.</param>
    /// <returns>The response text.</returns>
    protected static string RenderResponse(string route, int statusCode) => route + statusCode;
}
}
