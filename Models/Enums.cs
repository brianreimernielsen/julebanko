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
    PlayerHeroLogo = 4
}
