---
mode: 'agent'
description: 'Review and refactor code in your Revit add-in project according to defined instructions with focus on AI maintainability'
---

## Role

You're a senior expert software engineer with extensive experience in maintaining Revit add-in projects over a long time, ensuring clean code, best practices, and AI-friendly code structure.

## Task

1. **Comprehensive Review**: Take a deep breath, and review all coding guidelines instructions in `.github/instructions/*.md` and `.github/copilot-instructions.md`, then review all the code carefully and make code refactorings if needed.

2. **AI-First Refactoring**: The final code should be clean and maintainable while following the specified coding standards and instructions with special attention to:
   - Linear, step-by-step code flows
   - Comprehensive WHY documentation
   - Explicit error conditions
   - Immutable data structures where possible
   - Single responsibility principle

3. **File Integrity**: Do not split up the code, keep the existing files intact and maintain current project structure.

4. **Testing**: If the project includes tests, ensure they are still passing after your changes and follow the updated testing guidelines.

5. **Revit API Compliance**: Ensure all refactored code respects Revit API threading requirements and uses proper patterns like ExternalEvent for async operations.

## Refactoring Priorities

### High Priority
- Fix any violations of Revit API threading requirements
- Ensure proper exception handling and logging
- Add missing XML documentation following the enhanced guidelines
- Improve async patterns to follow Revit-specific best practices
- Fix any nullable reference type issues

### Medium Priority  
- Improve code organization and single responsibility adherence
- Enhance error messages and user feedback
- Optimize performance for Revit context
- Improve test coverage and mock usage

### Low Priority
- Code style consistency improvements
- Minor performance optimizations
- Documentation enhancements for existing documented code

## Validation Checklist

After refactoring, ensure:
- [ ] All code compiles without errors or warnings
- [ ] All tests pass (if applicable)
- [ ] Revit API calls are properly handled with ExternalEvent where needed
- [ ] Exception handling follows project standards
- [ ] XML documentation is complete and follows enhanced guidelines
- [ ] Async patterns follow Revit-specific best practices
- [ ] Code follows AI-first development principles
- [ ] No breaking changes to public interfaces

## Success Criteria

The refactored code should be:
1. **Maintainable by AI agents** - Clear, linear, well-documented
2. **Professional quality** - Following industry best practices
3. **Revit API compliant** - Respecting all threading and context requirements
4. **Fully tested** - With appropriate unit and integration test coverage
5. **Well documented** - With comprehensive XML docs explaining WHY not just WHAT
