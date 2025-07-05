using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AgeDigitalTwins.Jobs;
using AgeDigitalTwins.Jobs.Models;

namespace AgeDigitalTwins;

public partial class AgeDigitalTwinsClient
{
    /// <summary>
    /// Imports models, twins, and relationships from an ND-JSON stream.
    /// </summary>
    /// <param name="inputStream">The ND-JSON input stream containing the data to import.</param>
    /// <param name="outputStream">The output stream where structured log entries will be written.</param>
    /// <param name="options">Configuration options for the import job. If null, default options will be used.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the import job result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when inputStream or outputStream is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the input stream contains invalid data.</exception>
    /// <remarks>
    /// <para>
    /// The input stream should contain ND-JSON (Newline Delimited JSON) data with the following format:
    /// </para>
    /// <list type="number">
    /// <item>Header section (optional): Contains metadata like file version, author, organization</item>
    /// <item>Models section (optional): Contains DTDL model definitions</item>
    /// <item>Twins section (optional): Contains digital twin instances</item>
    /// <item>Relationships section (optional): Contains relationships between twins</item>
    /// </list>
    /// <para>
    /// Each section is indicated by a JSON line with a "Section" property, followed by the data lines for that section.
    /// </para>
    /// <para>
    /// Example input format:
    /// </para>
    /// <code>
    /// {"Section": "Header"}
    /// {"fileVersion": "1.0.0", "author": "user", "organization": "company"}
    /// {"Section": "Models"}
    /// {"@id":"dtmi:example:model;1","@type":"Interface",...}
    /// {"Section": "Twins"}
    /// {"$dtId":"twin1","$metadata":{"$model":"dtmi:example:model;1"},...}
    /// {"Section": "Relationships"}
    /// {"$dtId":"twin1","$relationshipId":"rel1","$targetId":"twin2",...}
    /// </code>
    /// <para>
    /// The output stream will receive structured log entries in JSON format documenting the progress and results of the import operation.
    /// </para>
    /// </remarks>
    public virtual async Task<ImportJobResult> ImportAsync(
        Stream inputStream,
        Stream outputStream,
        ImportJobOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        if (inputStream == null)
            throw new ArgumentNullException(nameof(inputStream));
        if (outputStream == null)
            throw new ArgumentNullException(nameof(outputStream));

        options ??= new ImportJobOptions();
        var jobId = Guid.NewGuid().ToString("N")[..8]; // Use first 8 characters for shorter job ID

        return await StreamingImportJob.ExecuteAsync(
            this,
            inputStream,
            outputStream,
            jobId,
            options,
            cancellationToken
        );
    }
}
