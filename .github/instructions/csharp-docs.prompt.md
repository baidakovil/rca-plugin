---
mode: 'agent'
tools: ['changes', 'codebase', 'editFiles', 'problems']
description: 'Ensure that C# types are documented with XML comments and follow best practices for documentation in AI-maintained Revit add-in projects.'
---

# C# Documentation Best Practices for AI-Maintained Projects

## Core Documentation Requirements

- **All public members** must be documented with XML comments
- **Internal members** should be documented if they are complex or not self-explanatory
- **Private methods** with complex logic should have explanatory comments
- Focus on **WHY** not just **WHAT** - explain decisions, context, and reasoning

## Standard XML Tags

- Use `<summary>` for brief descriptions of what the member does
- Use `<param>` for method parameters with clear descriptions
- Use `<paramref>` to reference parameters within documentation
- Use `<returns>` for method return values, including Task details for async methods
- Use `<remarks>` for additional information, implementation details, usage notes, or context
- Use `<example>` for usage examples demonstrating how to use the member
- Use `<exception>` to document exceptions thrown by methods
- Use `<see langword>` for language-specific keywords like `null`, `true`, `false`, `int`, `bool`, etc.
- Use `<see cref>` to reference other types or members inline (within sentences)
- Use `<seealso>` for standalone references to related types or members
- Use `<inheritdoc/>` to inherit documentation from base classes or interfaces
- Use `<typeparam>` for type parameters in generic types or methods
- Use `<typeparamref>` to reference type parameters within documentation
- Use `<c>` for inline code snippets
- Use `<code>` for code blocks within `<example>` tags with `language="csharp"` attribute

## AI-First Documentation Principles

- **Explain WHY decisions were made** - AI needs context to maintain code effectively
- **Document error conditions explicitly** - What can go wrong and why
- **Explain thread safety considerations** - Especially important for Revit API context
- **Document performance implications** - Memory usage, API calls, blocking operations
- **Include pre-conditions and post-conditions** when relevant

## Revit API Specific Documentation

- Document **thread safety requirements** for Revit API calls
- Explain **ExternalEvent usage** and why it's needed
- Document **Revit context dependencies** (UIApplication, Document, etc.)
- Note **API version compatibility** when relevant
- Explain **exception handling strategies** for Revit API exceptions

## Examples

### Service Class Documentation
```csharp
/// <summary>
/// Service for executing Python code using IronPython3 with access to Revit API objects.
/// This service uses ExternalEvent to ensure thread-safe execution of Revit API calls.
/// </summary>
/// <remarks>
/// The service is designed to be thread-safe and can be called from modeless UI contexts.
/// All Python execution is marshaled to the Revit UI thread via ExternalEvent to comply
/// with Revit API threading requirements. The service captures both print() output and
/// return values from executed Python code.
/// </remarks>
public class PythonExecutionService : IPythonExecutionService
```

### Async Method Documentation
```csharp
/// <summary>
/// Executes Python code asynchronously via Revit ExternalEvent to ensure thread safety.
/// </summary>
/// <param name="code">The Python code to execute. Cannot be null or whitespace.</param>
/// <returns>
/// A task that represents the asynchronous execution. The task result contains
/// the formatted output including both print() statements and return values,
/// or error messages if execution fails.
/// </returns>
/// <remarks>
/// This method uses ExternalEvent to marshal execution to the Revit UI thread,
/// ensuring safe access to Revit API objects. The method will initialize the
/// ExternalEvent lazily if not already created, which allows the service to be
/// instantiated in test contexts without requiring Revit API availability.
/// </remarks>
/// <exception cref="InvalidOperationException">
/// Thrown when ExternalEvent cannot be created (outside Revit context) or when
/// Revit context has not been set via SetRevitContext().
/// </exception>
public async Task<string> ExecuteAsync(string code)
```

### Complex Logic Documentation
```csharp
// WHY: We capture stdout to a MemoryStream because IronPython's print() function
// outputs to the runtime's IO stream, not to the return value. This allows us
// to provide comprehensive output that includes both explicit print statements
// and the final expression result, giving users complete feedback.
using var outputStream = new MemoryStream();
engine.Runtime.IO.SetOutput(outputStream, StdoutEncoding);
```

## Documentation Completeness Checklist

- [ ] All public classes have `<summary>` and `<remarks>` if complex
- [ ] All public methods have `<summary>`, `<param>`, and `<returns>` (if applicable)
- [ ] All exceptions are documented with `<exception>`
- [ ] Thread safety considerations are documented in `<remarks>`
- [ ] Revit API dependencies are clearly stated
- [ ] Performance implications are noted when relevant
- [ ] Complex algorithms have explanatory comments about WHY they work that way
- [ ] Error handling strategies are explained
- [ ] Dependencies and pre-conditions are documented

## Maintenance Considerations

- Update documentation when behavior changes
- Keep examples current with actual usage patterns
- Ensure referenced types in `<see cref>` remain valid
- Review documentation during code reviews for accuracy and completeness
- Use consistent terminology throughout the project documentation
