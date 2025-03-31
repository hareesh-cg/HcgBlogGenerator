using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HcgBlogGenerator.Core.Models;

/// <summary>
/// Represents a static asset file (e.g., CSS, JavaScript, image) found in the source directory.
/// </summary>
public class Asset {
    /// <summary>
    /// The absolute path to the original asset file in the source directory.
    /// </summary>
    public required string SourcePath { get; init; }

    /// <summary>
    /// The absolute path where the processed or copied asset file will be written in the output directory.
    /// </summary>
    public required string OutputPath { get; init; }

    /// <summary>
    /// The type of asset, used to determine how it should be processed.
    /// </summary>
    public required AssetType Type { get; init; }
}

/// <summary>
/// Defines the types of assets that might require different processing.
/// </summary>
public enum AssetType {
    /// <summary>
    /// A file to be compiled using an SCSS compiler (e.g., .scss, .sass).
    /// </summary>
    Scss,

    /// <summary>
    /// A standard CSS file, typically just copied.
    /// </summary>
    Css,

    /// <summary>
    /// A JavaScript file, typically just copied (minification could be added later).
    /// </summary>
    JavaScript,

    /// <summary>
    /// An image file (e.g., png, jpg, svg), typically just copied (optimization could be added later).
    /// </summary>
    Image,

    /// <summary>
    /// A font file, typically just copied.
    /// </summary>
    Font,

    /// <summary>
    /// Any other file type that should just be copied directly to the output directory.
    /// </summary>
    Other
}
