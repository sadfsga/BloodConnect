using BloodConnect.Api.Models;

namespace BloodConnect.Api.Services;

// Who can give blood to whom (red blood cell donation rules).
public static class BloodCompatibility
{
    private static readonly Dictionary<BloodType, BloodType[]> CanGiveTo = new()
    {
        [BloodType.ONegative] = new[]
        {
            BloodType.ONegative, BloodType.OPositive, BloodType.ANegative, BloodType.APositive,
            BloodType.BNegative, BloodType.BPositive, BloodType.ABNegative, BloodType.ABPositive
        },
        [BloodType.OPositive] = new[]
        {
            BloodType.OPositive, BloodType.APositive, BloodType.BPositive, BloodType.ABPositive
        },
        [BloodType.ANegative] = new[]
        {
            BloodType.ANegative, BloodType.APositive, BloodType.ABNegative, BloodType.ABPositive
        },
        [BloodType.APositive] = new[]
        {
            BloodType.APositive, BloodType.ABPositive
        },
        [BloodType.BNegative] = new[]
        {
            BloodType.BNegative, BloodType.BPositive, BloodType.ABNegative, BloodType.ABPositive
        },
        [BloodType.BPositive] = new[]
        {
            BloodType.BPositive, BloodType.ABPositive
        },
        [BloodType.ABNegative] = new[]
        {
            BloodType.ABNegative, BloodType.ABPositive
        },
        [BloodType.ABPositive] = new[]
        {
            BloodType.ABPositive
        }
    };

    public static bool CanDonate(BloodType donor, BloodType recipient)
    {
        return CanGiveTo[donor].Contains(recipient);
    }

    // Blood types this donor can give to.
    public static IEnumerable<BloodType> RecipientsFor(BloodType donor)
    {
        return CanGiveTo[donor];
    }

    // Blood types that can give to this patient.
    public static IEnumerable<BloodType> DonorsFor(BloodType recipient)
    {
        return CanGiveTo.Where(kv => kv.Value.Contains(recipient)).Select(kv => kv.Key);
    }

    public static string Label(BloodType t)
    {
        return t switch
        {
            BloodType.APositive => "A+",
            BloodType.ANegative => "A-",
            BloodType.BPositive => "B+",
            BloodType.BNegative => "B-",
            BloodType.ABPositive => "AB+",
            BloodType.ABNegative => "AB-",
            BloodType.OPositive => "O+",
            BloodType.ONegative => "O-",
            _ => t.ToString()
        };
    }
}
