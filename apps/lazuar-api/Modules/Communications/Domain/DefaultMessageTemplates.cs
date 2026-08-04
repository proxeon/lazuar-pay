using System;
using System.Collections.Generic;
using System.Linq;
using Modules.Communications.Domain.Aggregates;

namespace Modules.Communications.Domain;

/// <summary>
/// Canonical default message template catalog. Used for entitlement seeding and template reset.
/// Only lifecycle / document / digital-delivery templates that have real event consumers.
/// </summary>
public static class DefaultMessageTemplates
{
    public sealed record Definition(
        string Name,
        string Channel,
        string Subject,
        string EmailBody,
        string WhatsAppBody,
        IReadOnlyList<string> RequiredVariables,
        IReadOnlyList<string> OptionalVariables);

    public static readonly IReadOnlyList<Definition> All =
    [
        new Definition(
            "Payment Failed",
            "ALL",
            "Action Needed: Payment issue for {{plan_name}}",
            "Hi {{customer_name}},\n\nWe tried to process your renewal for {{plan_name}}, but the payment didn't go through. This usually just means your bank blocked the transaction or the card expired.\n\nTo ensure you don't lose access, please update your payment details here:\n\n[Securely Update Payment]({{renewal_link}})\n\nIf you need any help, just reply to this email.\n\n— {{business_name}}",
            "Hi {{customer_name}} 👋 Quick heads up: your recent card payment for {{plan_name}} was declined by the bank. To keep your access active, you can quickly update your details here: {{renewal_link}}. Let us know if you need help!",
            ["{{renewal_link}}"],
            ["{{customer_name}}", "{{business_name}}", "{{plan_name}}"]),

        new Definition(
            "Subscription Cancelled",
            "ALL",
            "Your {{plan_name}} membership has ended",
            "Hi {{customer_name}},\n\nYour {{plan_name}} membership has been cancelled.\n\nWe hope to see you again! 🙏\n\n— {{business_name}}",
            "Hi {{customer_name}}, your {{plan_name}} membership has been cancelled. We hope to see you back soon! 🙏",
            Array.Empty<string>(),
            ["{{customer_name}}", "{{business_name}}", "{{plan_name}}"]),

        new Definition(
            "Digital Product Delivery",
            "ALL",
            "Your download is ready: {{plan_name}}",
            "Hi {{customer_name}},\n\nThank you for your purchase! You can access your file securely using the link below:\n\n[Download File]({{fulfillment_url}})\n\nYou can also find your purchases in your dashboard:\n[Access Portal]({{portal_magic_link}})\n\n— {{business_name}}",
            "Hi {{customer_name}}, thank you for purchasing {{plan_name}}! You can download your file here: {{fulfillment_url}}",
            ["{{fulfillment_url}}"],
            ["{{customer_name}}", "{{business_name}}", "{{plan_name}}", "{{portal_magic_link}}"]),

        new Definition(
            "Quotation Ready",
            "ALL",
            "Your quotation from {{business_name}} is ready",
            "Hi {{customer_name}},\n\nYour requested quotation from {{business_name}} has been generated. You can view and download the document using the secure link below:\n\n[Download Quotation]({{document_link}})\n\nIf you have any questions, please reply directly to this email.\n\n— {{business_name}}",
            "Hi {{customer_name}}, your quotation from {{business_name}} is ready. View and download it here: {{document_link}}",
            ["{{document_link}}"],
            ["{{customer_name}}", "{{business_name}}"]),

        new Definition(
            "Official Receipt",
            "ALL",
            "Your official receipt from {{business_name}}",
            "Hi {{customer_name}},\n\nThank you for your payment. Your official receipt and tax invoice (if applicable) have been generated. You can download the document securely using the link below:\n\n[Download Receipt]({{document_link}})\n\n— {{business_name}}",
            "Hi {{customer_name}}, thank you for your payment to {{business_name}}. You can download your official receipt here: {{document_link}}",
            ["{{document_link}}"],
            ["{{customer_name}}", "{{business_name}}"])
    ];

    /// <summary>Orphan templates previously seeded but never wired to fulfillment events.</summary>
    public static readonly IReadOnlyList<string> OrphanNames =
    [
        "Community Welcome",
        "Community Payment Success",
        "Event Ticket Confirmation",
        "Abandoned Cart (12h)",
        "Abandoned Cart (24h)",
        "Generic Receipt",
        "Subscription Renewal (3 Days)",
        "Subscription Renewal Due Today",
        "Subscription Renewal Overdue"
    ];

    public static Definition? GetByName(string name) =>
        All.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));

    public static bool IsCatalogTemplate(string name) => GetByName(name) != null;

    public static MessageTemplate CreateEntity(Guid organizationId, Definition definition) =>
        new(
            organizationId,
            definition.Name,
            definition.Channel,
            definition.Subject,
            definition.EmailBody,
            definition.WhatsAppBody,
            isDefault: true,
            definition.RequiredVariables,
            definition.OptionalVariables);

    public static IEnumerable<MessageTemplate> CreateAllForTenant(Guid organizationId) =>
        All.Select(d => CreateEntity(organizationId, d));
}
