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
	src := t.Dagger.Source.Directory("sdk/csharp")
	return CheckCompleted, dag.
		CsharpSDKDev(dagger.CsharpSDKDevOpts{Source: src}).
		Lint(ctx)
}

func (t CsharpSDK) Test(ctx context.Context) (MyCheckStatus, error) {
	src := t.Dagger.Source.Directory("sdk/csharp")
	return CheckCompleted, dag.
		CsharpSDKDev(dagger.CsharpSDKDevOpts{Source: src}).
		Test(ctx, t.Dagger.introspectionJSON())
}

// Generate the C# SDK
func (t CsharpSDK) Generate(ctx context.Context) (*dagger.Changeset, error) {
	src := t.Dagger.Source.Directory("sdk/csharp")

	relLayer := dag.
		CsharpSDKDev(dagger.CsharpSDKDevOpts{Source: src}).
		Generate(t.Dagger.introspectionJSON())
	absLayer := dag.Directory().WithDirectory("sdk/csharp", relLayer)
	return absLayer.Changes(dag.Directory()).Sync(ctx)
}

func (t CsharpSDK) Bump(ctx context.Context, version string) (*dagger.Changeset, error) { //nolint:unparam
	// TODO: Implement version bumping for C# SDK
	// For now, skip it like the dotnet SDK does
	return dag.Directory().Changes(dag.Directory()).Sync(ctx)
}
