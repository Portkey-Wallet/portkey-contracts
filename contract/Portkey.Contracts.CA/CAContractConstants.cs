namespace Portkey.Contracts.CA;

public static class CAContractConstants
{
    public const int LoginGuardianIsOccupiedByOthers = 0;

    // >1 fine, == 0 , conflict.
    public const int LoginGuardianIsNotOccupied = 1;
    public const int LoginGuardianIsYours = 2;

    public const string GuardianApprovedCount = "guardianApprovedCount";
    public const string GuardianCount = "guardianCount";

    public const long TenThousand = 10000;

    public const string ELFTokenSymbol = "ELF";
    public const long CADelegationAmount = 10000000000000000;

    public const long DefaultSecondaryDelegationFee = 100_00000000;

    public const int ManagerMaxCount = 70;
    
    public const long TokenDefaultTransferLimitAmount = 100000000000000000;
}