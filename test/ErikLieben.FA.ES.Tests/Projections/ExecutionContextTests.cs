using ErikLieben.FA.ES.Documents;
using NSubstitute;
using ErikLieben.FA.ES.Projections;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Projections
{
    public class ExecutionContextTests
    {
        public class Ctor
        {
            [Fact]
            public void Should_initialize_properly_with_valid_parameters()
            {
                // Arrange
                var @event = Substitute.For<IEvent<TestEvent>>();
                var document = Substitute.For<IObjectDocument>();
                var item = new TestData();

                // Act
                var sut = new ExecutionContext<TestEvent, TestData>(@event, document, item);

                // Assert
                Assert.Equal(@event, sut.Event);
                Assert.Equal(document, sut.Document);
                Assert.Equal(item, sut.Item);
                Assert.Null(sut.ParentContext);
                Assert.True(sut.IsRoot);
            }

            [Fact]
            public void Should_initialize_with_null_item()
            {
                // Arrange
                var @event = Substitute.For<IEvent<TestEvent>>();
                var document = Substitute.For<IObjectDocument>();

                // Act
                var sut = new ExecutionContext<TestEvent, TestData>(@event, document, null);

                // Assert
                Assert.Equal(@event, sut.Event);
                Assert.Equal(document, sut.Document);
                Assert.Null(sut.Item);
                Assert.Null(sut.ParentContext);
                Assert.True(sut.IsRoot);
            }

            [Fact]
            public void Should_initialize_with_parent_context()
            {
                // Arrange
                var @event = Substitute.For<IEvent<TestEvent>>();
                var document = Substitute.For<IObjectDocument>();
                var item = new TestData();
                var parentContext = Substitute.For<IExecutionContextWithData<TestData>>();

                // Act
                var sut = new ExecutionContext<TestEvent, TestData>(@event, document, item, parentContext);

                // Assert
                Assert.Equal(@event, sut.Event);
                Assert.Equal(document, sut.Document);
                Assert.Equal(item, sut.Item);
                Assert.Equal(parentContext, sut.ParentContext);
                Assert.False(sut.IsRoot);
            }
        }

        public class EventProperty
        {
            [Fact]
            public void Should_return_event_from_constructor()
            {
                // Arrange
                var @event = Substitute.For<IEvent<TestEvent>>();
                var document = Substitute.For<IObjectDocument>();

                // Act
                var sut = new ExecutionContext<TestEvent, TestData>(@event, document, null);

                // Assert
                Assert.Equal(@event, sut.Event);
            }

            [Fact]
            public void Should_return_event_when_accessed_through_interface()
            {
                // Arrange
                var @event = Substitute.For<IEvent<TestEvent>>();
                var document = Substitute.For<IObjectDocument>();
                var sut = new ExecutionContext<TestEvent, TestData>(@event, document, null);

                // Act
                var executionContextEvent = ((IExecutionContextWithData<TestData>)sut).Event;

                // Assert
                Assert.Equal(@event, executionContextEvent);
            }
        }

        public class DocumentProperty
        {
            [Fact]
            public void Should_return_document_from_constructor()
            {
                // Arrange
                var @event = Substitute.For<IEvent<TestEvent>>();
                var document = Substitute.For<IObjectDocument>();

                // Act
                var sut = new ExecutionContext<TestEvent, TestData>(@event, document, null);

                // Assert
                Assert.Equal(document, sut.Document);
            }
        }

        public class ItemProperty
        {
            [Fact]
            public void Should_return_item_from_constructor()
            {
                // Arrange
                var @event = Substitute.For<IEvent<TestEvent>>();
                var document = Substitute.For<IObjectDocument>();
                var item = new TestData();

                // Act
                var sut = new ExecutionContext<TestEvent, TestData>(@event, document, item);

                // Assert
                Assert.Equal(item, sut.Item);
            }

            [Fact]
            public void Should_return_null_when_item_is_null()
            {
                // Arrange
                var @event = Substitute.For<IEvent<TestEvent>>();
                var document = Substitute.For<IObjectDocument>();

                // Act
                var sut = new ExecutionContext<TestEvent, TestData>(@event, document, null);

                // Assert
                Assert.Null(sut.Item);
            }

            [Fact]
            public void Should_return_item_from_parent_context_when_item_is_null()
            {
                // Arrange
                var @event = Substitute.For<IEvent<TestEvent>>();
                var document = Substitute.For<IObjectDocument>();
                var parentItem = new TestData();
                var parentContext = Substitute.For<IExecutionContextWithData<TestData>>();
                parentContext.Item.Returns(parentItem);

                // Act
                var sut = new ExecutionContext<TestEvent, TestData>(@event, document, null, parentContext);

                // Assert
                Assert.Equal(parentItem, sut.Item);
            }

            [Fact]
            public void Should_return_local_item_even_when_parent_context_has_item()
            {
                // Arrange
                var @event = Substitute.For<IEvent<TestEvent>>();
                var document = Substitute.For<IObjectDocument>();
                var item = new TestData { Id = 1 };
                var parentItem = new TestData { Id = 2 };
                var parentContext = Substitute.For<IExecutionContextWithData<TestData>>();
                parentContext.Item.Returns(parentItem);

                // Act
                var sut = new ExecutionContext<TestEvent, TestData>(@event, document, item, parentContext);

                // Assert
                Assert.Equal(item, sut.Item);
                Assert.NotEqual(parentItem, sut.Item);
            }
        }

        public class ParentContextProperty
        {
            [Fact]
            public void Should_return_null_when_no_parent_context()
            {
                // Arrange
                var @event = Substitute.For<IEvent<TestEvent>>();
                var document = Substitute.For<IObjectDocument>();

                // Act
                var sut = new ExecutionContext<TestEvent, TestData>(@event, document, null);

                // Assert
                Assert.Null(sut.ParentContext);
            }

            [Fact]
            public void Should_return_parent_context_from_constructor()
            {
                // Arrange
                var @event = Substitute.For<IEvent<TestEvent>>();
                var document = Substitute.For<IObjectDocument>();
                var parentContext = Substitute.For<IExecutionContextWithData<TestData>>();

                // Act
                var sut = new ExecutionContext<TestEvent, TestData>(@event, document, null, parentContext);

                // Assert
                Assert.Equal(parentContext, sut.ParentContext);
            }
        }

        public class IsRootProperty
        {
            [Fact]
            public void Should_return_true_when_no_parent_context()
            {
                // Arrange
                var @event = Substitute.For<IEvent<TestEvent>>();
                var document = Substitute.For<IObjectDocument>();

                // Act
                var sut = new ExecutionContext<TestEvent, TestData>(@event, document, null);

                // Assert
                Assert.True(sut.IsRoot);
            }

            [Fact]
            public void Should_return_false_when_parent_context_exists()
            {
                // Arrange
                var @event = Substitute.For<IEvent<TestEvent>>();
                var document = Substitute.For<IObjectDocument>();
                var parentContext = Substitute.For<IExecutionContextWithData<TestData>>();

                // Act
                var sut = new ExecutionContext<TestEvent, TestData>(@event, document, null, parentContext);

                // Assert
                Assert.False(sut.IsRoot);
            }
        }

        public class ToStringMethod
        {
            [Fact]
            public void Should_return_formatted_string_when_no_parent_context()
            {
                // Arrange
                var @event = Substitute.For<IEvent<TestEvent>>();
                @event.EventType.Returns("TestEvent");
                @event.EventVersion.Returns(1);
                var document = Substitute.For<IObjectDocument>();
                document.ObjectName.Returns("TestObject");
                document.ObjectId.Returns("123");

                // Act
                var sut = new ExecutionContext<TestEvent, TestData>(@event, document, null);
                var result = sut.ToString();

                // Assert
                Assert.Contains("objectName 'TestObject'", result);
                Assert.Contains("objectId '123'", result);
                Assert.Contains("eventType 'TestEvent'", result);
                Assert.Contains("eventVersion '1'", result);
                Assert.Contains("Parent: None", result);
            }

            [Fact]
            public void Should_include_parent_context_in_string_representation()
            {
                // Arrange
                var @event = Substitute.For<IEvent<TestEvent>>();
                @event.EventType.Returns("TestEvent");
                @event.EventVersion.Returns(1);
                var document = Substitute.For<IObjectDocument>();
                document.ObjectName.Returns("TestObject");
                document.ObjectId.Returns("123");
                var parentContext = Substitute.For<IExecutionContextWithData<TestData>>();

                // Act
                var sut = new ExecutionContext<TestEvent, TestData>(@event, document, null, parentContext);
                var result = sut.ToString();

                // Assert
                Assert.Contains("objectName 'TestObject'", result);
                Assert.Contains("objectId '123'", result);
                Assert.Contains("eventType 'TestEvent'", result);
                Assert.Contains("eventVersion '1'", result);
                Assert.Contains($"Parent: {parentContext}", result);
            }
        }

        public class InterfaceImplementation
        {
            [Fact]
            public void Should_implement_IExecutionContext()
            {
                // Arrange
                var @event = Substitute.For<IEvent<TestEvent>>();
                var document = Substitute.For<IObjectDocument>();

                // Act
                var sut = new ExecutionContext<TestEvent, TestData>(@event, document, null);

                // Assert
                Assert.IsType<IExecutionContext>(sut, exactMatch: false);
            }

            [Fact]
            public void Should_implement_IExecutionContextWithData()
            {
                // Arrange
                var @event = Substitute.For<IEvent<TestEvent>>();
                var document = Substitute.For<IObjectDocument>();

                // Act
                var sut = new ExecutionContext<TestEvent, TestData>(@event, document, null);

                // Assert
                Assert.IsType<IExecutionContextWithData<TestData>>(sut, exactMatch: false);
            }

            [Fact]
            public void Should_implement_IExecutionContextWithEvent()
            {
                // Arrange
                var @event = Substitute.For<IEvent<TestEvent>>();
                var document = Substitute.For<IObjectDocument>();

                // Act
                var sut = new ExecutionContext<TestEvent, TestData>(@event, document, null);

                // Assert
                Assert.IsType<IExecutionContextWithEvent<TestEvent>>(sut, exactMatch: false);
            }

            [Fact]
            public void Should_implement_IExecutionContext_with_generic_parameters()
            {
                // Arrange
                var @event = Substitute.For<IEvent<TestEvent>>();
                var document = Substitute.For<IObjectDocument>();

                // Act
                var sut = new ExecutionContext<TestEvent, TestData>(@event, document, null);

                // Assert
                Assert.IsType<IExecutionContext<TestEvent, TestData>>(sut, exactMatch: false);
            }
        }

        public class TestEvent
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public class TestData
        {
            public int Id { get; set; }
            public string Description { get; set; } = string.Empty;
        }
    }
}
