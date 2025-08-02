using AgeDigitalTwins.Models;

namespace AgeDigitalTwins.Test.Infrastructure;

/// <summary>
/// Shared assertion helpers for job testing to ensure consistent validation across all job tests.
/// </summary>
public static class JobAssertions
{
    /// <summary>
    /// Asserts basic job properties are correct.
    /// </summary>
    public static void AssertJobBasicProperties(
        JobRecord job,
        string expectedId,
        string expectedType
    )
    {
        Assert.NotNull(job);
        Assert.Equal(expectedId, job.Id);
        Assert.Equal(expectedType, job.JobType);
        Assert.True(job.CreatedDateTime <= DateTime.UtcNow);
        Assert.True(job.PurgeDateTime > DateTime.UtcNow);
    }

    /// <summary>
    /// Asserts that a job completed successfully.
    /// </summary>
    public static void AssertJobSuccess(JobRecord job)
    {
        Assert.True(
            job.Status == JobStatus.Succeeded || job.Status == JobStatus.PartiallySucceeded,
            $"Expected job to succeed but got status: {job.Status}"
        );
        Assert.NotNull(job.FinishedDateTime);
        Assert.True(job.LastActionDateTime <= DateTime.UtcNow);
    }

    /// <summary>
    /// Asserts import job results with expected counts.
    /// </summary>
    public static void AssertImportCounts(
        JobRecord job,
        int expectedModels,
        int expectedTwins,
        int expectedRelationships
    )
    {
        Assert.Equal(expectedModels, job.ModelsCreated);
        Assert.Equal(expectedTwins, job.TwinsCreated);
        Assert.Equal(expectedRelationships, job.RelationshipsCreated);
    }

    /// <summary>
    /// Asserts delete job results with expected counts.
    /// </summary>
    public static void AssertDeleteCounts(
        JobRecord job,
        int expectedModels,
        int expectedTwins,
        int expectedRelationships
    )
    {
        Assert.Equal(expectedModels, job.ModelsDeleted);
        Assert.Equal(expectedTwins, job.TwinsDeleted);
        Assert.Equal(expectedRelationships, job.RelationshipsDeleted);
    }

    /// <summary>
    /// Asserts that a job has no errors.
    /// </summary>
    public static void AssertNoErrors(JobRecord job)
    {
        Assert.Equal(0, job.ErrorCount);
    }

    /// <summary>
    /// Asserts that a job has the expected number of errors.
    /// </summary>
    public static void AssertErrorCount(JobRecord job, int expectedErrors)
    {
        Assert.Equal(expectedErrors, job.ErrorCount);
    }

    /// <summary>
    /// Asserts that a job is in the expected status.
    /// </summary>
    public static void AssertJobStatus(JobRecord job, JobStatus expectedStatus)
    {
        Assert.Equal(expectedStatus, job.Status);
    }

    /// <summary>
    /// Asserts that a job has completed (finished datetime is set).
    /// </summary>
    public static void AssertJobCompleted(JobRecord job)
    {
        Assert.NotNull(job.FinishedDateTime);
        Assert.True(job.FinishedDateTime <= DateTime.UtcNow);
    }

    /// <summary>
    /// Asserts that a job is still running (finished datetime is null).
    /// </summary>
    public static void AssertJobRunning(JobRecord job)
    {
        Assert.Equal(JobStatus.Running, job.Status);
        Assert.Null(job.FinishedDateTime);
    }

    /// <summary>
    /// Asserts that job deletion counts are all non-negative (for delete jobs).
    /// </summary>
    public static void AssertDeleteCountsNonNegative(JobRecord job)
    {
        Assert.True(
            job.RelationshipsDeleted >= 0,
            "Relationships deleted count should be non-negative"
        );
        Assert.True(job.TwinsDeleted >= 0, "Twins deleted count should be non-negative");
        Assert.True(job.ModelsDeleted >= 0, "Models deleted count should be non-negative");
    }

    /// <summary>
    /// Asserts that job creation counts are all non-negative (for import jobs).
    /// </summary>
    public static void AssertImportCountsNonNegative(JobRecord job)
    {
        Assert.True(
            job.RelationshipsCreated >= 0,
            "Relationships created count should be non-negative"
        );
        Assert.True(job.TwinsCreated >= 0, "Twins created count should be non-negative");
        Assert.True(job.ModelsCreated >= 0, "Models created count should be non-negative");
    }
}
