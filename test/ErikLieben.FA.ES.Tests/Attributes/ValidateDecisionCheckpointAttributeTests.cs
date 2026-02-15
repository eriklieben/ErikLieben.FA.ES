using ErikLieben.FA.ES.Attributes;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Attributes;

public class ValidateDecisionCheckpointAttributeTests
{
    [Fact]
    public void Should_use_default_parameter_name_when_not_specified()
    {
        // Act
        var sut = new ValidateDecisionCheckpointAttribute();

        // Assert
        Assert.Equal(ValidateDecisionCheckpointAttribute.DefaultParameterName, sut.ParameterName);
        Assert.Equal("decisionContext", sut.ParameterName);
    }

    [Fact]
    public void Should_use_custom_parameter_name_when_specified()
    {
        // Arrange
        var customName = "myContext";

        // Act
        var sut = new ValidateDecisionCheckpointAttribute(customName);

        // Assert
        Assert.Equal(customName, sut.ParameterName);
    }

    [Fact]
    public void Should_throw_when_parameter_name_is_null()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new ValidateDecisionCheckpointAttribute(null!));
    }

    [Fact]
    public void Should_throw_when_parameter_name_is_empty()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new ValidateDecisionCheckpointAttribute(string.Empty));
    }

    [Fact]
    public void Should_throw_when_parameter_name_is_whitespace()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new ValidateDecisionCheckpointAttribute("   "));
    }

    [Fact]
    public void Should_use_default_max_decision_age()
    {
        // Act
        var sut = new ValidateDecisionCheckpointAttribute();

        // Assert
        Assert.Equal(ValidateDecisionCheckpointAttribute.DefaultMaxDecisionAgeSeconds, sut.MaxDecisionAgeSeconds);
        Assert.Equal(300, sut.MaxDecisionAgeSeconds);
    }

    [Fact]
    public void Should_allow_setting_custom_max_decision_age()
    {
        // Arrange
        var sut = new ValidateDecisionCheckpointAttribute();

        // Act
        sut.MaxDecisionAgeSeconds = 600;

        // Assert
        Assert.Equal(600, sut.MaxDecisionAgeSeconds);
    }

    [Fact]
    public void Should_restrict_usage_to_methods()
    {
        // Arrange & Act
        var attributes = typeof(ValidateDecisionCheckpointAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false);

        // Assert
        Assert.NotEmpty(attributes);
        var usage = (AttributeUsageAttribute)attributes[0];
        Assert.Equal(AttributeTargets.Method, usage.ValidOn);
    }

    [Fact]
    public void Should_inherit_from_attribute()
    {
        // Arrange & Act
        var sut = new ValidateDecisionCheckpointAttribute();

        // Assert
        Assert.IsType<Attribute>(sut, exactMatch: false);
    }

    [Fact]
    public void DefaultParameterName_Should_be_decisionContext()
    {
        // Assert
        Assert.Equal("decisionContext", ValidateDecisionCheckpointAttribute.DefaultParameterName);
    }

    [Fact]
    public void DefaultMaxDecisionAgeSeconds_Should_be_300()
    {
        // Assert
        Assert.Equal(300, ValidateDecisionCheckpointAttribute.DefaultMaxDecisionAgeSeconds);
    }
}
