using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace StopwatchOverlay;

public sealed record AppBackgroundChoice(
    string Id,
    string DisplayName,
    string? ResourceUri,
    string? FilePath,
    bool IsCustom)
{
    public bool IsAvailable { get; set; } = true;
    public string DisplayLabel => IsAvailable
        ? DisplayName
        : $"{DisplayName} (unavailable)";
    public bool IsThemeDefault => Id == AppBackgroundCatalog.ThemeDefault;
    public override string ToString() => DisplayLabel;
}

public static class AppBackgroundCatalog
{
    public const string ThemeDefault = "theme-default";
    public const string FestiveChalk = "preset:festive-chalk";
    public const string WoodlandMushrooms = "preset:woodland-mushrooms";
    public const string AutumnPatchwork = "preset:autumn-patchwork";
    public const string GreenCreatures = "preset:green-creatures";
    public const string AquaTattoo = "preset:aqua-tattoo";
    public const string SapphireGarden = "preset:sapphire-garden";
    public const string TurquoisePomegranate = "preset:turquoise-pomegranate";
    public const string MidnightPaisley = "preset:midnight-paisley";
    public const string AzureMosaic = "preset:azure-mosaic";

    public const double DefaultPatternStrength = 30;
    public const double MinimumPatternStrength = 10;
    public const double MaximumPatternStrength = 45;

    public const int MaximumCustomBackgrounds = 64;
    public const int MaximumDisplayNameLength = 80;
    public const long MaximumImportBytes = 25L * 1024 * 1024;
    public const int MaximumImageDimension = 8_192;
    public const long MaximumPixelCount = 40_000_000;

    private static readonly string[] SupportedExtensions = [".jpg", ".jpeg", ".png", ".bmp"];

    public static Func<Stream, string, (int Width, int Height)>? ImageDimensionReader { get; set; }
    public static Func<Stream, string, (int Width, int Height)>? ImageValidator { get; set; }

    private static readonly AppBackgroundChoice[] BuiltInChoices =
    [
        new(ThemeDefault, "Theme default", null, null, false),
        new(FestiveChalk, "Festive Chalk",
            "pack://application:,,,/StopwatchOverlay;component/Assets/Backgrounds/festive-chalk.jpg", null, false),
        new(WoodlandMushrooms, "Woodland Mushrooms",
            "pack://application:,,,/StopwatchOverlay;component/Assets/Backgrounds/woodland-mushrooms.jpg", null, false),
        new(AutumnPatchwork, "Autumn Patchwork",
            "pack://application:,,,/StopwatchOverlay;component/Assets/Backgrounds/autumn-patchwork.jpg", null, false),
        new(GreenCreatures, "Green Creatures",
            "pack://application:,,,/StopwatchOverlay;component/Assets/Backgrounds/green-creatures.jpg", null, false),
        new(AquaTattoo, "Aqua Tattoo",
            "pack://application:,,,/StopwatchOverlay;component/Assets/Backgrounds/aqua-tattoo.jpg", null, false),
        new(SapphireGarden, "Sapphire Garden",
            "pack://application:,,,/StopwatchOverlay;component/Assets/Backgrounds/sapphire-garden.jpg", null, false),
        new(TurquoisePomegranate, "Turquoise Pomegranate",
            "pack://application:,,,/StopwatchOverlay;component/Assets/Backgrounds/turquoise-pomegranate.jpg", null, false),
        new(MidnightPaisley, "Midnight Paisley",
            "pack://application:,,,/StopwatchOverlay;component/Assets/Backgrounds/midnight-paisley.jpg", null, false),
        new(AzureMosaic, "Azure Mosaic",
            "pack://application:,,,/StopwatchOverlay;component/Assets/Backgrounds/azure-mosaic.jpg", null, false)
    ];

    public static IReadOnlyList<string> BuiltInIds { get; } =
        BuiltInChoices.Select(choice => choice.Id).ToArray();

