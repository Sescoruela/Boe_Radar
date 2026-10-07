using BoeRadar.Application;
using BoeRadar.Domain;
using BoeRadar.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;

namespace BoeRadar.UnitTests;

public sealed class SubscriptionTests
{
    [Fact]
    public async Task RemoteSmtpCannotSendWithoutTls()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Email:Host"] = "smtp.example.invalid", ["Email:From"] = "radar@example.invalid" }).Build();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new SmtpEmailSender(config)
            .SendAsync("test@example.invalid", "Test", "Test", default));
    }

    [Theory]
    [InlineData(0, 30)]
    [InlineData(1025, 0)]
    [InlineData(1025, 121)]
    public async Task InvalidSmtpPortOrTimeoutFailsBeforeOpeningAConnection(int port, int timeout)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Email:Host"] = "localhost",
            ["Email:From"] = "radar@example.invalid",
            ["Email:Port"] = port.ToString(),
            ["Email:TimeoutSeconds"] = timeout.ToString()
        }).Build();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new SmtpEmailSender(config)
            .SendAsync("test@example.invalid", "Test", "Test", default));
    }

    [Fact]
    public void AlertProfileIsOptionalAndInvalidProfileIsRejected()
    {
        Assert.Null(SubscriptionRules.Normalize(new([], [])).Profile);
        var profile = new BusinessProfile("sme", "retail", "baleares");
        Assert.Equal(profile, SubscriptionRules.Normalize(new([], [], Profile: profile)).Profile);
        Assert.Throws<ArgumentException>(() => SubscriptionRules.Normalize(
            new([], [], Profile: profile with { Territory = "inventado" })));
    }

    [Fact]
    public void AlertProfileAddsExplainablePriorityWithoutBecomingAnEligibilityFilter()
    {
        var preferences = new SubscriptionPreferences([], [], Profile: new("sme", "retail", "baleares"));
        var item = new DigestItem(Guid.NewGuid(), Guid.NewGuid(), "BOE-A-2026-1", "Ayudas al comercio para pymes",
            "Resumen", RadarCategory.Grant, "gemini", null);
        var personalized = SubscriptionRules.Personalize(preferences, item);
        Assert.True(personalized.ProfileMatch!.Priority > 0);
        Assert.NotEmpty(personalized.ProfileMatch.Checks);
        var unknown = SubscriptionRules.Personalize(preferences, item with { Title = "Cambios generales" });
        Assert.Equal(0, unknown.ProfileMatch!.Priority);
        Assert.NotEmpty(SubscriptionRules.Match(preferences, unknown));
        Assert.Null(SubscriptionRules.Personalize(new([], []), personalized).ProfileMatch);
    }

    [Fact]
    public void RemovingOrUnsubscribingClearsTheStoredAlertProfile()
    {
        var now = DateTimeOffset.UtcNow;
        var subscription = Subscription.Create("a@example.invalid", "verify", now.AddHours(24), "[]", "[]", now, "{}");
        subscription.Activate("manage", now);
        subscription.UpdatePreferences("[]", "[]", 8, now);
        Assert.Null(subscription.BusinessProfileJson);
        subscription.UpdatePreferences("[]", "[]", 8, now, "{}");
        subscription.Unsubscribe(now);
        Assert.Null(subscription.BusinessProfileJson);
    }

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
