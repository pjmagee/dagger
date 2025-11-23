using static Dagger.Client;

// Example 1: Simple container execution
Console.WriteLine("Example 1: Running a simple command in a container");
var output = await Dag.Container()
    .From("alpine:latest")
    .WithExec(["echo", "Hello from Dagger!"])
    .StdoutAsync();
Console.WriteLine($"Output: {output}");

// Example 2: Building and running a multi-step pipeline
Console.WriteLine("\nExample 2: Multi-step pipeline");
var pythonVersion = await Dag.Container()
    .From("python:3.11-slim")
    .WithExec(["python", "--version"])
    .StdoutAsync();
Console.WriteLine($"Python version: {pythonVersion}");

// Example 3: Working with directories
Console.WriteLine("\nExample 3: Working with directories");
var dir = Dag.Directory().WithNewFile("hello.txt", "Hello from Dagger SDK!");

var fileContents = await Dag.Container()
    .From("alpine:latest")
    .WithMountedDirectory("/data", dir)
    .WithExec(["cat", "/data/hello.txt"])
    .StdoutAsync();
Console.WriteLine($"File contents: {fileContents}");

// Example 4: Chaining operations
Console.WriteLine("\nExample 4: Building a custom container");
var customContainer = await Dag.Container()
    .From("ubuntu:22.04")
    .WithExec(["apt-get", "update"])
    .WithExec(["apt-get", "install", "-y", "curl"])
    .WithExec(["curl", "--version"])
    .StdoutAsync();
Console.WriteLine($"Curl installed: {customContainer}");

// Example 5: Working with secrets (if DAGGER_SESSION_PORT is available)
Console.WriteLine("\nExample 5: Using environment variables");
var envContainer = Dag.Container()
    .From("alpine:latest")
    .WithEnvVariable("MY_VAR", "Hello World")
    .WithExec(["sh", "-c", "echo $MY_VAR"]);

var envOutput = await envContainer.StdoutAsync();
Console.WriteLine($"Environment variable output: {envOutput}");

Console.WriteLine("\n✅ All examples completed successfully!");