    public static string ManagedDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StopwatchOverlay",
        "Backgrounds");

    public static void NormalizeSettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        settings.PanelBackgroundStrength = double.IsFinite(settings.PanelBackgroundStrength)
            ? Math.Clamp(
                settings.PanelBackgroundStrength,
                MinimumPatternStrength,
                MaximumPatternStrength)
            : DefaultPatternStrength;

        var normalized = new List<CustomAppBackground>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (CustomAppBackground? item in settings.CustomBackgrounds ?? [])
        {
            string normalizedId = Guid.TryParse(item?.Id, out Guid parsed)
                ? parsed.ToString("N")
                : "";
            if (normalized.Count >= MaximumCustomBackgrounds
                || item == null
                || normalizedId.Length == 0
                || !TryNormalizeManagedFileName(item.FileName, out string fileName)
                || !Path.GetFileNameWithoutExtension(fileName).Equals(
                    $"custom-{normalizedId}",
                    StringComparison.OrdinalIgnoreCase)
                || !seenIds.Add(normalizedId)
                || !seenFiles.Add(fileName))
            {
                continue;
            }

            normalized.Add(new CustomAppBackground
            {
                Id = normalizedId,
                DisplayName = NormalizeDisplayName(item.DisplayName),
                FileName = fileName
            });
        }

        settings.CustomBackgrounds = normalized;
        settings.PanelBackgroundId = NormalizeSelection(
            settings.PanelBackgroundId,
            normalized);
    }

    public static string NormalizeSelection(
        string? requestedId,
        IEnumerable<CustomAppBackground>? customBackgrounds)
    {
        string candidate = requestedId?.Trim() ?? "";
        AppBackgroundChoice? builtIn = BuiltInChoices.FirstOrDefault(choice =>
            candidate.Equals(choice.Id, StringComparison.OrdinalIgnoreCase)
            || candidate.Equals(choice.DisplayName, StringComparison.OrdinalIgnoreCase));
        if (builtIn != null)
            return builtIn.Id;

        if (candidate.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
        {
            string requestedCustomId = candidate["custom:".Length..];
            CustomAppBackground? custom = customBackgrounds?.FirstOrDefault(item =>
                item != null
                && requestedCustomId.Equals(item.Id, StringComparison.OrdinalIgnoreCase));
            if (custom != null)
                return CustomSelectionId(custom.Id);
        }

        return ThemeDefault;
    }

    public static IReadOnlyList<AppBackgroundChoice> GetAvailableChoices(
        AppSettings settings,
        string? managedDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        NormalizeSettings(settings);
        string root = managedDirectory ?? ManagedDirectory;
        var choices = new List<AppBackgroundChoice>(BuiltInChoices);
        foreach (CustomAppBackground custom in settings.CustomBackgrounds)
        {
            bool resolved = TryResolveManagedPath(root, custom.FileName, out string fullPath);
            var choice = new AppBackgroundChoice(
                CustomSelectionId(custom.Id),
                custom.DisplayName,
                null,
                resolved ? fullPath : null,
                true)
            {
                IsAvailable = resolved && CanReadManagedImage(fullPath)
            };
            choices.Add(choice);
        }

        return choices;
    }

    public static AppBackgroundChoice ResolveChoice(
        AppSettings settings,
        string? managedDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        NormalizeSettings(settings);

        AppBackgroundChoice? builtIn = BuiltInChoices.FirstOrDefault(choice =>
            choice.Id.Equals(settings.PanelBackgroundId, StringComparison.OrdinalIgnoreCase));
        if (builtIn != null)
            return builtIn;

        if (settings.PanelBackgroundId.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
        {
            string requestedId = settings.PanelBackgroundId["custom:".Length..];
            CustomAppBackground? custom = settings.CustomBackgrounds.FirstOrDefault(item =>
                item.Id.Equals(requestedId, StringComparison.OrdinalIgnoreCase));
            string root = managedDirectory ?? ManagedDirectory;
            if (custom != null
                && TryResolveManagedPath(root, custom.FileName, out string fullPath)
                && CanReadManagedImage(fullPath))
            {
                return new AppBackgroundChoice(
                    CustomSelectionId(custom.Id),
                    custom.DisplayName,
                    null,
                    fullPath,
                    true);
            }
        }

        settings.PanelBackgroundId = ThemeDefault;
        return BuiltInChoices[0];
    }

    public static bool TryImport(
        string sourcePath,
        IEnumerable<CustomAppBackground>? existing,
        out CustomAppBackground? imported,
        out string? error,
        string? managedDirectory = null)
    {
        imported = null;
        error = null;
        string? temporaryPath = null;
        string? destinationPath = null;

        try
        {
            if ((existing ?? []).Count(item => item != null) >= MaximumCustomBackgrounds)
            {
                error = $"The background library can contain up to {MaximumCustomBackgrounds} custom images.";
                return false;
            }

            string sourceFullPath = Path.GetFullPath(sourcePath);
            var source = new FileInfo(sourceFullPath);
            if (!source.Exists)
            {
                error = "That image could not be found.";
                return false;
            }

            string extension = source.Extension.ToLowerInvariant();
            if (!SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                error = "Choose a JPG, JPEG, PNG, or BMP image.";
                return false;
            }

            if (source.Length <= 0 || source.Length > MaximumImportBytes)
            {
                error = "Choose an image smaller than 25 MB.";
                return false;
            }

            if (!TryValidateImage(sourceFullPath, extension, out error))
                return false;

            string root = Path.GetFullPath(managedDirectory ?? ManagedDirectory);
            Directory.CreateDirectory(root);

            string id = Guid.NewGuid().ToString("N");
            string fileName = $"custom-{id}{extension}";
            if (!TryResolveManagedPath(root, fileName, out destinationPath))
            {
                error = "The custom background location is not available.";
                return false;
            }

            temporaryPath = destinationPath + ".tmp";
            using (var input = new FileStream(
                       sourceFullPath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read))
            using (var output = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       81920,
                       FileOptions.WriteThrough))
            {
                if (input.Length <= 0 || input.Length > MaximumImportBytes)
                    throw new InvalidDataException("The image size changed while it was being imported.");
                CopyWithLimit(input, output, MaximumImportBytes);
                output.Flush(flushToDisk: true);
            }

            if (!TryValidateImage(temporaryPath, extension, out error))
                return false;

            File.Move(temporaryPath, destinationPath);
            temporaryPath = null;

            imported = new CustomAppBackground
            {
                Id = id,
                DisplayName = MakeUniqueDisplayName(
                    NormalizeDisplayName(Path.GetFileNameWithoutExtension(source.Name)),
                    existing),
                FileName = fileName
            };
            return true;
        }
        catch (Exception exception) when (IsExpectedImageBoundaryFailure(exception))
        {
            error = "The image could not be imported because it is damaged or unsupported.";
            return false;
        }
        finally
        {
            if (temporaryPath != null)
            {
                try
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch (Exception exception) when (IsExpectedImageBoundaryFailure(exception))
                {
                    // Clean up attempts are best-effort.
                }
            }
        }
    }

    public static bool DeleteManagedCopy(
        CustomAppBackground? custom,
        string? managedDirectory = null)
    {
        if (custom == null)
            return false;

        string root = Path.GetFullPath(managedDirectory ?? ManagedDirectory);
        if (!TryResolveManagedPath(root, custom.FileName, out string fullPath))
            return false;

        try
        {
            if (File.Exists(fullPath))
                File.Delete(fullPath);
            return true;
        }
        catch (Exception exception) when (IsExpectedImageBoundaryFailure(exception))
        {
            CrashLogger.LogRecoverable(exception, "ManagedBackgroundDelete");
            return false;
        }
    }

    public static string CustomSelectionId(string id) => $"custom:{id}";

    private static bool TryValidateImage(
        string path,
        string extension,
        out string? error)
    {
        error = null;
        try
        {
            int width, height;
            if (ImageValidator != null)
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                (width, height) = ImageValidator(stream, extension);
            }
            else if (ImageDimensionReader != null)
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                (width, height) = ImageDimensionReader(stream, extension);
            }
            else
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (!TryReadImageDimensions(stream, extension, out width, out height))
                {
                    error = "The image could not be read or its format does not match its extension.";
                    return false;
                }
            }

            if (width <= 0
                || height <= 0
                || width > MaximumImageDimension
                || height > MaximumImageDimension
                || (long)width * height > MaximumPixelCount)
            {
                error = "Choose an image no larger than 8,192 pixels per side or 40 megapixels.";
                return false;
            }

            return true;
        }
        catch (Exception exception) when (IsExpectedImageBoundaryFailure(exception))
        {
            error = "The image could not be read or its format does not match its extension.";
            return false;
        }
    }

    private static bool CanReadManagedImage(string path)
    {
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists
                || file.Length <= 0
                || file.Length > MaximumImportBytes)
            {
                return false;
            }

            string extension = file.Extension.ToLowerInvariant();
            int width, height;
            if (ImageDimensionReader != null)
            {
                using var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                (width, height) = ImageDimensionReader(stream, extension);
            }
            else
            {
                using var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                if (!TryReadImageDimensions(stream, extension, out width, out height))
                    return false;
            }

            return width > 0
                   && height > 0
                   && width <= MaximumImageDimension
                   && height <= MaximumImageDimension
                   && (long)width * height <= MaximumPixelCount;
        }
        catch (Exception exception) when (IsExpectedImageBoundaryFailure(exception))
        {
            return false;
        }
    }

    private static bool TryReadImageDimensions(Stream stream, string extension, out int width, out int height)
    {
        width = 0;
        height = 0;
        try
        {
            Span<byte> header = stackalloc byte[32];
            int read = stream.Read(header);
            if (read < 8) return false;

            if (extension is ".png")
            {
                // PNG signature: 89 50 4E 47 0D 0A 1A 0A
                if (header[0] != 0x89 || header[1] != 0x50 || header[2] != 0x4E || header[3] != 0x47 ||
                    header[4] != 0x0D || header[5] != 0x0A || header[6] != 0x1A || header[7] != 0x0A)
                    return false;
                if (read < 24) return false;
                width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
                height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
                return true;
            }

            if (extension is ".bmp")
            {
                // BMP signature: 'B', 'M'
                if (header[0] != 0x42 || header[1] != 0x4D) return false;
                if (read < 26) return false;
                int dibHeaderSize = header[14] | (header[15] << 8) | (header[16] << 16) | (header[17] << 24);
                if (dibHeaderSize == 12)
                {
                    width = header[18] | (header[19] << 8);
                    height = header[20] | (header[21] << 8);
                }
                else
                {
                    width = header[18] | (header[19] << 8) | (header[20] << 16) | (header[21] << 24);
                    height = Math.Abs(header[22] | (header[23] << 8) | (header[24] << 16) | (header[25] << 24));
                }
                return true;
            }

            if (extension is ".jpg" or ".jpeg")
            {
                // JPEG signature: FF D8
                if (header[0] != 0xFF || header[1] != 0xD8) return false;

                stream.Position = 2;
                while (true)
                {
                    int markerPrefix = stream.ReadByte();
                    while (markerPrefix == 0xFF)
                    {
                        markerPrefix = stream.ReadByte();
                    }
                    if (markerPrefix == -1) return false;

                    byte marker = (byte)markerPrefix;
                    if (marker == 0xD9 || marker == 0xDA) // EOI or SOS
                        return false;

                    int lenHigh = stream.ReadByte();
                    int lenLow = stream.ReadByte();
                    if (lenHigh == -1 || lenLow == -1) return false;
                    int length = (lenHigh << 8) | lenLow;
                    if (length < 2) return false;

                    // SOF0..SOF3, SOF5..SOF7, SOF9..SOF11, SOF13..SOF15
                    if (marker is >= 0xC0 and <= 0xCF && marker is not 0xC4 and not 0xC8 and not 0xCC)
                    {
                        int precision = stream.ReadByte();
                        int hHigh = stream.ReadByte();
                        int hLow = stream.ReadByte();
                        int wHigh = stream.ReadByte();
                        int wLow = stream.ReadByte();
                        if (precision == -1 || hHigh == -1 || hLow == -1 || wHigh == -1 || wLow == -1) return false;
                        height = (hHigh << 8) | hLow;
                        width = (wHigh << 8) | wLow;
                        return true;
                    }

                    stream.Position += length - 2;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static void CopyWithLimit(Stream input, Stream output, long maximumBytes)
    {
        byte[] buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += read;
            if (total > maximumBytes)
                throw new InvalidDataException("The image exceeded the import size limit.");
            output.Write(buffer, 0, read);
        }
    }

    public static bool IsExpectedImageBoundaryFailure(Exception exception)
        => exception is IOException
            or UnauthorizedAccessException
            or System.Security.SecurityException
            or NotSupportedException
            or ArgumentException
            or InvalidOperationException
            or FormatException;

    private static string MakeUniqueDisplayName(
        string requested,
        IEnumerable<CustomAppBackground>? existing)
    {
        var used = new HashSet<string>(
            BuiltInChoices.Select(choice => choice.DisplayName)
                .Concat((existing ?? [])
                    .Where(item => item != null)
                    .Select(item => item.DisplayName)),
            StringComparer.OrdinalIgnoreCase);
        if (!used.Contains(requested))
            return requested;

        for (int suffix = 2; suffix < 10_000; suffix++)
        {
            string suffixText = $" ({suffix})";
            int prefixLength = Math.Max(
                1,
                MaximumDisplayNameLength - suffixText.Length);
            string candidate = requested[..Math.Min(requested.Length, prefixLength)] + suffixText;
            if (!used.Contains(candidate))
                return candidate;
        }

        return "Custom background";
    }

    private static string NormalizeDisplayName(string? name)
    {
        string cleaned = new((name ?? "")
            .Where(character => !char.IsControl(character))
            .ToArray());
        cleaned = cleaned.Trim();
        if (cleaned.Length == 0)
            cleaned = "Custom background";
        return cleaned[..Math.Min(cleaned.Length, MaximumDisplayNameLength)];
    }

    private static bool TryNormalizeManagedFileName(
        string? requested,
        out string fileName)
    {
        fileName = Path.GetFileName(requested ?? "");
        string extension = Path.GetExtension(fileName);
        return fileName.Length > 0
               && fileName.Equals(requested, StringComparison.Ordinal)
               && SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryResolveManagedPath(
        string root,
        string? requestedFileName,
        out string fullPath)
    {
        fullPath = "";
        if (!TryNormalizeManagedFileName(requestedFileName, out string fileName))
            return false;

        string fullRoot = Path.GetFullPath(root).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        string candidate = Path.GetFullPath(Path.Combine(fullRoot, fileName));
        if (!string.Equals(
                Path.GetDirectoryName(candidate),
                fullRoot,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        fullPath = candidate;
        return true;
    }
}
