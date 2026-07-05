using FluentAssertions;
using Harrbor.Abstractions.Providers;
using Xunit;

namespace Harrbor.Tests.Unit.Abstractions;

public class FieldSchemaBuilderTests
{
    private sealed class SampleSettings
    {
        [Field(Kind = FieldKind.Url, Required = true, Label = "Base URL", Order = 1)]
        public string BaseUrl { get; set; } = "";

        [Field(Kind = FieldKind.Password, Secret = true, Required = true, Order = 2)]
        public string ApiKey { get; set; } = "";

        [Field(Min = 1, Max = 65535, Order = 3)]
        public int Port { get; set; } = 22;

        [Field(Order = 4)]
        public bool Enabled { get; set; } = true;

        [Field(ElementForbidWhitespace = true, ElementForbiddenChars = "\"'", Order = 5)]
        public List<string> Flags { get; set; } = [];

        public string NotAField { get; set; } = "ignored";
    }

    [Fact]
    public void Build_ReflectsAnnotatedPropertiesInOrder()
    {
        var schema = FieldSchemaBuilder.Build(typeof(SampleSettings));
        schema.Select(f => f.Name).Should().Equal("baseUrl", "apiKey", "port", "enabled", "flags");
    }

    [Fact]
    public void Build_IgnoresUnannotatedProperties()
    {
        FieldSchemaBuilder.Build(typeof(SampleSettings)).Should().NotContain(f => f.ClrName == "NotAField");
    }

    [Fact]
    public void Build_InfersFieldTypeFromClrType()
    {
        var schema = FieldSchemaBuilder.Build(typeof(SampleSettings));
        schema.Single(f => f.Name == "baseUrl").Type.Should().Be(FieldType.String);
        schema.Single(f => f.Name == "port").Type.Should().Be(FieldType.Int);
        schema.Single(f => f.Name == "enabled").Type.Should().Be(FieldType.Bool);
        schema.Single(f => f.Name == "flags").Type.Should().Be(FieldType.StringList);
    }

    [Fact]
    public void Build_CapturesExplicitKindSecretAndRequired()
    {
        var apiKey = FieldSchemaBuilder.Build(typeof(SampleSettings)).Single(f => f.Name == "apiKey");
        apiKey.Kind.Should().Be(FieldKind.Password);
        apiKey.Secret.Should().BeTrue();
        apiKey.Required.Should().BeTrue();
    }

    [Fact]
    public void Build_CamelCasesWireNameKeepsClrName()
    {
        var field = FieldSchemaBuilder.Build(typeof(SampleSettings)).Single(f => f.Name == "baseUrl");
        field.ClrName.Should().Be("BaseUrl");
        field.Label.Should().Be("Base URL");
    }

    [Fact]
    public void Build_DefaultsKindFromTypeWhenAuto()
    {
        FieldSchemaBuilder.Build(typeof(SampleSettings)).Single(f => f.Name == "port").Kind.Should().Be(FieldKind.Int);
    }

    [Fact]
    public void Build_SnapshotsDefaultsFromFreshInstance()
    {
        var schema = FieldSchemaBuilder.Build(typeof(SampleSettings));
        schema.Single(f => f.Name == "port").Default.Should().Be(22);
        schema.Single(f => f.Name == "enabled").Default.Should().Be(true);
        schema.Single(f => f.Name == "flags").Default.Should().BeAssignableTo<IEnumerable<string>>();
    }

    [Fact]
    public void Build_CapturesRangeBounds()
    {
        var port = FieldSchemaBuilder.Build(typeof(SampleSettings)).Single(f => f.Name == "port");
        port.Min.Should().Be(1);
        port.Max.Should().Be(65535);
    }

    [Fact]
    public void Build_CapturesListElementRules()
    {
        var flags = FieldSchemaBuilder.Build(typeof(SampleSettings)).Single(f => f.Name == "flags");
        flags.Element.Should().NotBeNull();
        flags.Element!.ForbidWhitespace.Should().BeTrue();
        flags.Element.ForbiddenChars.Should().Be("\"'");
    }

    [Fact]
    public void Build_LeavesRangeAndElementUnsetWhenNotDeclared()
    {
        var baseUrl = FieldSchemaBuilder.Build(typeof(SampleSettings)).Single(f => f.Name == "baseUrl");
        baseUrl.Min.Should().BeNull();
        baseUrl.Max.Should().BeNull();
        baseUrl.Element.Should().BeNull();
    }

    private sealed class UnsupportedSettings
    {
        [Field] public DateTime When { get; set; }
    }

    [Fact]
    public void Build_ThrowsForUnsupportedType()
    {
        var act = () => FieldSchemaBuilder.Build(typeof(UnsupportedSettings));
        act.Should().Throw<NotSupportedException>();
    }
}
