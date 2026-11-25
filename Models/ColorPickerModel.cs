namespace Julebanko.Models;

/// <summary>View model for the shared admin color picker partial.</summary>
public class ColorPickerModel
{
    public required string Name { get; init; }
    public string? Value { get; init; }
    public required string Label { get; init; }
    public string? Hint { get; init; }
    public bool ShowRgb { get; init; } = true;
    public bool Required { get; init; } = true;
}
