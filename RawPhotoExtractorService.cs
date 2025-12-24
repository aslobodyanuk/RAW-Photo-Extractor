using Microsoft.Extensions.Logging;

namespace RAW.Photo.Extractor;

public class RawPhotoExtractorService
{
    private readonly AppConfig _config;
    private readonly ILogger<RawPhotoExtractorService> _logger;

    public RawPhotoExtractorService(AppConfig config, ILogger<RawPhotoExtractorService> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Enumerates all files recursively from the specified directory.
    /// </summary>
    public List<string> EnumerateFilesRecursively(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Directory does not exist: {directoryPath}");
        }

        return Directory.EnumerateFiles(
            directoryPath,
            "*.*",
            SearchOption.AllDirectories
        ).ToList();
    }

    /// <summary>
    /// Extracts unique base names (without extensions) from a list of file paths.
    /// </summary>
    public List<string> ExtractBaseNames(List<string> filePaths)
    {
        return filePaths
            .Select(file => Path.GetFileNameWithoutExtension(file))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Finds all RAW files that match the given base names.
    /// </summary>
    public Dictionary<string, List<string>> FindMatchingRawFiles(
        List<string> baseNames,
        List<string> allFilesInRootDirectory)
    {
        var rawFilesToCopy = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var baseName in baseNames)
        {
            var matchingRawFiles = allFilesInRootDirectory
                .Where(rawFile => IsMatchingRawFile(rawFile, baseName))
                .ToList();

            if (matchingRawFiles.Any())
            {
                rawFilesToCopy[baseName] = matchingRawFiles;
            }
        }

        return rawFilesToCopy;
    }

    /// <summary>
    /// Checks if a file is a matching RAW file for the given base name.
    /// </summary>
    private bool IsMatchingRawFile(string filePath, string baseName)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();

        return string.Equals(fileName, baseName, StringComparison.OrdinalIgnoreCase) &&
               _config.RawFileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Copies all RAW files to the output directory and returns copy statistics.
    /// </summary>
    public CopyResult CopyRawFilesToOutput(
        Dictionary<string, List<string>> rawFilesToCopy,
        string outputDirectory)
    {
        var result = new CopyResult();

        foreach (var kvp in rawFilesToCopy)
        {
            foreach (var rawFile in kvp.Value)
            {
                try
                {
                    var destinationPath = GetUniqueDestinationPath(rawFile, outputDirectory);
                    File.Copy(rawFile, destinationPath, overwrite: false);
                    result.CopiedFiles.Add((rawFile, destinationPath));
                    result.CopiedCount++;
                    _logger.LogDebug("Copied file: {SourceFile} -> {DestinationFile}", rawFile, destinationPath);
                }
                catch (Exception ex)
                {
                    result.Errors.Add((rawFile, ex.Message));
                    result.ErrorCount++;
                    _logger.LogError(ex, "Error copying file: {FilePath}", rawFile);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Gets a unique destination path, handling duplicate file names by appending a number.
    /// </summary>
    private string GetUniqueDestinationPath(string sourceFile, string outputDirectory)
    {
        var fileName = Path.GetFileName(sourceFile);
        var destinationPath = Path.Combine(outputDirectory, fileName);

        if (!File.Exists(destinationPath))
        {
            return destinationPath;
        }

        // Handle duplicate file names by appending a number
        var baseFileName = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        int counter = 1;

        do
        {
            destinationPath = Path.Combine(
                outputDirectory,
                $"{baseFileName}_{counter}{extension}"
            );
            counter++;
        } while (File.Exists(destinationPath));

        return destinationPath;
    }

    /// <summary>
    /// Validates that a directory exists, throws DirectoryNotFoundException if not.
    /// </summary>
    public void ValidateDirectoryExists(string directoryPath, string directoryName)
    {
        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"{directoryName} does not exist: {directoryPath}");
        }
    }

    /// <summary>
    /// Ensures the output directory exists, creating it if necessary.
    /// </summary>
    public void EnsureOutputDirectoryExists(string outputDirectory)
    {
        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }
    }
}

/// <summary>
/// Result of copying RAW files to output directory.
/// </summary>
public class CopyResult
{
    public List<(string SourceFile, string DestinationFile)> CopiedFiles { get; } = new();
    public List<(string FilePath, string ErrorMessage)> Errors { get; } = new();
    public int CopiedCount { get; set; }
    public int ErrorCount { get; set; }
}

