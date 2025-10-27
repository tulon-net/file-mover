# Contributing to File Mover

Thank you for your interest in contributing to File Mover! This document provides guidelines and instructions for contributing to the project.

## Table of Contents

1. [Code of Conduct](#code-of-conduct)
2. [Getting Started](#getting-started)
3. [Development Setup](#development-setup)
4. [Making Changes](#making-changes)
5. [Testing](#testing)
6. [Submitting Changes](#submitting-changes)
7. [Code Style](#code-style)
8. [Project Structure](#project-structure)

## Code of Conduct

This project adheres to a code of conduct. By participating, you are expected to uphold this code. Please be respectful and constructive in your interactions.

## Getting Started

### Prerequisites

- .NET 9 SDK
- Docker and Docker Compose
- Git
- Your favorite IDE (Visual Studio, VS Code, Rider)

### Fork and Clone

1. Fork the repository on GitHub
2. Clone your fork locally:
   ```bash
   git clone https://github.com/YOUR-USERNAME/file-mover.git
   cd file-mover
   ```
3. Add upstream remote:
   ```bash
   git remote add upstream https://github.com/tulon-net/file-mover.git
   ```

## Development Setup

### 1. Restore Dependencies

```bash
dotnet restore
```

### 2. Start Infrastructure Services

```bash
docker-compose up -d mariadb redis rabbitmq
```

### 3. Build the Solution

```bash
dotnet build
```

### 4. Run Tests

```bash
dotnet test
```

## Making Changes

### Create a Feature Branch

```bash
git checkout -b feature/your-feature-name
```

Branch naming conventions:
- `feature/` - New features
- `bugfix/` - Bug fixes
- `hotfix/` - Urgent fixes
- `docs/` - Documentation updates
- `refactor/` - Code refactoring

### Make Your Changes

1. Write clean, readable code
2. Follow the existing code style
3. Add tests for new functionality
4. Update documentation as needed
5. Keep commits focused and atomic

### Commit Messages

Follow the Conventional Commits specification:

```
type(scope): subject

body

footer
```

Types:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `style`: Code style changes (formatting, etc.)
- `refactor`: Code refactoring
- `test`: Adding or updating tests
- `chore`: Maintenance tasks

Examples:
```
feat(api): add schedule validation endpoint

Implement endpoint to validate cron expressions and schedule configuration
before saving to database.

Closes #123
```

```
fix(worker): handle FTP connection timeout

Added retry logic with exponential backoff for FTP connection failures.

Fixes #456
```

## Testing

### Run All Tests

```bash
dotnet test
```

### Run Specific Test Project

```bash
dotnet test tests/FileMover.Api.Tests
```

### Run with Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

### Writing Tests

- Follow AAA pattern (Arrange, Act, Assert)
- Use descriptive test names
- Test edge cases and error conditions
- Mock external dependencies

Example test:

```csharp
[Fact]
public async Task GenerateFile_WithValidSchedule_ShouldSucceed()
{
    // Arrange
    var schedule = new Schedule { /* ... */ };
    var handler = new GenerateFileHandler(/* ... */);
    
    // Act
    var result = await handler.HandleAsync(schedule);
    
    // Assert
    Assert.True(result.IsSuccess);
}
```

## Submitting Changes

### Before Submitting

1. **Update from upstream**
   ```bash
   git fetch upstream
   git rebase upstream/main
   ```

2. **Run tests**
   ```bash
   dotnet test
   ```

3. **Build solution**
   ```bash
   dotnet build
   ```

4. **Check formatting**
   ```bash
   dotnet format
   ```

### Create Pull Request

1. Push your changes to your fork:
   ```bash
   git push origin feature/your-feature-name
   ```

2. Go to GitHub and create a Pull Request

3. Fill out the PR template:
   - Clear title and description
   - Reference related issues
   - Describe your changes
   - List any breaking changes
   - Add screenshots for UI changes

4. Wait for review and address feedback

### Pull Request Checklist

- [ ] Tests pass locally
- [ ] Code follows project style
- [ ] Documentation updated
- [ ] Commit messages follow convention
- [ ] No merge conflicts
- [ ] Changes are focused and atomic
- [ ] Breaking changes documented

## Code Style

### C# Conventions

- Use C# 12 features appropriately
- Follow Microsoft's C# Coding Conventions
- Use PascalCase for public members
- Use camelCase for private fields (with `_` prefix)
- Use meaningful variable names
- Keep methods small and focused

### Formatting

Run the formatter before committing:

```bash
dotnet format
```

### Documentation

- Add XML comments for public APIs
- Update README for significant changes
- Document complex algorithms
- Include examples where helpful

Example:

```csharp
/// <summary>
/// Transfers a file to the specified FTP server.
/// </summary>
/// <param name="filePath">The local file path to transfer.</param>
/// <param name="ftpHost">The FTP server hostname.</param>
/// <returns>A task representing the async operation.</returns>
/// <exception cref="FtpException">Thrown when FTP transfer fails.</exception>
public async Task TransferFileAsync(string filePath, string ftpHost)
{
    // Implementation
}
```

## Project Structure

```
file-mover/
├── src/
│   ├── FileMover.Api/              # REST API
│   ├── FileMover.Worker.GenerateFile/  # File generation worker
│   ├── FileMover.Worker.SendFile/      # FTP transfer worker
│   ├── FileMover.Domain/           # Domain entities
│   ├── FileMover.Infrastructure/   # Data access
│   └── FileMover.Contracts/        # Message contracts
├── tests/
│   ├── FileMover.Api.Tests/
│   ├── FileMover.Worker.Tests/
│   └── FileMover.Domain.Tests/
├── infrastructure/
│   ├── docker/                     # Docker Swarm
│   ├── k8s/                        # Kubernetes
│   └── observability/              # Monitoring
└── docs/                           # Documentation
```

### Adding New Projects

If you need to add a new project:

1. Create the project:
   ```bash
   dotnet new [template] -n ProjectName -o src/ProjectName
   ```

2. Add to solution:
   ```bash
   dotnet sln add src/ProjectName/ProjectName.csproj
   ```

3. Add corresponding test project

## Areas for Contribution

### Priority Areas

1. **Features**
   - Additional secret vault providers
   - SFTP support
   - File compression before transfer
   - Scheduling UI

2. **Improvements**
   - Performance optimizations
   - Enhanced error handling
   - Better logging
   - Metrics and monitoring

3. **Documentation**
   - API documentation
   - Deployment guides
   - Troubleshooting guides
   - Example configurations

4. **Testing**
   - Integration tests
   - Performance tests
   - Load tests
   - E2E tests

### Good First Issues

Look for issues labeled `good-first-issue` - these are designed for newcomers.

## Questions?

- Open an issue for bugs or feature requests
- Start a discussion for questions
- Check existing documentation

## License

By contributing, you agree that your contributions will be licensed under the project's MIT License.

Thank you for contributing! 🎉
