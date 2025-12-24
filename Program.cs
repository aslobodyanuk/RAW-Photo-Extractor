using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RAW.Photo.Extractor;
using Serilog;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var parser = new Parser(with => with.HelpWriter = null);
    var result = parser.ParseArguments<CommandLineOptions>(args);

    return result.MapResult(
        options => RunApplication(options),
        errors => DisplayHelp(result, errors)
    );
}
finally
{
    Log.CloseAndFlush();
}

static int RunApplication(CommandLineOptions options)
{
    try
    {
        // Create logger factory
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddSerilog();
        });
        var logger = loggerFactory.CreateLogger<Program>();
        var serviceLogger = loggerFactory.CreateLogger<RawPhotoExtractorService>();

        // Load configuration from appsettings.json
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        var appConfig = new AppConfig();
        configuration.Bind(appConfig);

        // Validate configuration using FluentValidation
        var validator = new AppConfigValidator();
        var validationResult = validator.Validate(appConfig);
        if (!validationResult.IsValid)
        {
            logger.LogError("Configuration validation failed:");
            foreach (var error in validationResult.Errors)
            {
                logger.LogError("  - {PropertyName}: {ErrorMessage}", error.PropertyName, error.ErrorMessage);
            }
            return 1;
        }

        // Initialize service with logger
        var service = new RawPhotoExtractorService(appConfig, serviceLogger);

        // Validate directories
        try
        {
            service.ValidateDirectoryExists(options.NonRawPhotosDirectory, "NonRawPhotosDirectory");
            service.ValidateDirectoryExists(options.RootDirectory, "RootDirectory");
        }
        catch (DirectoryNotFoundException ex)
        {
            logger.LogError(ex, "Directory validation failed: {Message}", ex.Message);
            return 1;
        }

        // Create output directory if it doesn't exist
        var outputDirExisted = Directory.Exists(options.OutputDirectory);
        service.EnsureOutputDirectoryExists(options.OutputDirectory);
        if (!outputDirExisted)
        {
            logger.LogInformation("Created output directory: {OutputDirectory}", options.OutputDirectory);
        }

        logger.LogInformation("Starting RAW photo extraction...");
        logger.LogInformation("Non-RAW Photos Directory: {NonRawPhotosDirectory}", options.NonRawPhotosDirectory);
        logger.LogInformation("Root Directory (search): {RootDirectory}", options.RootDirectory);
        logger.LogInformation("Output Directory: {OutputDirectory}", options.OutputDirectory);
        logger.LogInformation("RAW Extensions: {RawExtensions}", string.Join(", ", appConfig.RawFileExtensions));

        // Get all non-RAW photo files recursively
        var nonRawFiles = service.EnumerateFilesRecursively(options.NonRawPhotosDirectory);
        logger.LogInformation("Found {Count} non-RAW photo files to process", nonRawFiles.Count);

        // Extract base names (without extensions)
        var baseNames = service.ExtractBaseNames(nonRawFiles);
        logger.LogInformation("Extracted {Count} unique base names", baseNames.Count);

        // Get all files from root directory
        var rawFilesFound = service.EnumerateFilesRecursively(options.RootDirectory);
        logger.LogInformation("Scanning {Count} files in root directory for RAW matches...", rawFilesFound.Count);

        // Find matching RAW files
        var rawFilesToCopy = service.FindMatchingRawFiles(baseNames, rawFilesFound);
        var matchedCount = rawFilesToCopy.Values.Sum(files => files.Count);
        logger.LogInformation("Found {MatchedCount} RAW files matching {BaseNameCount} base names", matchedCount, rawFilesToCopy.Count);

        // Copy all matching RAW files to output directory
        var copyResult = service.CopyRawFilesToOutput(rawFilesToCopy, options.OutputDirectory);

        // Display copy progress
        foreach (var (sourceFile, destinationFile) in copyResult.CopiedFiles)
        {
            var fileName = Path.GetFileName(sourceFile);
            var destFileName = Path.GetFileName(destinationFile);
            logger.LogInformation("Copied: {SourceFileName} -> {DestFileName}", fileName, destFileName);
        }

        // Display errors
        foreach (var (filePath, errorMessage) in copyResult.Errors)
        {
            logger.LogError("Error copying {FilePath}: {ErrorMessage}", filePath, errorMessage);
        }

        logger.LogInformation("=== Summary ===");
        logger.LogInformation("Base names processed: {BaseNameCount}", baseNames.Count);
        logger.LogInformation("RAW files found: {MatchedCount}", matchedCount);
        logger.LogInformation("Files copied: {CopiedCount}", copyResult.CopiedCount);
        logger.LogInformation("Files skipped: 0");
        logger.LogInformation("Errors: {ErrorCount}", copyResult.ErrorCount);

        return copyResult.ErrorCount > 0 ? 1 : 0;
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Fatal error: {Message}", ex.Message);
        return 1;
    }
}

static int DisplayHelp(ParserResult<CommandLineOptions> result, IEnumerable<Error> errors)
{
    var helpText = CommandLine.Text.HelpText.AutoBuild(result, h =>
    {
        h.AdditionalNewLineAfterOption = false;
        h.Heading = "RAW Photo Extractor";
        h.Copyright = "Copyright (c) 2025";
        return CommandLine.Text.HelpText.DefaultParsingErrorsHandler(result, h);
    }, e => e);

    Console.WriteLine(helpText);
    return 1;
}

