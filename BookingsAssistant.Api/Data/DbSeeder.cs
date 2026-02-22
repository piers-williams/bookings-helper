using BookingsAssistant.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookingsAssistant.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        if (await context.ApplicationUsers.AnyAsync())
            return;

        context.ApplicationUsers.Add(new ApplicationUser
        {
            Id = 1,
            Name = "Admin"
        });

        await context.SaveChangesAsync();
    }
}
