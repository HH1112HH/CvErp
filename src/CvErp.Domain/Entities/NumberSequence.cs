namespace CvErp.Domain.Entities;

public class NumberSequence
{
    public int Id { get; set; }
    public string Prefix { get; set; } = string.Empty;
    public int Year { get; set; }
    public int LastNumber { get; set; }
}
