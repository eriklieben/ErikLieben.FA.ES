using ErikLieben.FA.ES.Actions;
using Xunit;

namespace ErikLieben.FA.ES.CodeAnalysis.Tests;

public class TypeConstantsTests
{
    public class FrameworkNamespace
    {
        [Fact]
        public void Should_have_correct_value()
        {
            Assert.Equal("ErikLieben.FA.ES", TypeConstants.FrameworkNamespace);
        }
    }

    public class FrameworkAttributesNamespace
    {
        [Fact]
        public void Should_have_correct_value()
        {
            Assert.Equal("ErikLieben.FA.ES.Attributes", TypeConstants.FrameworkAttributesNamespace);
        }
    }

    public class AggregateFullName
    {
        [Fact]
        public void Should_have_correct_value()
        {
            Assert.Equal("ErikLieben.FA.ES.Processors.Aggregate", TypeConstants.AggregateFullName);
        }

        [Fact]
        public void Should_match_actual_aggregate_type()
        {
            var aggregateType = typeof(Processors.Aggregate);
            Assert.Equal(TypeConstants.AggregateFullName, aggregateType.FullName);
        }
    }

    public class ProjectionFullName
    {
        [Fact]
        public void Should_have_correct_value()
        {
            Assert.Equal("ErikLieben.FA.ES.Projections.Projection", TypeConstants.ProjectionFullName);
        }

        [Fact]
        public void Should_match_actual_projection_type()
        {
            var projectionType = typeof(Projections.Projection);
            Assert.Equal(TypeConstants.ProjectionFullName, projectionType.FullName);
        }
    }

    public class RoutedProjectionFullName
    {
        [Fact]
        public void Should_have_correct_value()
        {
            Assert.Equal("ErikLieben.FA.ES.Projections.RoutedProjection", TypeConstants.RoutedProjectionFullName);
        }

        [Fact]
        public void Should_match_actual_routed_projection_type()
        {
            var routedProjectionType = typeof(Projections.RoutedProjection);
            Assert.Equal(TypeConstants.RoutedProjectionFullName, routedProjectionType.FullName);
        }
    }

    public class IEventStreamFullName
    {
        [Fact]
        public void Should_have_correct_value()
        {
            Assert.Equal("ErikLieben.FA.ES.IEventStream", TypeConstants.IEventStreamFullName);
        }

        [Fact]
        public void Should_match_actual_event_stream_interface()
        {
            var eventStreamType = typeof(IEventStream);
            Assert.Equal(TypeConstants.IEventStreamFullName, eventStreamType.FullName);
        }
    }

    public class StreamActionAttributeNamespace
    {
        [Fact]
        public void Should_have_correct_value()
        {
            Assert.Equal("ErikLieben.FA.ES.Attributes", TypeConstants.StreamActionAttributeNamespace);
        }
    }

    public class WhenAttributeNamespace
    {
        [Fact]
        public void Should_have_correct_value()
        {
            Assert.Equal("ErikLieben.FA.ES.Attributes", TypeConstants.WhenAttributeNamespace);
        }
    }

    public class StreamActionInterfaceNames
    {
        [Fact]
        public void Should_contain_all_stream_action_interfaces()
        {
            var interfaces = TypeConstants.StreamActionInterfaceNames;

            Assert.Contains("IAsyncPostCommitAction", interfaces);
            Assert.Contains("IPostAppendAction", interfaces);
            Assert.Contains("IPostReadAction", interfaces);
            Assert.Contains("IPreAppendAction", interfaces);
            Assert.Contains("IPreReadAction", interfaces);
        }

        [Fact]
        public void Should_have_exactly_five_interfaces()
        {
            Assert.Equal(5, TypeConstants.StreamActionInterfaceNames.Length);
        }

        [Fact]
        public void Should_match_actual_interface_names()
        {
            Assert.Equal("IAsyncPostCommitAction", typeof(IAsyncPostCommitAction).Name);
            Assert.Equal("IPostAppendAction", typeof(IPostAppendAction).Name);
            Assert.Equal("IPostReadAction", typeof(IPostReadAction).Name);
            Assert.Equal("IPreAppendAction", typeof(IPreAppendAction).Name);
            Assert.Equal("IPreReadAction", typeof(IPreReadAction).Name);
        }
    }
}
