# .github/copilot-instructions.md

Welcome to the Revit plugin project. Follow these simple guidelines to generate clean, maintainable C# code.

1. Use PascalCase for all class, method, and property names.  
2. Use camelCase for all private fields and local variables.  
3. Add XML doc comments (`///`) above every public class and method.  
4. Keep each method focused on a single responsibility.  
5. Name files to match the primary class they contain.  
6. Organize code into folders by feature (e.g., `Commands`, `UI`, `Models`).  
7. Use `using` directives only for namespaces you reference.  
8. Avoid magic strings—define all literal strings as `const` or resource entries.  
9. Encapsulate long event handlers by extracting helper methods.  
10. Always check for `null` before accessing object members.  
11. Use `TaskDialog.Show` inside a dedicated helper to display messages.  
12. Group related properties in region blocks with clear names.  
13. Declare all Revit API calls inside `try`/`catch` and log exceptions.  
14. Write unit tests for all non-UI logic in a separate test project.  
15. Reference .NET 8 SDK and target `net8.0` in all project files.  
16. Commit small, focused changes with clear commit messages.  
17. Keep XAML markup minimal; define styles and resources externally.  
18. Use dependency injection for services and providers.  
19. Name boolean parameters or properties with “Is” or “Has” prefixes.  
20. Run `dotnet format` before each commit to enforce code style.