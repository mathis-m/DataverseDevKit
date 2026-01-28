using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Extensions.Logging;
using DataverseDevKit.PluginHost.Runtime;
using DataverseDevKit.PluginHost.Services;
using DataverseDevKit.Core.Abstractions;

namespace DataverseDevKit.PluginHost.Tests;

/// <summary>
/// Integration tests for plugin loading and command execution.
/// These tests verify that plugins can be loaded, initialized, and executed properly,
/// especially focusing on assembly loading and dependency resolution.
/// </summary>
public class PluginLoadingTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILoggerFactory _loggerFactory;

    public PluginLoadingTests(ITestOutputHelper output)
    {
        _output = output;
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
    }

    [Fact]
    public async Task SolutionLayerAnalyzerPlugin_LoadsSuccessfully()
    {
        // Arrange
        var pluginPath = FindPluginAssembly("Ddk.SolutionLayerAnalyzer");
        Assert.True(File.Exists(pluginPath), $"Plugin assembly not found at: {pluginPath}");

        var loader = new PluginLoader();
        loader.SetLogger(_loggerFactory.CreateLogger<PluginLoader>());

        // Act - Load the plugin
        await loader.LoadPluginAsync(pluginPath);

        // Assert
        Assert.NotNull(loader.Plugin);
        Assert.Equal("com.ddk.solutionlayeranalyzer", loader.Plugin.PluginId);
        
        _output.WriteLine($"✓ Plugin loaded successfully: {loader.Plugin.PluginId}");
        _output.WriteLine($"✓ Plugin name: {loader.Plugin.Name}");
        _output.WriteLine($"✓ Plugin version: {loader.Plugin.Version}");
    }

    [Fact]
    public async Task SolutionLayerAnalyzerPlugin_InitializesSuccessfully()
    {
        // Arrange
        var pluginPath = FindPluginAssembly("Ddk.SolutionLayerAnalyzer");
        var loader = new PluginLoader();
        loader.SetLogger(_loggerFactory.CreateLogger<PluginLoader>());
        
        await loader.LoadPluginAsync(pluginPath);

        var storagePath = Path.Combine(Path.GetTempPath(), "ddk-test-plugins", Guid.NewGuid().ToString());
        var mockFactory = new MockServiceClientFactory(_loggerFactory.CreateLogger<MockServiceClientFactory>());
        var contextLogger = _loggerFactory.CreateLogger("TestPluginContext");

        // Act - Initialize the plugin
        await loader.InitializePluginAsync(
            pluginId: "com.ddk.solutionlayeranalyzer",
            storagePath: storagePath,
            config: new Dictionary<string, string>(),
            contextLogger: contextLogger,
            serviceClientFactory: mockFactory
        );

        // Assert
        Assert.NotNull(loader.Context);
        Assert.NotNull(loader.Context.ServiceClientFactory);
        
        _output.WriteLine($"✓ Plugin initialized successfully");
        _output.WriteLine($"✓ Storage path: {storagePath}");

        // Cleanup
        await loader.DisposeAsync();
        if (Directory.Exists(storagePath))
        {
            Directory.Delete(storagePath, true);
        }
    }

    [Fact]
    public async Task SolutionLayerAnalyzerPlugin_ExecutesClearCommand()
    {
        // Arrange
        var pluginPath = FindPluginAssembly("Ddk.SolutionLayerAnalyzer");
        var loader = new PluginLoader();
        loader.SetLogger(_loggerFactory.CreateLogger<PluginLoader>());
        
        await loader.LoadPluginAsync(pluginPath);

        var storagePath = Path.Combine(Path.GetTempPath(), "ddk-test-plugins", Guid.NewGuid().ToString());
        var mockFactory = new MockServiceClientFactory(_loggerFactory.CreateLogger<MockServiceClientFactory>());
        var contextLogger = _loggerFactory.CreateLogger("TestPluginContext");

        await loader.InitializePluginAsync(
            pluginId: "com.ddk.solutionlayeranalyzer",
            storagePath: storagePath,
            config: new Dictionary<string, string>(),
            contextLogger: contextLogger,
            serviceClientFactory: mockFactory
        );

        // Act - Execute clear command
        var result = await loader.Plugin.ExecuteAsync("clear", "{}", CancellationToken.None);

        // Assert
        _output.WriteLine($"✓ Clear command executed successfully");
        _output.WriteLine($"Result: {result}");

        // Cleanup
        await loader.DisposeAsync();
        if (Directory.Exists(storagePath))
        {
            Directory.Delete(storagePath, true);
        }
    }

    [Fact]
    public async Task SolutionLayerAnalyzerPlugin_ExecutesIndexCommand_WithMockData()
    {
        // Arrange
        var pluginPath = FindPluginAssembly("Ddk.SolutionLayerAnalyzer");
        var loader = new PluginLoader();
        loader.SetLogger(_loggerFactory.CreateLogger<PluginLoader>());
        
        await loader.LoadPluginAsync(pluginPath);

        var storagePath = Path.Combine(Path.GetTempPath(), "ddk-test-plugins", Guid.NewGuid().ToString());
        var mockFactory = new MockServiceClientFactory(_loggerFactory.CreateLogger<MockServiceClientFactory>());
        var contextLogger = _loggerFactory.CreateLogger("TestPluginContext");

        await loader.InitializePluginAsync(
            pluginId: "com.ddk.solutionlayeranalyzer",
            storagePath: storagePath,
            config: new Dictionary<string, string>(),
            contextLogger: contextLogger,
            serviceClientFactory: mockFactory
        );

        // Act - Execute index command with mock data
        var indexRequest = @"{
            ""connectionId"": ""default"",
            ""sourceSolutions"": [""CoreSolution""],
            ""targetSolutions"": [""ProjectA"", ""ProjectB""],
            ""includeComponentTypes"": [""SystemForm"", ""SavedQuery""],
            ""maxParallel"": 8,
            ""payloadMode"": ""lazy""
        }";

        _output.WriteLine("Executing index command with request:");
        _output.WriteLine(indexRequest);

        var result = await loader.Plugin.ExecuteAsync("index", indexRequest, CancellationToken.None);

        // Assert
        var resultString = result.ToString();
        Assert.Contains("stats", resultString, StringComparison.OrdinalIgnoreCase);
        
        _output.WriteLine($"✓ Index command executed successfully");
        _output.WriteLine($"Result: {resultString}");

        // Cleanup
        await loader.DisposeAsync();
        if (Directory.Exists(storagePath))
        {
            Directory.Delete(storagePath, true);
        }
    }

    [Fact]
    public async Task SolutionLayerAnalyzerPlugin_ServiceClientFactory_IsAccessible()
    {
        // This test specifically verifies that the ServiceClientFactory can be accessed
        // and the GetServiceClient method can be called without method resolution issues.
        
        // Arrange
        var pluginPath = FindPluginAssembly("Ddk.SolutionLayerAnalyzer");
        var loader = new PluginLoader();
        loader.SetLogger(_loggerFactory.CreateLogger<PluginLoader>());
        
        await loader.LoadPluginAsync(pluginPath);

        var storagePath = Path.Combine(Path.GetTempPath(), "ddk-test-plugins", Guid.NewGuid().ToString());
        var mockFactory = new MockServiceClientFactory(_loggerFactory.CreateLogger<MockServiceClientFactory>());
        var contextLogger = _loggerFactory.CreateLogger("TestPluginContext");

        await loader.InitializePluginAsync(
            pluginId: "com.ddk.solutionlayeranalyzer",
            storagePath: storagePath,
            config: new Dictionary<string, string>(),
            contextLogger: contextLogger,
            serviceClientFactory: mockFactory
        );

        // Act - Try to get service client from context
        var factory = loader.Context.ServiceClientFactory;
        Assert.NotNull(factory);
        
        // This should NOT throw "Method name not found" error
        var serviceClient = factory.GetServiceClient("default");
        
        // Assert
        Assert.NotNull(serviceClient);
        _output.WriteLine($"✓ ServiceClientFactory.GetServiceClient() called successfully");
        _output.WriteLine($"✓ ServiceClient type: {serviceClient.GetType().FullName}");

        // Cleanup
        await loader.DisposeAsync();
        if (Directory.Exists(storagePath))
        {
            Directory.Delete(storagePath, true);
        }
    }

    /// <summary>
    /// Finds the plugin assembly in the dist folder structure.
    /// Assumes the plugin was built and copied to dist/backend by the build process.
    /// </summary>
    private string FindPluginAssembly(string pluginAssemblyName)
    {
        // Start from the test assembly location and navigate to find the plugin
        var testDir = AppContext.BaseDirectory;
        _output.WriteLine($"Test directory: {testDir}");

        // Navigate up to find the repository root
        var repoRoot = testDir;
        while (!string.IsNullOrEmpty(repoRoot) && !Directory.Exists(Path.Combine(repoRoot, "src", "plugins")))
        {
            repoRoot = Directory.GetParent(repoRoot)?.FullName;
        }

        if (string.IsNullOrEmpty(repoRoot))
        {
            throw new FileNotFoundException("Could not find repository root");
        }

        _output.WriteLine($"Repository root: {repoRoot}");

        // Look for the plugin in the dist folder
        var distPath = Path.Combine(repoRoot, "src", "plugins", "solution-layer-analyzer", "dist", "backend", $"{pluginAssemblyName}.dll");
        
        if (!File.Exists(distPath))
        {
            // Try alternative location - output path during development
            distPath = Path.Combine(repoRoot, "src", "plugins", "solution-layer-analyzer", "src", "bin", "Debug", "net10.0", $"{pluginAssemblyName}.dll");
        }

        _output.WriteLine($"Looking for plugin at: {distPath}");
        return distPath;
    }
}
