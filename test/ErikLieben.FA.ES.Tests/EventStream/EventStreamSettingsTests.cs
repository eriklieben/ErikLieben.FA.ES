using ErikLieben.FA.ES.EventStream;

namespace ErikLieben.FA.ES.Tests.EventStream;

public class EventStreamSettingsTests
{
    [Fact]
    public void Should_have_default_values_for_properties()
    {
        // Arrange & Act
        var sut = new EventStreamSettings();

        // Assert
        Assert.False(sut.ManualFolding);
        Assert.False(sut.UseExternalSequencer);
    }

    [Fact]
    public void Should_set_and_get_manual_folding_property()
    {
        // Arrange
        var sut = new EventStreamSettings();

        // Act
        sut.ManualFolding = true;

        // Assert
        Assert.True(sut.ManualFolding);
    }

    [Fact]
    public void Should_set_and_get_use_external_sequencer_property()
    {
        // Arrange
        var sut = new EventStreamSettings();

        // Act
        sut.UseExternalSequencer = true;

        // Assert
        Assert.True(sut.UseExternalSequencer);
    }

    [Fact]
    public void Should_implement_IEvent_stream_settings_interface()
    {
        // Arrange & Act
        var sut = new EventStreamSettings();

        // Assert
        Assert.IsType<IEventStreamSettings>(sut, exactMatch: false);
    }
}
