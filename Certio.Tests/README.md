# Certio.Tests

Unit test project for Certio Application Layer services.

## Structure

```
Certio.Tests/
├── Services/
│   ├── MatterServiceTests.cs      - Matter operations tests
│   ├── TaskServiceTests.cs        - Task operations tests
│   ├── SubTaskServiceTests.cs     - SubTask operations tests
│   └── PermissionServiceTests.cs  - Permission checking tests
└── README.md
```

## Running Tests

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter FullyQualifiedName~MatterServiceTests

# Run with coverage
dotnet test /p:CollectCoverage=true
```

## Test Coverage Goals

As per PHASE_2_IMPLEMENTATION_SUMMARY.md:

- **PermissionService**: 10-15 tests
- **MatterService**: 20-25 tests
- **TaskService**: 20-25 tests
- **SubTaskService**: 15-20 tests

**Total Target**: 65-85 tests

## Test Patterns

### Testing Service Methods

```csharp
[Fact]
public async Task CreateMatter_WithoutPermission_ReturnsFailure()
{
    // Arrange
    var mockPermissionService = new Mock<IPermissionService>();
    mockPermissionService
        .Setup(s => s.HasPermissionAsync(It.IsAny<int>(), It.IsAny<int>(), Permission.CreateMatters))
        .ReturnsAsync(false);
    
    var service = new MatterService(..., mockPermissionService.Object, ...);
    
    // Act
    var result = await service.CreateMatterAsync(1, 1, new CreateMatterDto());
    
    // Assert
    Assert.False(result.Success);
    Assert.Equal("UNAUTHORIZED_OPERATION", result.ErrorCode);
}
```

## Current Status

✅ Test project created  
✅ Dependencies added (xUnit, Moq)  
✅ Project references added  
⏳ Test implementation pending

**Estimated effort**: 8-10 hours for full coverage

