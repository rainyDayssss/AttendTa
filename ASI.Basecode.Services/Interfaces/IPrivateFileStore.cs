using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces;

/// <summary>Names the private storage areas that application workflows may use.</summary>
public enum PrivateFileArea
{
    VenueImages,
    EventImages,
    Certificates,
    Temporary
}

/// <summary>Stores private application files outside the web root.</summary>
public interface IPrivateFileStore
{
    /// <summary>Writes a file into a supported private area and returns its relative key, or a physical temporary path for the temporary area.</summary>
    Task<string> SaveAsync(PrivateFileArea area, Stream content, string extension, CancellationToken cancellationToken = default);

    /// <summary>Creates a unique, empty file under the private temporary area and returns its physical path for a renderer to populate.</summary>
    string CreateTemporaryPath(string extension);

    /// <summary>Validates and moves a generated temporary file into a permanent private area without overwriting.</summary>
    Task<string> PromoteAsync(string temporaryPath, PrivateFileArea area, CancellationToken cancellationToken = default);

    /// <summary>Opens a private file by its relative key (for example, uploads/venues/abc.jpg).</summary>
    Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>Deletes a private relative key or a generated physical temporary path.</summary>
    void Delete(string relativePath);
}
