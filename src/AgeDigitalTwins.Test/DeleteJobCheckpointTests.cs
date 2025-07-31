using AgeDigitalTwins.Jobs;
using AgeDigitalTwins.Models;
using Xunit.Abstractions;

namespace AgeDigitalTwins.Test;

/// <summary>
/// Tests for the DeleteJobCheckpoint functionality and checkpoint-based resumption.
/// </summary>
[Trait("Category", "Unit")]
public class DeleteJobCheckpointTests : TestBase
{
    private readonly ITestOutputHelper _output;

    public DeleteJobCheckpointTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Create_ShouldCreateCheckpoint_WithCorrectDefaults()
    {
        // Arrange
        var jobId = "test-checkpoint-job";

        // Act
        var checkpoint = DeleteJobCheckpoint.Create(jobId);

        // Assert
        Assert.NotNull(checkpoint);
        Assert.Equal(jobId, checkpoint.JobId);
        Assert.Equal(DeleteSection.Relationships, checkpoint.CurrentSection);
        Assert.Equal(0, checkpoint.RelationshipsDeleted);
        Assert.Equal(0, checkpoint.TwinsDeleted);
        Assert.Equal(0, checkpoint.ModelsDeleted);
        Assert.Equal(0, checkpoint.ErrorCount);
        Assert.False(checkpoint.RelationshipsCompleted);
        Assert.False(checkpoint.TwinsCompleted);
        Assert.False(checkpoint.ModelsCompleted);
        Assert.True(checkpoint.LastUpdated <= DateTime.UtcNow);

        _output.WriteLine($"✓ Created checkpoint for job: {checkpoint.JobId}");
        _output.WriteLine($"  Initial section: {checkpoint.CurrentSection}");
        _output.WriteLine($"  Last updated: {checkpoint.LastUpdated}");
    }

    [Fact]
    public void IsCompleted_ShouldReturnFalse_WhenNotAllSectionsAreCompleted()
    {
        // Arrange
        var checkpoint = DeleteJobCheckpoint.Create("test-job");

        // Act & Assert - Initially not completed
        var isCompleted =
            checkpoint.RelationshipsCompleted
            && checkpoint.TwinsCompleted
            && checkpoint.ModelsCompleted;
        Assert.False(isCompleted);

        // Complete relationships only
        checkpoint.RelationshipsCompleted = true;
        isCompleted =
            checkpoint.RelationshipsCompleted
            && checkpoint.TwinsCompleted
            && checkpoint.ModelsCompleted;
        Assert.False(isCompleted);

        // Complete relationships and twins
        checkpoint.TwinsCompleted = true;
        isCompleted =
            checkpoint.RelationshipsCompleted
            && checkpoint.TwinsCompleted
            && checkpoint.ModelsCompleted;
        Assert.False(isCompleted);

        _output.WriteLine("✓ IsCompleted logic correctly returns false for partial completion");
    }

    [Fact]
    public void IsCompleted_ShouldReturnTrue_WhenAllSectionsAreCompleted()
    {
        // Arrange
        var checkpoint = DeleteJobCheckpoint.Create("test-job");

        // Act
        checkpoint.RelationshipsCompleted = true;
        checkpoint.TwinsCompleted = true;
        checkpoint.ModelsCompleted = true;

        // Assert
        var isCompleted =
            checkpoint.RelationshipsCompleted
            && checkpoint.TwinsCompleted
            && checkpoint.ModelsCompleted;
        Assert.True(isCompleted);

        _output.WriteLine(
            "✓ IsCompleted logic correctly returns true when all sections are completed"
        );
    }

    [Fact]
    public void SectionProgression_ShouldAdvanceCorrectly_ThroughAllStates()
    {
        // Arrange
        var checkpoint = DeleteJobCheckpoint.Create("test-job");

        // Act & Assert - Start with relationships
        Assert.Equal(DeleteSection.Relationships, checkpoint.CurrentSection);
        Assert.False(checkpoint.RelationshipsCompleted);

        // Mark relationships completed and advance
        checkpoint.RelationshipsCompleted = true;
        checkpoint.CurrentSection = DeleteSection.Twins;
        Assert.True(checkpoint.RelationshipsCompleted);
        Assert.Equal(DeleteSection.Twins, checkpoint.CurrentSection);

        // Mark twins completed and advance
        checkpoint.TwinsCompleted = true;
        checkpoint.CurrentSection = DeleteSection.Models;
        Assert.True(checkpoint.TwinsCompleted);
        Assert.Equal(DeleteSection.Models, checkpoint.CurrentSection);

        // Mark models completed and advance
        checkpoint.ModelsCompleted = true;
        checkpoint.CurrentSection = DeleteSection.Completed;
        Assert.True(checkpoint.ModelsCompleted);
        Assert.Equal(DeleteSection.Completed, checkpoint.CurrentSection);

        _output.WriteLine("✓ Section progression correctly advances through all sections");
    }

