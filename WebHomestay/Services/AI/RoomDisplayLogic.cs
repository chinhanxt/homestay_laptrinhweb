using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models.Configuration;
using WebHomestay.Models.DTOs.AI;
using WebHomestay.Models.Entities.AI;

namespace WebHomestay.Services.AI;

public class RoomDisplayLogic : IRoomDisplayLogic
{
    private readonly ApplicationDbContext _context;

    public RoomDisplayLogic(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> CanShowRoomsAsync(AIBookingSessionState sessionState, CancellationToken cancellationToken = default)
    {
        var settings = await GetAISettingsAsync(cancellationToken);
        
        var maxShows = ParseInt(settings, "AIPublicBookingMaxRoomShows", 3);
        var cooldownMins = ParseInt(settings, "AIPublicBookingRoomCooldown", 5);

        if (sessionState.RoomDisplayCount >= maxShows)
        {
            if (!sessionState.LastRoomDisplayTime.HasValue) return true;
            
            var timeSinceLastShow = DateTime.UtcNow - sessionState.LastRoomDisplayTime.Value;
            if (timeSinceLastShow.TotalMinutes < cooldownMins)
            {
                return false;
            }
            
            // Cooldown expired, reset count
            sessionState.RoomDisplayCount = 0;
        }

        return true;
    }

    public async Task<List<object>> GenerateRoomUiBlocksAsync(
        List<ScoredRoom> recommendedRooms,
        AIBookingSessionState sessionState,
        CancellationToken cancellationToken = default)
    {
        var blocks = new List<object>();

        if (recommendedRooms == null || !recommendedRooms.Any())
            return blocks;

        // Record the show event
        sessionState.RoomDisplayCount++;
        sessionState.LastRoomDisplayTime = DateTime.UtcNow;

        var roomCards = recommendedRooms.Select(sr => new
        {
            Type = "roomCard",
            RoomId = sr.Room.Id,
            Name = sr.Room.Name,
            PricePerHour = sr.Room.PricePerHour,
            PricePerDay = sr.Room.PricePerDay,
            Capacity = sr.Room.Capacity,
            MaxGuests = sr.Room.MaxGuests,
            ImageUrl = sr.Room.ImageUrl,
            MatchScore = sr.RelevanceScore,
            Explanation = sr.ExplanationForUser
        }).ToList<object>();

        blocks.AddRange(roomCards);

        return blocks;
    }

    public Task<List<object>> GenerateAlternativeSuggestionsAsync(
        RecommendationCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var suggestions = new List<object>();

        // For example, if budget was tight, suggest looking at lower capacity rooms or hourly booking instead of daily
        suggestions.Add(new
        {
            Type = "suggestion",
            Text = "Thử xem các phòng nhỏ hơn hoặc thay đổi thời gian thuê (theo giờ thay vì qua đêm) để có giá tốt hơn nhé."
        });

        return Task.FromResult(suggestions);
    }

    private async Task<Dictionary<string, string>> GetAISettingsAsync(CancellationToken cancellationToken)
    {
        return await _context.SystemSettings
            .Where(s => s.GroupName == "AI")
            .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue, cancellationToken);
    }

    private int ParseInt(Dictionary<string, string> settings, string key, int defaultValue)
    {
        if (settings.TryGetValue(key, out var valStr) && int.TryParse(valStr, out var val))
        {
            return val;
        }
        return defaultValue;
    }
}
