using Microsoft.EntityFrameworkCore;
using TripPlanner.Api.Dtos;
using TripPlanner.Domain.Enums;
using TripPlanner.Infrastructure.Persistence;

namespace TripPlanner.Api.Services;

public static class TagHelper
{
    public static async Task<List<TagDto>> GetTagsForAsync(TripPlannerDbContext db, EntityType type, Guid entityId)
    {
        return await db.TaggedItems
            .Where(ti => ti.EntityType == type && ti.EntityId == entityId)
            .Select(ti => new TagDto(ti.Tag.Id, ti.Tag.Name, ti.Tag.Color))
            .ToListAsync();
    }

    public static async Task<Dictionary<Guid, List<TagDto>>> GetTagsForManyAsync(TripPlannerDbContext db, EntityType type, IEnumerable<Guid> entityIds)
    {
        var ids = entityIds.ToList();
        var rows = await db.TaggedItems
            .Where(ti => ti.EntityType == type && ids.Contains(ti.EntityId))
            .Select(ti => new { ti.EntityId, Tag = new TagDto(ti.Tag.Id, ti.Tag.Name, ti.Tag.Color) })
            .ToListAsync();
        return rows.GroupBy(r => r.EntityId).ToDictionary(g => g.Key, g => g.Select(x => x.Tag).ToList());
    }
}
