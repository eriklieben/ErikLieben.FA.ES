using System;
using System.Linq;
using System.Reflection;
using ErikLieben.FA.ES.Azure.Functions.Worker.Extensions;
using Microsoft.Azure.Functions.Worker.Converters;
using Microsoft.Azure.Functions.Worker.Extensions.Abstractions;
using Xunit;

namespace ErikLieben.FA.ES.Azure.Functions.Worker.Extensions.Tests;

public class EventStreamInputAttributeTests
{
    // [Fact]
    // public void Assembly_should_have_ExtensionInformation_attribute()
    // {
    //     var asm = typeof(EventStreamInputAttribute).Assembly;
    //     var attr = asm.GetCustomAttributes().OfType<ExtensionInformationAttribute>().FirstOrDefault();
    //     Assert.NotNull(attr);
    //     Assert.Equal("ErikLieben.FA.ES.WebJobs.Isolated.Extensions.Tests", attr.Name);
    //     Assert.Equal("0.0.1", attr.Version);
    // }

    [Fact]
    public void Attribute_should_have_expected_metadata_attributes()
    {
        var t = typeof(EventStreamInputAttribute);
        var usage = t.GetCustomAttribute<AttributeUsageAttribute>();
        Assert.NotNull(usage);
        Assert.True(usage!.ValidOn.HasFlag(AttributeTargets.Parameter));
        Assert.True(usage.ValidOn.HasFlag(AttributeTargets.ReturnValue));

        var inputConv = t.GetCustomAttribute<InputConverterAttribute>();
        Assert.NotNull(inputConv);
        Assert.Equal(typeof(EventStreamConverter), inputConv!.ConverterType);

        var fb = t.GetCustomAttribute<ConverterFallbackBehaviorAttribute>();
        Assert.NotNull(fb);
        Assert.Equal(ConverterFallbackBehavior.Default, fb!.Behavior);
    }

    [Fact]
    public void Ctor_should_set_ObjectId_and_properties_roundtrip()
    {
        var attr = new EventStreamInputAttribute("obj-1")
        {
            ObjectType = "Order",
            Connection = "conn",
            DocumentType = "doc",
            DefaultStreamType = "es",
            DefaultStreamConnection = "es-conn",
            CreateEmptyObjectWhenNonExistent = true
        };

        Assert.Equal("obj-1", attr.ObjectId);
        Assert.Equal("Order", attr.ObjectType);
        Assert.Equal("conn", attr.Connection);
        Assert.Equal("doc", attr.DocumentType);
        Assert.Equal("es", attr.DefaultStreamType);
        Assert.Equal("es-conn", attr.DefaultStreamConnection);
        Assert.True(attr.CreateEmptyObjectWhenNonExistent);
    }

    [Fact]
    public void Default_values_should_be_correct()
    {
        var attr = new EventStreamInputAttribute("x");
        Assert.Null(attr.ObjectType);
        Assert.Null(attr.Connection);
        Assert.Null(attr.DocumentType);
        Assert.Null(attr.DefaultStreamType);
        Assert.Null(attr.DefaultStreamConnection);
        Assert.False(attr.CreateEmptyObjectWhenNonExistent);
    }
}
