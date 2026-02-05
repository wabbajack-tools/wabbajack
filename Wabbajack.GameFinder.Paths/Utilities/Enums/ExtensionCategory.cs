using JetBrains.Annotations;

namespace Wabbajack.GameFinder.Paths.Utilities.Enums;

/// <summary>
///     Provides categories for each common extension.
/// </summary>
[PublicAPI]
public enum ExtensionCategory : byte
{
    /// <summary>
    /// Unknown file format.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Represents archive and compressed file formats.
    /// </summary>
    Archive = 1,

    /// <summary>
    /// Represents archives containing audio data.
    /// </summary>
    ArchiveOfAudio = 2,

    /// <summary>
    /// Represents archives containing texture/image data.
    /// </summary>
    ArchiveOfImage = 3,

    /// <summary>
    /// Represents audio and sound file formats.
    /// </summary>
    Audio = 4,

    /// <summary>
    /// Represents binary.
    /// </summary>
    Binary = 5,

    /// <summary>
    /// Represents databases and other files which contain a collection of 'records'.
    /// </summary>
    Database = 6,

    /// <summary>
    /// Represents executable and application file formats.
    /// These should be binaries that are intended to be directly executed
    /// by the user.
    /// </summary>
    Executable = 7,

    /// <summary>
    /// Represents image and graphic file formats.
    /// </summary>
    Image = 8,

    /// <summary>
    /// Represents dynamic library and code library formats.
    /// </summary>
    Library = 9,

    /// <summary>
    /// Represents 2D and 3D model and related file formats.
    /// </summary>
    Model = 10,

    /// <summary>
    /// Represents script and source code file formats. Must be human readable.
    /// </summary>
    Script = 11,

    /// <summary>
    /// Represents text, configuration, and documentation file formats.
    /// </summary>
    Text = 12,

    /// <summary>
    /// Represents video, movie, and cutscene file formats.
    /// </summary>
    Video = 13,
}
