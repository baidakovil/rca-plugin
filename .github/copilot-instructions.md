# .github/copilot-instructions.md

Welcome to the Revit plugin project. 
Revit Chat Assistant is an add-in for Autodesk Revit, designed to make working in Revit faster and more convenient with the help of an AI chat agent embedded in a dedicated panel. Similar to how GitHub Copilot works in VS Code or Visual Studio, Revit Chat Assistant (RCA) can modify the Revit model or answer user questions.

Follow these guidelines to generate clean, maintainable C# code for AI-first development.

## AI-First Development Principles

1. **Linear, step-by-step code flows** - Avoid complex nested logic that's hard for AI to understand and modify
2. **Comprehensive WHY documentation** - Every non-trivial decision should be explained in comments or XML docs
3. **Explicit error conditions** - Handle and document all expected failure modes
4. **Prefer immutable data structures** - Reduces state-related bugs and makes code more predictable
5. **Single responsibility principle** - Each method and class should have one clear purpose

## Code Standards and Conventions

1. Follow the project's [.editorconfig](../.editorconfig) file for formatting, naming, and code style conventions.
2. Add XML doc comments (`///`) above every public class and method following [csharp-docs.prompt.md](instructions/csharp-docs.prompt.md).
3. Keep each method focused on a single responsibility and follow SOLID principles.
4. Name files to match the primary class they contain.
5. Organize code into folders by feature (e.g., `Commands`, `UI`, `Models`, `Services`).
6. Use `using` directives only for namespaces you reference.
7. Avoid magic strings—define all literal strings as `const` or resource entries.
8. Encapsulate long event handlers by extracting helper methods.
9. Always check for `null` before accessing object members (or use nullable reference types appropriately).
10. Use `TaskDialog.Show` inside a dedicated helper to display messages.
11. Group related properties in region blocks with clear names.

## Revit API Specific Guidelines

12. Declare all Revit API calls inside `try`/`catch` and log exceptions appropriately.
13. Use `ExternalEvent` pattern for async operations that require Revit API access.
14. Respect Revit API threading requirements - most API calls must be on the UI thread.
15. Mock Revit API dependencies in unit tests using interfaces.
16. Reference RevitAPI.dll and RevitAPIUI.dll with `<Private>False</Private>` in all projects.
17. For Loader project, ensure ILRepack properly handles RevitAPI references during assembly merging.

## Project Configuration

18. Target `net8.0-windows` framework for Revit 2026 compatibility.
19. Enable nullable reference types across ALL projects: set `<Nullable>enable</Nullable>` in every `.csproj` and use correct annotations (`?`, `!`) to fix warnings gradually. Temporary suppressions are allowed only at narrow scope when interacting with external APIs.
20. All generated code must compile without errors or warnings.
21. Use ILRepack for merging Loader and Contracts assemblies into a single deployment DLL.

## Assembly Merging with ILRepack

22. Use ILRepack to merge Rca.Loader.dll and Rca.Loader.Contracts.dll into a single assembly.
23. Use custom MSBuild tasks to properly handle RevitAPI dependencies during merge process.
24. Ensure strict build process that fails if merging doesn't succeed - no fallback to separate assemblies.
25. Maintain clean API boundaries even within the merged assembly.
26. Update hot-reloading system to handle single merged assembly instead of separate Loader/Contracts DLLs.

## Testing and Quality

27. Write unit tests for all non-UI logic in separate test projects following [csharp-nunit.prompt.md](instructions/csharp-nunit.prompt.md).
28. Use dependency injection for services and providers to enable proper testing.
29. Follow [csharp-async.prompt.md](instructions/csharp-async.prompt.md) for async code patterns.
30. When task is approved to be done, update documentation in the [docs](docs) folder. Do not create excessive documentation, but ensure all non-trivial logic is explained.

## Development Workflow

31. Keep XAML markup minimal; define styles and resources externally.
32. Name boolean parameters or properties with "Is" or "Has" prefixes.
33. Validate all changes compile successfully before considering the task complete.
34. When modifying build processes, ensure compatibility with CI/CD pipeline.
35. Build the project after every significant change to catch issues early. Use 'dotnet build --no-incremental' to ensure a full rebuild.

## Code Organization Patterns

- **Services**: Business logic and external integrations
- **Commands**: Revit command implementations
- **UI**: User interface components and view models
- **Models**: Data structures and domain objects
- **Contracts**: Interfaces and shared abstractions (internalized in merged assembly)
- **Tests**: Unit and integration tests

## Error Handling Standards

- Use structured logging with consistent message formats
- Provide meaningful error messages to users via `TaskDialog`
- Log technical details for debugging purposes
- Handle Revit API exceptions gracefully
- Use appropriate exception types for different error conditions
- Handle ILRepack and build process errors with clear diagnostics

## Performance Considerations

- Use `ConfigureAwait(false)` in library code, but be cautious in Revit context
- Minimize Revit API calls in tight loops
- Consider memory usage when processing large datasets
- Use appropriate collection types for the use case
- Keep merged assemblies lean to optimize loading time

This project is designed to be maintained primarily by AI agents, so prioritize clarity, consistency, and comprehensive documentation over brevity.
