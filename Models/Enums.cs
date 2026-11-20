namespace BloodConnect.Api.Models;

public enum BloodType
{
    APositive,
    ANegative,
    BPositive,
    BNegative,
    ABPositive,
    ABNegative,
    OPositive,
    ONegative
}

public enum UserRole
{
    User,
    Admin
}

public enum Urgency
{
    Normal,
    Urgent,
    Critical
}

public enum RequestStatus
{
    Open,
    Fulfilled,
    Cancelled,
    Expired
}

public enum PledgeStatus
{
    Pledged,
    Donated,
    Cancelled
}
