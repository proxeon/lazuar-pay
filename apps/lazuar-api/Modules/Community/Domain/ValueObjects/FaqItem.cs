using BuildingBlocks.Domain;

namespace Modules.Community.Domain.ValueObjects;

public class FaqItem : ValueObject
{
    public string Id { get; }
    public string Question { get; }
    public string Answer { get; }

    public FaqItem(string id, string question, string answer)
    {
        if (string.IsNullOrWhiteSpace(question)) throw new ArgumentException("Question cannot be empty.", nameof(question));
        if (string.IsNullOrWhiteSpace(answer)) throw new ArgumentException("Answer cannot be empty.", nameof(answer));

        Id = string.IsNullOrWhiteSpace(id) ? Guid.CreateVersion7().ToString() : id;
        Question = question;
        Answer = answer;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Id;
        yield return Question;
        yield return Answer;
    }
}