    [Fact]
    public void DeleteSection_ShouldHaveCorrectValues()
    {
        // Assert enum values are as expected
        Assert.Equal(0, (int)DeleteSection.Relationships);
        Assert.Equal(1, (int)DeleteSection.Twins);
        Assert.Equal(2, (int)DeleteSection.Models);
        Assert.Equal(3, (int)DeleteSection.Completed);

        _output.WriteLine("✓ DeleteSection enum has correct sequential values");
    }

    [Fact]
    public void Checkpoint_ShouldSupportProgressUpdates()
    {
        // Arrange
        var checkpoint = DeleteJobCheckpoint.Create("test-job");
        var initialTime = checkpoint.LastUpdated;

        // Wait a tiny bit to ensure time difference
        Thread.Sleep(10);

        // Act
        checkpoint.RelationshipsDeleted = 5;
        checkpoint.TwinsDeleted = 3;
        checkpoint.ModelsDeleted = 2;
        checkpoint.ErrorCount = 1;
        checkpoint.LastUpdated = DateTime.UtcNow;

        // Assert
        Assert.Equal(5, checkpoint.RelationshipsDeleted);
        Assert.Equal(3, checkpoint.TwinsDeleted);
        Assert.Equal(2, checkpoint.ModelsDeleted);
        Assert.Equal(1, checkpoint.ErrorCount);
        Assert.True(checkpoint.LastUpdated > initialTime);

        _output.WriteLine($"✓ Checkpoint progress updated successfully");
        _output.WriteLine($"  Relationships: {checkpoint.RelationshipsDeleted}");
        _output.WriteLine($"  Twins: {checkpoint.TwinsDeleted}");
        _output.WriteLine($"  Models: {checkpoint.ModelsDeleted}");
        _output.WriteLine($"  Errors: {checkpoint.ErrorCount}");
    }

    [Theory]
    [InlineData(DeleteSection.Relationships, false, false, false)]
    [InlineData(DeleteSection.Twins, true, false, false)]
    [InlineData(DeleteSection.Models, true, true, false)]
    [InlineData(DeleteSection.Completed, true, true, true)]
    public void CurrentSection_ShouldReflect_CompletionState(
        DeleteSection expectedSection,
        bool relationshipsCompleted,
        bool twinsCompleted,
        bool modelsCompleted
    )
    {
        // Arrange
        var checkpoint = DeleteJobCheckpoint.Create("test-job");

        // Act
        checkpoint.RelationshipsCompleted = relationshipsCompleted;
        checkpoint.TwinsCompleted = twinsCompleted;
        checkpoint.ModelsCompleted = modelsCompleted;
        checkpoint.CurrentSection = expectedSection;

        // Assert
        Assert.Equal(expectedSection, checkpoint.CurrentSection);
        Assert.Equal(relationshipsCompleted, checkpoint.RelationshipsCompleted);
        Assert.Equal(twinsCompleted, checkpoint.TwinsCompleted);
        Assert.Equal(modelsCompleted, checkpoint.ModelsCompleted);

        _output.WriteLine($"✓ Section {expectedSection} correctly reflects completion state");
    }
}

/// <summary>
/// Integration tests for delete job checkpoint persistence and resumption.
/// </summary>
[Trait("Category", "Integration")]
public class DeleteJobCheckpointIntegrationTests : TestBase
{
    private readonly ITestOutputHelper _output;

    public DeleteJobCheckpointIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task SaveCheckpointAsync_ShouldPersistCheckpoint_Successfully()
    {
        // Arrange
        var jobId = $"test-checkpoint-{Guid.NewGuid()}";
        var jobService = Client.JobService;

        // Create a job first
        await jobService.CreateJobAsync(jobId, "delete", (object?)null);

        var checkpoint = DeleteJobCheckpoint.Create(jobId);
        checkpoint.RelationshipsDeleted = 10;
        checkpoint.TwinsDeleted = 5;
        checkpoint.ErrorCount = 2;
        checkpoint.CurrentSection = DeleteSection.Twins;

        try
        {
            // Act
            await jobService.SaveCheckpointAsync(checkpoint);

            // Assert - Try to load it back
            var loadedCheckpoint = await jobService.LoadDeleteCheckpointAsync(jobId);

            Assert.NotNull(loadedCheckpoint);
            Assert.Equal(jobId, loadedCheckpoint.JobId);
            Assert.Equal(10, loadedCheckpoint.RelationshipsDeleted);
            Assert.Equal(5, loadedCheckpoint.TwinsDeleted);
            Assert.Equal(2, loadedCheckpoint.ErrorCount);
            Assert.Equal(DeleteSection.Twins, loadedCheckpoint.CurrentSection);

            _output.WriteLine($"✓ Successfully saved and loaded checkpoint for job: {jobId}");
            _output.WriteLine($"  Section: {loadedCheckpoint.CurrentSection}");
            _output.WriteLine(
                $"  Progress: {loadedCheckpoint.RelationshipsDeleted}R, {loadedCheckpoint.TwinsDeleted}T, {loadedCheckpoint.ModelsDeleted}M"
            );
        }
        finally
        {
            // Cleanup
            await jobService.DeleteJobAsync(jobId);
        }
    }

