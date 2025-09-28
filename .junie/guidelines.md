# Test-writing guidelines

These rules describe how to write tests in this repository. They are intentionally concise so they can be applied consistently across projects.

- Test framework: xUnit
- Mocking: NSubstitute (when needed)
- Target framework: net9.0 (match the project under test)
- Project name for Results library tests: ErikLieben.FA.ES

## File and type naming
- Place tests for a type in a separate file named `{TypeName}Tests.cs` (e.g., `ResultTests.cs`).
- Namespace should mirror the project, e.g., `namespace ErikLieben.FA.ES;`.
- Use one top-level public test class per SUT type: `public class {TypeName}Tests { ... }`.
- When testing a specific method on the SUT, create an inner class with the method name inside the outer test class.
    - Example: `public class ResultTests { public class Map { /* tests for Result<T>.Map */ } }`.

## Test method naming
- Use the exact pattern: `Should_not_be_empty`.
    - Start with `Should`.
    - All following words are lowercase and separated by underscores.
    - Examples: `Should_return_success`, `Should_throw_when_mapper_is_null`, `Should_accumulate_errors`.

## AAA pattern and SUT variable
- Follow Arrange/Act/Assert (AAA) in every test.
- If you store the system under test in a variable, name it `sut`.
    - Example: `var sut = new ObjectUnderTest();`

## Using xUnit
- Prefer `[Fact]` for single-case tests.
- Use `[Theory]` with `[InlineData]` when a test case varies only by inputs/expected values.

## Using NSubstitute
- Use NSubstitute for fakes/mocks/stubs when interactions must be verified.
- Keep substitutes focused on the behavior the test asserts.

## Coverage goal
- Aim for 100% code coverage for the units you touch/add.
- Include both success and failure paths.
- Cover null-guard clauses (e.g., `ArgumentNullException.ThrowIfNull`) by asserting thrown exceptions.

## Structure examples

File: `ResultTests.cs`

```csharp
using System;
using ErikLieben.FA.Results;
using Xunit;

namespace ErikLieben.FA.ES;

public class ResultTests
{
    public class Map
    {
        [Fact]
        public void Should_map_to_generic_success()
        {
            // Arrange
            var sut = Result.Success();

            // Act
            var mapped = sut.Map(123);

            // Assert
            Assert.True(mapped.IsSuccess);
            Assert.Equal(123, mapped.Value);
        }

        [Fact]
        public void Should_propagate_errors_when_failure()
        {
            // Arrange
            var sut = Result.Failure(ValidationError.Create("err"));

            // Act
            var mapped = sut.Map(1);

            // Assert
            Assert.True(mapped.IsFailure);
            Assert.Equal("err", mapped.Errors[0].Message);
        }
    }
}
```

File: `ResultOfTTests.cs`

```csharp
using System;
using ErikLieben.FA.Results;
using Xunit;

namespace ErikLieben.FA.ES;

public class ResultOfTTests
{
    public class Bind
    {
        [Fact]
        public void Should_bind_when_success()
        {
            // Arrange
            var sut = Result<int>.Success(2);

            // Act
            var bound = sut.Bind(x => Result<string>.Success((x * 3).ToString()));

            // Assert
            Assert.True(bound.IsSuccess);
            Assert.Equal("6", bound.Value);
        }

        [Fact]
        public void Should_throw_when_binder_null()
        {
            // Arrange
            var sut = Result<int>.Success(1);

            // Act
            Func<Result<string>> act = () => sut.Bind<string>(null!);

            // Assert
            Assert.Throws<ArgumentNullException>(() => act());
        }
    }
}
```

## Do and Don't
- Do keep tests small, deterministic, and focused on one behavior.
- Do assert both positive and negative paths where applicable.
- Do name variables clearly; prefer `sut` for the system under test.
- Don't combine unrelated assertions in a single test.
- Don't hide Arrange/Act/Assert steps—make them explicit and readable.

## Minimal checklist per test
- File name ends with `Tests.cs` and matches SUT type.
- Inner class present and named after the SUT method under test (when applicable).
- Method name starts with `Should_...` and uses lowercase words separated by underscores.
- AAA is clearly visible.
- `sut` is used for the system under test variable when stored.
- Failure and success paths are covered.
- Argument validation paths are covered.

# XML documentation guidelines

These rules describe how to add XML documentation comments to production code in this repository.

## Documentation scope
- Document all **public** classes, interfaces, structs, enums, and delegates
- Document all **public** methods, properties, fields, and events
- Document all **protected** members (visible to derived classes)
- Skip **internal** and **private** members (unless building shared internal libraries)

## XML comment structure
```csharp
/// <summary>
/// Brief description of what this member does
/// </summary>
/// <param name="parameterName">Description of the parameter</param>
/// <returns>Description of what is returned</returns>
/// <exception cref="ExceptionType">When this exception is thrown</exception>
```

## Writing style
- Use third-person present tense ("Gets the user name" not "Get the user name")
- Start with a verb when describing methods ("Calculates", "Retrieves", "Validates")
- Be concise but complete
- Avoid redundant phrases like "This method..." or "This property..."

## Content requirements
- Explain purpose and behavior, not implementation
- Document all parameters, even if purpose seems obvious
- Include units for numeric values (seconds, pixels, bytes, etc.)
- Mention null handling behavior
- Document all exceptions that can be thrown directly by the method

## Documentation examples

Class:
```csharp
/// <summary>
/// Represents the result of an operation that can either succeed or fail with validation errors
/// </summary>
/// <typeparam name="T">The type of the success value</typeparam>
public class Result<T>
{
    /// <summary>
    /// Gets a value indicating whether the operation succeeded
    /// </summary>
    /// <value>True if the operation was successful; otherwise, false</value>
    public bool IsSuccess { get; }

    /// <summary>
    /// Transforms the success value using the specified mapper function
    /// </summary>
    /// <typeparam name="TResult">The type of the mapped result</typeparam>
    /// <param name="mapper">A function to transform the success value</param>
    /// <returns>
    /// A new <see cref="Result{T}"/> with the mapped value if successful; 
    /// otherwise, the original failure result
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mapper"/> is null</exception>
    public Result<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        // Implementation...
    }
}
```

## Project configuration
Ensure your project file includes:
```xml
<PropertyGroup>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>
```

## Documentation checklist
- Summary clearly explains the purpose
- All parameters documented with meaningful descriptions
- Return value documented (for non-void methods)
- All possible exceptions documented
- Generic type parameters explained
- XML syntax is valid
- Parameter names match actual method signature
