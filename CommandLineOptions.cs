using CommandLine;

namespace RAW.Photo.Extractor;

public class CommandLineOptions
{
    [Option('n', "NonRawPhotosDirectory", Required = true, HelpText = "Directory containing non-RAW photos to use as reference for finding RAW files.")]
    public string NonRawPhotosDirectory { get; set; } = string.Empty;

    [Option('r', "RootDirectory", Required = true, HelpText = "Root directory to search for RAW files recursively.")]
    public string RootDirectory { get; set; } = string.Empty;

    [Option('o', "OutputDirectory", Required = true, HelpText = "Directory where extracted RAW files will be copied.")]
    public string OutputDirectory { get; set; } = string.Empty;
}

