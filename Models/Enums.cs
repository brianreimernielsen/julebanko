namespace Julebanko.Models;

public enum WinType
{
    OneRow = 1,
    TwoRows = 2,
    FullBoard = 3
}

public enum ClaimStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

public enum AdminRole
{
    BankoAdmin = 1,
    SetupAdmin = 2
}

public enum MediaSlot
{
    PlayerHero = 1,
    PlayerBackground = 2,
    PlayerDecoration = 3,
    PlayerHeroLogo = 4,
    /// <summary>Legacy full-page PDF background (no longer drawn; upload UI de-emphasized).</summary>
    BingoPdfBackground = 5,
    /// <summary>Christmas garland left of the PDF document title.</summary>
    BingoHeaderIconLeft = 6,
    /// <summary>Christmas garland right of the PDF document title.</summary>
    BingoHeaderIconRight = 7
}

/// <summary>
/// Result of checking a claim's plate series against drawn numbers.
/// </summary>
public enum ClaimValidationStatus
{
    /// <summary>Auto-validate off or not yet checked.</summary>
    NotChecked = 0,
    /// <summary>Plate found and claimed win type matches drawn numbers.</summary>
    Valid = 1,
    /// <summary>Plate found but drawn numbers do not support the claimed win type.</summary>
    Mismatch = 2,
    /// <summary>No plate with that series in the active season.</summary>
    CardNotFound = 3
}
