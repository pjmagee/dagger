package main

import (
	"context"

	"github.com/dagger/dagger/.dagger/internal/dagger"
)

type CsharpSDK struct {
	// +private
	Dagger *DaggerDev
}

func (t CsharpSDK) Name() string {
	return "csharp"
}

func (t CsharpSDK) Lint(ctx context.Context) (MyCheckStatus, error) {
	// TODO: Implement once C# SDK dev module is fully bootstrapped
	// For now, skip to avoid circular dependency during initial setup
	return CheckSkipped, nil
}

func (t CsharpSDK) Test(ctx context.Context) (MyCheckStatus, error) {
	// TODO: Implement once C# SDK dev module is fully bootstrapped
	// For now, skip to avoid circular dependency during initial setup
	return CheckSkipped, nil
}

// Generate the C# SDK
func (t CsharpSDK) Generate(ctx context.Context) (*dagger.Changeset, error) {
	// TODO: Implement once C# SDK is more mature
	// For now, skip like the dotnet SDK does
	return dag.Directory().Changes(dag.Directory()).Sync(ctx)
}

func (t CsharpSDK) Bump(ctx context.Context, version string) (*dagger.Changeset, error) { //nolint:unparam
	// TODO: Implement version bumping for C# SDK
	// For now, skip it like the dotnet SDK does
	return dag.Directory().Changes(dag.Directory()).Sync(ctx)
}
