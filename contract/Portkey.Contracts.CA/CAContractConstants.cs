namespace Portkey.Contracts.CA;

public static class CAContractConstants
{

    public const string GuardianApprovedCount = "guardianApprovedCount";
    public const string GuardianCount = "guardianCount";

    public const long TenThousand = 10000;

    public const string ELFTokenSymbol = "ELF";
    public const long CADelegationAmount = 10000000000000000;

    public const long DefaultProjectDelegationFee = 100_00000000;

    public const int ManagerMaxCount = 70;

    public const long TokenDefaultTransferLimitAmount = 100000000000000000;
    public const int DelegateeListMaxCount = 10;

    public const int MainChainId = 9992731;
    
    
}

enum LoginGuardianStatus
{
    IsOccupiedByOthers,
    IsNotOccupied,
    IsYours
}