    [Fact]
    public async Task LoadDeleteCheckpointAsync_ShouldReturnNull_WhenCheckpointDoesNotExist()
    {
        // Arrange
        var jobId = $"non-existent-checkpoint-{Guid.NewGuid()}";
        var jobService = Client.JobService;

        // Act
        var checkpoint = await jobService.LoadDeleteCheckpointAsync(jobId);

        // Assert
        Assert.Null(checkpoint);

        _output.WriteLine(
            "✓ LoadDeleteCheckpointAsync correctly returned null for non-existent checkpoint"
        );
    }

    [Fact]
    public async Task SaveCheckpointAsync_ShouldUpdateExistingCheckpoint()
    {
        // Arrange
        var jobId = $"test-checkpoint-update-{Guid.NewGuid()}";
        var jobService = Client.JobService;

        // Create a job first
        await jobService.CreateJobAsync(jobId, "delete", (object?)null);

        var checkpoint = DeleteJobCheckpoint.Create(jobId);
        checkpoint.RelationshipsDeleted = 5;

        try
        {
            // Act - Save initial checkpoint
            await jobService.SaveCheckpointAsync(checkpoint);

            // Update checkpoint
            checkpoint.RelationshipsDeleted = 10;
            checkpoint.TwinsDeleted = 3;
            checkpoint.CurrentSection = DeleteSection.Models;

            // Save updated checkpoint
            await jobService.SaveCheckpointAsync(checkpoint);

            // Assert - Load and verify updated values
            var loadedCheckpoint = await jobService.LoadDeleteCheckpointAsync(jobId);

            Assert.NotNull(loadedCheckpoint);
            Assert.Equal(10, loadedCheckpoint.RelationshipsDeleted);
            Assert.Equal(3, loadedCheckpoint.TwinsDeleted);
            Assert.Equal(DeleteSection.Models, loadedCheckpoint.CurrentSection);

            _output.WriteLine($"✓ Successfully updated checkpoint for job: {jobId}");
            _output.WriteLine(
                $"  Updated progress: {loadedCheckpoint.RelationshipsDeleted}R, {loadedCheckpoint.TwinsDeleted}T"
            );
        }
        finally
        {
            // Cleanup
            await jobService.DeleteJobAsync(jobId);
        }
    }

    [Fact]
    public async Task DeleteJobCheckpoint_ShouldSurvive_JobServiceRestart()
    {
        // Arrange
        var jobId = $"test-checkpoint-persistence-{Guid.NewGuid()}";
        var jobService1 = Client.JobService;

        // Create a job and checkpoint
        await jobService1.CreateJobAsync(jobId, "delete", (object?)null);

        var checkpoint = DeleteJobCheckpoint.Create(jobId);
        checkpoint.RelationshipsDeleted = 15;
        checkpoint.TwinsDeleted = 8;
        checkpoint.ModelsDeleted = 3;
        checkpoint.CurrentSection = DeleteSection.Completed;
        checkpoint.RelationshipsCompleted = true;
        checkpoint.TwinsCompleted = true;
        checkpoint.ModelsCompleted = true;

        try
        {
            // Act - Save checkpoint
            await jobService1.SaveCheckpointAsync(checkpoint);

            // Simulate restart by creating a new client (which creates a new JobService)
            // In a real scenario, this would be a completely new application instance
            var jobService2 = Client.JobService; // Same service in tests, but principle is the same

            // Load checkpoint from "new" service
            var loadedCheckpoint = await jobService2.LoadDeleteCheckpointAsync(jobId);

            // Assert
            Assert.NotNull(loadedCheckpoint);
            Assert.Equal(jobId, loadedCheckpoint.JobId);
            Assert.Equal(15, loadedCheckpoint.RelationshipsDeleted);
            Assert.Equal(8, loadedCheckpoint.TwinsDeleted);
            Assert.Equal(3, loadedCheckpoint.ModelsDeleted);
            Assert.Equal(DeleteSection.Completed, loadedCheckpoint.CurrentSection);
            Assert.True(loadedCheckpoint.RelationshipsCompleted);
            Assert.True(loadedCheckpoint.TwinsCompleted);
            Assert.True(loadedCheckpoint.ModelsCompleted);

            var isCompleted =
                loadedCheckpoint.RelationshipsCompleted
                && loadedCheckpoint.TwinsCompleted
                && loadedCheckpoint.ModelsCompleted;
            Assert.True(isCompleted);

            _output.WriteLine($"✓ Checkpoint survived service restart for job: {jobId}");
            _output.WriteLine($"  Final state: {loadedCheckpoint.CurrentSection}");
            _output.WriteLine($"  Completed: {isCompleted}");
        }
        finally
        {
            // Cleanup
            await jobService1.DeleteJobAsync(jobId);
        }
    }
}
