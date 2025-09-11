# Proposed Rca.Loader Structure Refactoring

## Current Issues
1. **Mixed responsibilities** - LoaderApp and RcaLoader overlap
2. **Poor organization** - too many files at root level  
3. **Test code in production** - TestEmbeddedResources should be removed
4. **SOLID violations** - missing abstractions and clear separation

## Recommended Structure

```
src\Rca.Loader\
??? Commands\                    # External commands (? keep as is)
?   ??? InitializerCommand.cs    
?   ??? ReloadRuntimeCommand.cs  
?   ??? OpenStandaloneWindowCommand.cs
??? Services\                    # Core business logic
?   ??? IRuntimeManager.cs       # Interface for runtime management
?   ??? RuntimeManager.cs        # Runtime loading/unloading
?   ??? IPipeServerService.cs    # Interface for pipe communication
?   ??? PipeServerService.cs     # Pipe server implementation
?   ??? IRibbonService.cs        # Interface for UI ribbon
?   ??? RibbonService.cs         # Ribbon building logic
??? Infrastructure\              # Low-level plumbing
?   ??? RuntimeLoadContext.cs    # Assembly loading context
?   ??? RuntimeCommandHandler.cs # Pipe command handling
?   ??? LoaderConstants.cs       # Constants and configuration
??? Testing\                     # Move to test project
?   ??? [Move to Rca.Loader.Tests]
??? LoaderApp.cs                 # Main entry point (simplified)
??? Rca.Loader.csproj           # Project file
```

## Key Improvements

### 1. **Clear Separation of Concerns**
- **LoaderApp**: Only Revit lifecycle management
- **Services**: Business logic with interfaces
- **Infrastructure**: Low-level technical concerns
- **Commands**: User-initiated actions

### 2. **SOLID Compliance**
- **Interfaces** for all major services
- **Dependency injection** ready
- **Single responsibility** per class
- **Testable** components

### 3. **Follow Guidelines**
- ? Organize code into folders by feature
- ? Use dependency injection for services
- ? Keep each method focused on single responsibility
- ? Move test code to separate test project

## Consolidation Recommendations

### **Merge**: LoaderApp.cs + RcaLoader.cs
- **Current**: Two similar classes with overlapping responsibilities
- **Proposed**: Single `LoaderApp` as main entry point
- **Benefit**: Eliminates confusion and duplication

### **Remove**: TestEmbeddedResources.cs  
- **Current**: Debug code in production assembly
- **Proposed**: Move to test project or remove entirely
- **Benefit**: Cleaner production code

### **Split**: Large classes into focused services
- **RuntimeManager** ? Keep as is (focused responsibility)
- **PipeServer** ? Rename to `PipeServerService` + interface
- **RibbonBuilder** ? Rename to `RibbonService` + interface

### **Move**: Testing folder
- **Current**: Test utilities in main project
- **Proposed**: Move to `Rca.Loader.Tests` project
- **Benefit**: Proper test/production separation
```