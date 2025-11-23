# Testing NuGet Publishing Locally

This guide shows how to test the C# SDK NuGet publishing **without sharing your API key**.

## Prerequisites

- Dagger CLI installed
- Optional: NuGet.org account + API key ([create one here](https://www.nuget.org/account/apikeys)) for actual publishing tests

## Option 1: Dry Run (No API Key Needed) ✅ Recommended

Test that the package builds correctly without actually publishing:

```powershell
# Using the main Dagger module (recommended - provides introspection automatically)
dagger call sdk csharp release-dry-run

# Or manually specify a tag
dagger call sdk csharp publish --tag="sdk/csharp/v0.1.0-test" --dry-run=true
```

This will:
- Build the NuGet package
- Verify the package was created  
- **NOT** publish to NuGet.org
- **NOT** require any API key

## Option 2: Test Publish Using Environment Variable

You can safely test publishing using your own API key via environment variables:

```powershell
# Set your NuGet API key as an environment variable
$env:NUGET_API_KEY = "your-api-key-here"

# Test publish with your key
dagger call sdk csharp publish `
  --tag="sdk/csharp/v0.1.0-test" `
  --nuget-token=env:NUGET_API_KEY
```

**Important:** 
- Use a test version like `v0.1.0-test` so you don't accidentally publish a real version
- Your API key never leaves your local machine
- The `env:NUGET_API_KEY` syntax tells Dagger to read from the environment variable

## Option 3: Inspect the Package Locally

Before publishing, you can inspect the generated package:

```powershell
# Build the package and export it
dagger call sdk csharp pack -o ./test-packages

# Inspect the package contents
Expand-Archive -Path ./test-packages/Dagger.SDK.*.nupkg -DestinationPath ./extracted
Get-ChildItem -Recurse ./extracted

# Test installing locally
dotnet new console -n TestNuGet
cd TestNuGet
dotnet add package Dagger.SDK --source ..\test-packages
```

## How GitHub Actions Will Work

Once merged, the GitHub Actions workflow will:

1. Detect a tag push like `sdk/csharp/v0.1.0`
2. Call the `releaser` module
3. Pass `NUGET_TOKEN` from GitHub Secrets (which only maintainers can set)
4. Publish to NuGet.org

**You won't need to share your API key with anyone.**

## For Maintainers: Setting Up GitHub Secrets

Repository maintainers need to:

1. Go to Repository Settings → Secrets and Variables → Actions
2. Add a new secret: `RELEASE_NUGET_TOKEN`
3. Value: NuGet API key with push permissions

Then update `.github/workflows/publish.yml` to include:

```yaml
--nuget-token=env:NUGET_TOKEN \
```

And add to the `env:` section:

```yaml
NUGET_TOKEN: ${{ secrets.RELEASE_NUGET_TOKEN }}
```

## Common Issues

### "Package already exists"

If you try to publish the same version twice:

```bash
# Increment the version in Dagger.SDK.csproj
# Then rebuild and retry
```

### "Invalid API key"

Ensure your API key:
- Has "Push new packages and package versions" scope
- Matches the glob pattern `Dagger.SDK*`
- Is not expired

### "Dry run succeeded but publish fails"

Check:
- The version doesn't already exist on NuGet.org
- Your API key has push permissions
- You're using the correct source URL (`https://api.nuget.org/v3/index.json`)

## Next Steps

1. **Test dry-run first**: `dagger call -m sdk/csharp/dev publish --tag=test --dry-run=true`
2. **Test local package**: Build and inspect the .nupkg file
3. **Test with your key**: Use a test version number
4. **Submit PR**: Once tested, submit for review
5. **Maintainers publish**: They'll add the secret and merge
