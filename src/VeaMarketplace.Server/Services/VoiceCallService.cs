using VeaMarketplace.Server.Data;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Server.Services;

public class VoiceCallService
{
    private readonly DatabaseService _db;
    private readonly FriendService _friendService;

    public VoiceCallService(DatabaseService db, FriendService friendService)
    {
        _db = db;
        _friendService = friendService;
    }

    public (bool Success, string Message, VoiceCall? Call) StartCall(string callerId, string recipientId)
    {
        var caller = _db.Users.FindById(callerId);
        var recipient = _db.Users.FindById(recipientId);

        if (caller == null || recipient == null)
            return (false, "User not found", null);

        // Check if they are friends
        if (!_friendService.AreFriends(callerId, recipientId))
            return (false, "You can only call friends", null);

        // Check if there's an active call
        var activeCall = _db.VoiceCalls.FindOne(c =>
            (c.CallerId == callerId || c.RecipientId == callerId) &&
            (c.Status == VoiceCallStatus.Ringing || c.Status == VoiceCallStatus.InProgress));

        if (activeCall != null)
            return (false, "You already have an active call", null);

        var call = new VoiceCall
        {
            CallerId = callerId,
            CallerUsername = caller.Username,
            CallerAvatarUrl = caller.AvatarUrl,
            RecipientId = recipientId,
            RecipientUsername = recipient.Username,
            RecipientAvatarUrl = recipient.AvatarUrl,
            Status = VoiceCallStatus.Ringing,
            StartedAt = DateTime.UtcNow
        };

        _db.VoiceCalls.Insert(call);
        return (true, "Call started", call);
    }

    public (bool Success, string Message, VoiceCall? Call) AnswerCall(string callId, string userId, bool accept)
    {
        var call = _db.VoiceCalls.FindById(callId);
        if (call == null)
            return (false, "Call not found", null);

        if (call.RecipientId != userId)
            return (false, "Not authorized", null);

        if (call.Status != VoiceCallStatus.Ringing)
            return (false, "Call is no longer ringing", null);

        if (accept)
        {
            call.Status = VoiceCallStatus.InProgress;
            call.AnsweredAt = DateTime.UtcNow;
        }
        else
        {
            call.Status = VoiceCallStatus.Declined;
            call.EndedAt = DateTime.UtcNow;
        }

        _db.VoiceCalls.Update(call);
        return (true, accept ? "Call answered" : "Call declined", call);
    }

    public (bool Success, string Message, VoiceCall? Call) EndCall(string callId, string userId)
    {
        var call = _db.VoiceCalls.FindById(callId);
        if (call == null)
            return (false, "Call not found", null);

        if (call.CallerId != userId && call.RecipientId != userId)
            return (false, "Not authorized", null);

        if (call.Status == VoiceCallStatus.Ended)
            return (false, "Call already ended", null);

        if (call.Status == VoiceCallStatus.Ringing)
        {
            call.Status = userId == call.CallerId ? VoiceCallStatus.Ended : VoiceCallStatus.Missed;
        }
        else
        {
            call.Status = VoiceCallStatus.Ended;
        }

        call.EndedAt = DateTime.UtcNow;
        _db.VoiceCalls.Update(call);
        return (true, "Call ended", call);
    }

    public VoiceCall? GetActiveCall(string userId)
    {
        return _db.VoiceCalls.FindOne(c =>
            (c.CallerId == userId || c.RecipientId == userId) &&
            (c.Status == VoiceCallStatus.Ringing || c.Status == VoiceCallStatus.InProgress));
    }

    public List<VoiceCallDto> GetCallHistory(string userId, int limit = 20)
    {
        return _db.VoiceCalls
            .Query()
            .Where(c => c.CallerId == userId || c.RecipientId == userId)
            .OrderByDescending(c => c.StartedAt)
            .Limit(limit)
            .ToList()
            .Select(MapToDto)
            .ToList();
    }

    private static VoiceCallDto MapToDto(VoiceCall call)
    {
        return new VoiceCallDto
        {
            Id = call.Id,
            CallerId = call.CallerId,
            CallerUsername = call.CallerUsername,
            CallerAvatarUrl = call.CallerAvatarUrl,
            RecipientId = call.RecipientId,
            RecipientUsername = call.RecipientUsername,
            RecipientAvatarUrl = call.RecipientAvatarUrl,
            Status = call.Status,
            StartedAt = call.StartedAt,
            AnsweredAt = call.AnsweredAt,
            Duration = call.Duration
        };
    }
}
