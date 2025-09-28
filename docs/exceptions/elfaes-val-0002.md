# ELFAES-VAL-0002 — InvalidBindingSourceException

Category: Validation (VAL)

Summary
- An invalid binding source is provided during Azure Functions Worker model binding.

When does this happen?
- The provided ModelBindingData.Source is not supported by the binding.

Common causes
- Binding configuration expects a different source.
- Incorrect attribute usage or trigger configuration.

Recommended actions
- Ensure the binding source matches the expected value(s).
- Update configuration or attributes to supply a supported source.

Code reference
- Exception class: ErikLieben.FA.ES.Azure.Functions.Worker.Extensions.Exceptions.InvalidBindingSourceException
- Error code: ELFAES-VAL-0002
- Source: src/ErikLieben.FA.ES.Azure.Functions.Worker.Extensions/Exceptions/InvalidBindingSourceException.cs
