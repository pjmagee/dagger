# Hello Module

A simple Dagger module demonstrating how to create CI/CD pipelines in C#.

## Usage

This module demonstrates the **Module** usage pattern - creating reusable Dagger functions that can be called from the CLI or composed with other modules.

### Functions

- **Build**: Compiles a .NET application
- **Test**: Runs unit tests
- **BuildImage**: Creates a production Docker image
- **Publish**: Publishes the image to a registry
- **Run**: Runs the application locally

### Example Usage

```bash
# Initialize the module (creates dagger.json and template files)
dagger init --sdk=csharp

# Build a .NET project
dagger call build --source=./my-dotnet-app

# Run tests
dagger call test --source=./my-dotnet-app

# Build a container image
dagger call build-image --source=./my-dotnet-app --tag=v1.0.0

# Publish to a registry
dagger call publish \
  --source=./my-dotnet-app \
  --registry=ttl.sh \
  --tag=myapp-$(uuidgen) \
  --registry-password=env:REGISTRY_PASSWORD
```

### Running Locally

```bash
# Call a function from this module
dagger call run --source=./my-dotnet-app
```

## Module vs Client Library

This example shows **Dagger Module** usage:

- Uses `[Object]` and `[Function]` attributes
- Code is loaded as a Dagger module
- Functions are exposed via `dagger call`
- SDK code is generated at runtime

For **Client Library** usage (using Dagger.SDK as a NuGet package), see the `standalone-client` example.
