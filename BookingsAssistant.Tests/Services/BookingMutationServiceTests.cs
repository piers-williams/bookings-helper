using BookingsAssistant.Api.Models;
using BookingsAssistant.Api.Services;
using BookingsAssistant.Tests.Fakes;
using Microsoft.Extensions.Logging;

namespace BookingsAssistant.Tests.Services;

/// <summary>
/// Unit tests for BookingMutationService — all run against FakeOsmService.
/// No WebApplicationFactory needed; the service is instantiated directly.
/// </summary>
public class BookingMutationServiceTests
{
    private static BookingMutationService CreateService(FakeOsmService fake)
    {
        var logger = new LoggerFactory().CreateLogger<BookingMutationService>();
        return new BookingMutationService(fake, logger);
    }

    private static BookingItemDto MakeItem(string itemId, string siteId = "site-1",
        string startTime = "09:00", string endTime = "17:00") => new()
    {
        ItemId = itemId,
        Type = "site",
        SiteId = siteId,
        Label = "Test Pitch",
        StartDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
        EndDate = new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc),
        StartTime = startTime,
        EndTime = endTime
    };

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task HappyPath_TwoReplacements_AllCreatedBeforeAnyDeleted_StatusCompleted()
    {
        var fake = new FakeOsmService
        {
            CreatedItemIds = new List<string> { "new-1", "new-2" },
            ItemsToReturn = new List<BookingItemDto> { MakeItem("new-1"), MakeItem("new-2") }
        };

        var svc = CreateService(fake);
        var replacements = new List<ItemReplacement>
        {
            new() { Original = MakeItem("orig-1") },
            new() { Original = MakeItem("orig-2") }
        };

        var result = await svc.ReplaceItemsAsync("booking-99", replacements);

        Assert.Equal(BookingActionStatus.Completed, result.Status);
        Assert.Equal(new[] { "new-1", "new-2" }, result.Created);
        Assert.Equal(new[] { "orig-1", "orig-2" }, result.Deleted);
        Assert.Equal(2, result.Items.Count);

        // Assert ordering: all creates happened before any deletes
        var createIndices = fake.CallLog.Select((entry, i) => (entry, i))
            .Where(x => x.entry.Op == "create")
            .Select(x => x.i)
            .ToList();
        var deleteIndices = fake.CallLog.Select((entry, i) => (entry, i))
            .Where(x => x.entry.Op == "delete")
            .Select(x => x.i)
            .ToList();

        Assert.NotEmpty(createIndices);
        Assert.NotEmpty(deleteIndices);
        Assert.True(createIndices.Max() < deleteIndices.Min(),
            "All creates must complete before any deletes begin");
    }

    [Fact]
    public async Task HappyPath_SingleReplacement_Works()
    {
        var fake = new FakeOsmService
        {
            CreatedItemIds = new List<string> { "new-single" },
            ItemsToReturn = new List<BookingItemDto> { MakeItem("new-single") }
        };

        var svc = CreateService(fake);
        var replacements = new List<ItemReplacement>
        {
            new() { Original = MakeItem("orig-single") }
        };

        var result = await svc.ReplaceItemsAsync("booking-100", replacements);

        Assert.Equal(BookingActionStatus.Completed, result.Status);
        Assert.Equal(new[] { "new-single" }, result.Created);
        Assert.Equal(new[] { "orig-single" }, result.Deleted);
    }

    // ── Rollback ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreatePhaseFails_RollsBackCreatedItems_NoOriginalsDeleted_StatusRolledBack()
    {
        var fake = new FakeOsmService
        {
            CreatedItemIds = new List<string> { "new-1" },
            ItemsToReturn = new List<BookingItemDto>()
        };
        // 2nd create call throws
        fake.FailCreateOnCall = (2, new InvalidOperationException("OSM create failed"));

        var svc = CreateService(fake);
        var replacements = new List<ItemReplacement>
        {
            new() { Original = MakeItem("orig-1") },
            new() { Original = MakeItem("orig-2") }
        };

        var result = await svc.ReplaceItemsAsync("booking-99", replacements);

        Assert.Equal(BookingActionStatus.RolledBack, result.Status);

        // The first create succeeded and must be rolled back
        Assert.Empty(result.Created);
        Assert.Empty(result.Deleted);
        Assert.Contains(("booking-99", "new-1"), fake.DeletedItems);

        // Original items must NOT have been deleted
        Assert.DoesNotContain("orig-1", fake.DeletedItems.Select(d => d.ItemId));
        Assert.DoesNotContain("orig-2", fake.DeletedItems.Select(d => d.ItemId));

        Assert.NotEmpty(result.Message);
    }

    // ── completed_with_warnings ───────────────────────────────────────────────

    [Fact]
    public async Task DeletePhasePartialFailure_StatusCompletedWithWarnings()
    {
        var fake = new FakeOsmService
        {
            CreatedItemIds = new List<string> { "new-1", "new-2" },
            ItemsToReturn = new List<BookingItemDto>()
        };
        // orig-2 delete returns false
        fake.DeleteReturnFalseForIds.Add("orig-2");

        var svc = CreateService(fake);
        var replacements = new List<ItemReplacement>
        {
            new() { Original = MakeItem("orig-1") },
            new() { Original = MakeItem("orig-2") }
        };

        var result = await svc.ReplaceItemsAsync("booking-99", replacements);

        Assert.Equal(BookingActionStatus.CompletedWithWarnings, result.Status);
        Assert.Equal(new[] { "new-1", "new-2" }, result.Created);

        // Only orig-1 succeeded
        Assert.Equal(new[] { "orig-1" }, result.Deleted);
    }

    // ── Override fidelity ─────────────────────────────────────────────────────

    [Fact]
    public async Task Override_NewSiteId_ReflectedInSpec_OtherFieldsUnchanged()
    {
        var fake = new FakeOsmService
        {
            ItemsToReturn = new List<BookingItemDto>()
        };

        var original = MakeItem("orig-1", siteId: "site-OLD", startTime: "08:00", endTime: "16:00");
        original.StartDate = new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc);
        original.EndDate = new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc);

        var svc = CreateService(fake);
        var replacements = new List<ItemReplacement>
        {
            new() { Original = original, NewSiteId = "site-NEW" }
        };

        await svc.ReplaceItemsAsync("booking-99", replacements);

        var spec = Assert.Single(fake.CapturedSpecs);
        Assert.Equal("site-NEW", spec.CampsiteItemId);     // overridden (site → new item-type id)
        Assert.Equal("08:00", spec.StartTime);             // unchanged
        Assert.Equal("16:00", spec.EndTime);               // unchanged
        Assert.Equal(original.StartDate, spec.StartDate);  // unchanged
        Assert.Equal(original.EndDate, spec.EndDate);      // unchanged
    }

    [Fact]
    public async Task Override_NewStartTime_ReflectedInSpec_OtherFieldsUnchanged()
    {
        var fake = new FakeOsmService
        {
            ItemsToReturn = new List<BookingItemDto>()
        };

        var original = MakeItem("orig-1", siteId: "site-42", startTime: "09:00", endTime: "17:00");

        var svc = CreateService(fake);
        var replacements = new List<ItemReplacement>
        {
            new() { Original = original, NewStartTime = "14:00", NewEndTime = "18:00" }
        };

        await svc.ReplaceItemsAsync("booking-99", replacements);

        var spec = Assert.Single(fake.CapturedSpecs);
        Assert.Equal("14:00", spec.StartTime);       // overridden
        Assert.Equal("18:00", spec.EndTime);         // overridden
        Assert.Equal("site-42", spec.CampsiteItemId); // unchanged
    }

    [Fact]
    public async Task NoOverrides_SpecMatchesOriginalFields()
    {
        var fake = new FakeOsmService
        {
            ItemsToReturn = new List<BookingItemDto>()
        };

        var original = MakeItem("orig-1", siteId: "site-77", startTime: "10:00", endTime: "15:00");
        original.Label = "Camping Pitch";
        original.Type = "site";
        original.ActivityId = null;
        original.NumberPeople = 12;

        var svc = CreateService(fake);
        var replacements = new List<ItemReplacement>
        {
            new() { Original = original }
            // No overrides
        };

        await svc.ReplaceItemsAsync("booking-99", replacements);

        var spec = Assert.Single(fake.CapturedSpecs);
        Assert.Equal("site-77", spec.CampsiteItemId);
        Assert.Equal("10:00", spec.StartTime);
        Assert.Equal("15:00", spec.EndTime);
        Assert.Equal(12, spec.NumberPeople);
    }
}
