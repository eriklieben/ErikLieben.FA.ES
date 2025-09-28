using System;
using System.Reflection;
using System.Runtime.Serialization;
using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Exceptions;

public class ConstraintExceptionSerializationTests
{
    public class Serialization
    {
        [Fact]
        public void Should_roundtrip_constraint_via_serialization_constructor()
        {
            // Arrange
            var original = new ConstraintException("constraint failed", Constraint.Existing);
            var info = new SerializationInfo(typeof(ConstraintException), new FormatterConverter());
            var context = new StreamingContext(StreamingContextStates.All);

            // Act
            original.GetObjectData(info, context);
            var sut = (ConstraintException)Activator.CreateInstance(
                typeof(ConstraintException),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { info, context },
                culture: null)!;

            // Assert
            Assert.Equal(Constraint.Existing, sut.Constraint);
            Assert.Equal("[ELFAES-BIZ-0001] constraint failed", sut.Message);
        }

        [Fact]
        public void Should_include_constraint_in_SerializationInfo()
        {
            // Arrange
            var sut = new ConstraintException("x", Constraint.New);
            var info = new SerializationInfo(typeof(ConstraintException), new FormatterConverter());

            // Act
            sut.GetObjectData(info, new StreamingContext(StreamingContextStates.All));

            // Assert
            var restored = (Constraint)info.GetValue(nameof(ConstraintException.Constraint), typeof(Constraint))!;
            Assert.Equal(Constraint.New, restored);
            Assert.Equal("ELFAES-BIZ-0001", info.GetString(nameof(EsException.ErrorCode)));
        }
    }
}
