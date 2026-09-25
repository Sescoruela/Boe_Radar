using BoeRadar.Application;
using BoeRadar.Domain;

namespace BoeRadar.UnitTests;

public sealed class SubscriptionTests
{
    [Fact]
    public void Normalization_RejectsDisplayNamesAndOutOfRangeHours()
    {
        Assert.Throws<ArgumentException>(() => SubscriptionRules.NormalizeEmail("Persona <a@example.com>"));
        Assert.Throws<ArgumentException>(() => SubscriptionRules.Normalize(
            new SubscriptionPreferences([], [], 24)));
    }

    [Fact]
    public void Matching_RequiresCategoryAndAtLeastOneKeywordWhenBothConfigured()
    {
        var preferences = SubscriptionRules.Normalize(new SubscriptionPreferences(
            [RadarCategory.Grant], ["digitalización", "autónomos"]));
        var matching = new DigestItem(Guid.NewGuid(), Guid.NewGuid(), "BOE-A-1",
            "Ayudas a la digitalización", "Para pymes", RadarCategory.Grant, "gemini", null);
        Assert.Contains("keyword:digitalización", SubscriptionRules.Match(preferences, matching));
        Assert.Empty(SubscriptionRules.Match(preferences, matching with { Category = RadarCategory.Tax }));
        Assert.Empty(SubscriptionRules.Match(preferences, matching with { Title = "Otras ayudas" }));
    }

    [Fact]
    public void Unsubscribe_InvalidatesManagementTokenAndAllowsNewVerification()
    {
        var now = DateTimeOffset.UtcNow;
        var subscription = Subscription.Create("a@example.com", "verify", now.AddHours(24),
            "[]", "[]", now);
        subscription.Activate("manage", now.AddMinutes(1));
        subscription.Unsubscribe(now.AddMinutes(2));
        Assert.Equal(SubscriptionStatus.Unsubscribed, subscription.Status);
        Assert.Empty(subscription.ManagementTokenHash);
        subscription.RenewVerification("verify2", now.AddHours(25), "[]", "[]", now.AddMinutes(3));
        Assert.Equal(SubscriptionStatus.Pending, subscription.Status);
    }

    [Fact]
    public void Outbox_TracksAttemptsAndErasesMessageBodyAfterSend()
    {
        var now = DateTimeOffset.UtcNow;
        var message = OutboxMessage.Create("digest", "key", "a@example.com", "subject", "secret", null, now);
        message.Claim(now);
        message.MarkFailed(now, "smtp-error");
        Assert.Equal(DeliveryStatus.Failed, message.Status);
        Assert.True(message.NextAttemptAt > now);
        message.Claim(message.NextAttemptAt);
        message.MarkSent(message.NextAttemptAt);
        Assert.Equal(2, message.AttemptCount);
        Assert.Empty(message.Body);
    }
}
