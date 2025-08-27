# RCA Plugin Test System

This directory contains comprehensive unit and integration tests for the RCA Plugin solution.

## Test Structure

```
tests/
├── Rca.Contracts.Tests/       # Unit tests for contracts and DI container
├── Rca.Core.Tests/            # Unit tests for core services (PythonExecutionService, DebugLogService)
├── Rca.UI.Tests/              # Unit tests for UI ViewModels
├── Rca.Network.Tests/         # Unit tests for network components
├── RcaPlugin.Tests/           # Unit tests for plugin commands
└── Rca.Integration.Revit.Tests/  # Integration tests with Revit API (headless)
```

## Test Categories

- **Unit** - Fast unit tests that don't require Revit API (default category)
- **Revit** - Integration tests that require Revit environment

## Running Tests

### Unit Tests (Default)
```bash
# Run all unit tests (excludes Revit integration tests)
dotnet test --filter "TestCategory!=Revit"

# Run specific test project
dotnet test tests/Rca.Contracts.Tests/
```

### Revit Integration Tests
```bash
# Enable Revit tests and run all tests
set RCA_ENABLE_REVIT_TESTS=1
dotnet test

# Or run only Revit tests
set RCA_ENABLE_REVIT_TESTS=1
dotnet test --filter "TestCategory=Revit"
```

## Test Frameworks

- **NUnit** - Test runner and assertions
- **NSubstitute** - Mocking framework for dependencies  
- **FluentAssertions** - Readable assertion syntax
- **ricaun.RevitTest** - Headless Revit testing for integration tests

## Environment Variables

- `RCA_ENABLE_REVIT_TESTS=1` - Enables Revit integration tests (disabled by default in CI)

## Best Practices

1. **Isolation** - Each test is independent with fresh mocks in Setup
2. **AAA Pattern** - Arrange, Act, Assert structure
3. **Descriptive Names** - MethodName_State_ExpectedResult format
4. **Single Responsibility** - One logical scenario per test
5. **Categories** - Proper test categorization for filtering
6. **Mocking** - Use NSubstitute for all external dependencies

## CI/CD Integration

- Unit tests run automatically in GitHub Actions
- Revit integration tests are skipped in CI (no RCA_ENABLE_REVIT_TESTS variable)
- Tests run on Windows hosted runners only (due to .NET 8.0-windows targeting)

## Adding New Tests

1. Create test class following `<ClassName>Tests.cs` naming
2. Use appropriate namespace: `<Project>.Tests`
3. Add `[Category("Unit")]` for unit tests or `[Category("Revit")]` for integration tests
4. Mock all external dependencies using NSubstitute
5. Use FluentAssertions for readable assertions