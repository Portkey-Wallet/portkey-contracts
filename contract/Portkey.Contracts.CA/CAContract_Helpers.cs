using System;
using System.Collections.Generic;
using System.Linq;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core.Extension;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace Portkey.Contracts.CA;

public partial class CAContract
{
    public override BoolValue VerifySignature(VerifySignatureRequest request)
    {
        Assert(request != null, "Invalid VerifySignature request.");
        Assert(request?.GuardianApproved != null, "invalid input guardian");
        Assert(string.IsNullOrWhiteSpace(request?.MethodName), "invalid input method name");
        Assert(request?.CaHash != null, "invalid input caHash");
        Assert(string.IsNullOrWhiteSpace(request?.OperationDetails), "invalid input operation details");
        return new BoolValue
        {
            Value = CheckVerifierSignatureAndData(request.GuardianApproved, request.MethodName, request.CaHash, request.OperationDetails, true)
        };
    }
    
    private bool CheckVerifierSignatureAndData(GuardianInfo guardianInfo, string methodName, Hash caHash = null,
        string operationDetails = null, bool preValidation = false)
    {
        var verifierDocLength = GetVerificationDocLength(guardianInfo.VerificationInfo.VerificationDoc);

        if (verifierDocLength != 7 && verifierDocLength != 8)
        {
            return false;
        }

        return CheckVerifierSignatureAndDataWithCreateChainId(guardianInfo, methodName, caHash, operationDetails);
    }

    private bool CheckVerifierSignatureAndDataWithCreateChainId(GuardianInfo guardianInfo, string methodName,
        Hash caHash, string operationDetails = null, bool preValidation = false)
    {
        //[type,guardianIdentifierHash,verificationTime,verifierAddress,salt,operationType,createChainId<,operationHash>]
        var verificationDoc = guardianInfo.VerificationInfo.VerificationDoc;
        if (verificationDoc == null || string.IsNullOrWhiteSpace(verificationDoc))
        {
            return false;
        }

        var verifierDoc = verificationDoc.Split(",");

        var docInfo = GetVerificationDoc(verificationDoc);

        if (docInfo.OperationType == "0")
        {
            return false;
        }

        var key = HashHelper.ComputeFrom(guardianInfo.VerificationInfo.Signature.ToByteArray());
        if (State.VerifierDocMap[key])
        {
            return false;
        }

        //Check expired time 1h.
        var verificationTime = DateTime.SpecifyKind(Convert.ToDateTime(docInfo.VerificationTime), DateTimeKind.Utc);
        if (verificationTime.ToTimestamp().AddHours(1) <= Context.CurrentBlockTime ||
            !int.TryParse(docInfo.GuardianType, out var type) ||
            (int)guardianInfo.Type != type ||
            guardianInfo.IdentifierHash != docInfo.IdentifierHash)
        {
            return false;
        }

        var operationTypeStr = docInfo.OperationType;
        var operationTypeName = typeof(OperationType).GetEnumName(Convert.ToInt32(operationTypeStr))?.ToLower();
        if (operationTypeName != methodName)
        {
            return false;
        }

        //Check verifier address and data.
        var verifierAddress = docInfo.VerifierAddress;
        var verificationInfo = guardianInfo.VerificationInfo;
        var verifierServer =
            State.VerifiersServerList.Value.VerifierServers.FirstOrDefault(v => v.Id == verificationInfo.Id);
        if (verifierServer == null || !verifierServer.VerifierAddresses.Contains(verifierAddress))
        {
            return false;
        }

        var verifierAddressFromPublicKey =
            RecoverVerifierAddress(verificationInfo.VerificationDoc, verificationInfo.Signature);
        if (verifierAddressFromPublicKey != verifierAddress)
        {
            verifierAddressFromPublicKey =
                RecoverVerifierAddress($"{verificationInfo.VerificationDoc},{operationDetails}",
                    verificationInfo.Signature);
        }
        if (verifierAddressFromPublicKey != verifierAddress)
        {
            return false;
        }

        if (!CheckVerifierSignatureOperationDetail(operationTypeName, verifierDoc, operationDetails))
        {
            return false;
        }

        if (!preValidation)
        {
            State.VerifierDocMap[key] = true;
        }

        if (operationTypeName == nameof(OperationType.AddGuardian).ToLower() &&
            !CheckOnCreateChain(State.HolderInfoMap[caHash]))
        {
            return true;
        }

        //After verifying the contents of the operation,it is not necessary to verify the 'ChainId'
        if (verifierDoc.Length >= 8 &&
            ((operationTypeName == nameof(OperationType.CreateCaholder).ToLower() && caHash != null) ||
             operationTypeName == nameof(OperationType.SocialRecovery).ToLower()))
        {
            return true;
        }

        return int.Parse(verifierDoc[6]) == Context.ChainId;
    }

    private bool CheckOnCreateChain(HolderInfo holderInfo)
    {
        if (holderInfo.GuardianList == null || holderInfo.GuardianList.Guardians == null ||
            holderInfo.GuardianList.Guardians.Count == 0)
        {
            return false;
        }

        return holderInfo.CreateChainId == Context.ChainId || holderInfo.CreateChainId == 0;
    }

    private bool IsGuardianExist(Hash caHash, GuardianInfo guardianInfo)
    {
        var satisfiedGuardians = State.HolderInfoMap[caHash].GuardianList.Guardians.FirstOrDefault(
            g => g.IdentifierHash == guardianInfo.IdentifierHash && g.Type == guardianInfo.Type &&
                 g.VerifierId == guardianInfo.VerificationInfo.Id
        );
        return satisfiedGuardians != null;
    }

    private bool IsValidHash(Hash hash)
    {
        return hash != null && !hash.Value.IsEmpty;
    }

    private class VerificationDocInfo
    {
        public string GuardianType { get; set; }
        public Hash IdentifierHash { get; set; }
        public string VerificationTime { get; set; }
        public Address VerifierAddress { get; set; }
        public string Salt { get; set; }
        public string OperationType { get; set; }
    }

    private VerificationDocInfo GetVerificationDoc(string doc)
    {
        var docs = doc.Split(",");
        return new VerificationDocInfo
        {
            GuardianType = docs[0],
            IdentifierHash = Hash.LoadFromHex(docs[1]),
            VerificationTime = docs[2],
            VerifierAddress = Address.FromBase58(docs[3]),
            Salt = docs[4],
            OperationType = docs[5]
        };
    }

    private HolderInfo GetHolderInfoByCaHash(Hash caHash)
    {
        var holderInfo = State.HolderInfoMap[caHash];
        Assert(holderInfo != null, $"CA holder is null.CA hash:{caHash}");
        Assert(holderInfo!.GuardianList != null, $"No guardians found in this holder by caHash: {caHash}");

        return holderInfo;
    }

    private string GetSaltFromVerificationDoc(string verificationDoc)
    {
        return verificationDoc.Split(",")[4];
    }

    private static bool IsOverDay(Timestamp lastDayTime, Timestamp nowDayTime)
    {
        return lastDayTime == null || DateTime.Parse(lastDayTime.ToDateTime().ToString("yyyy-MM-dd")) <
            DateTime.Parse(nowDayTime.ToDateTime().ToString("yyyy-MM-dd"));
    }

    private TokenInfo GetTokenInfo(string symbol)
    {
        return State.TokenContract.GetTokenInfo.Call(new GetTokenInfoInput
        {
            Symbol = symbol
        });
    }

    private GetBalanceOutput GetTokenBalance(string symbol, Address cAddress)
    {
        return State.TokenContract.GetBalance.Call(new GetBalanceInput
        {
            Symbol = symbol,
            Owner = cAddress
        });
    }

    private void SetProjectDelegator(Address delegatorAddress)
    {
        if (!IsValidHash(State.CaProjectDelegateHash.Value))
        {
            return;
        }

        var projectDelegateInfo = State.ProjectDelegateInfo[State.CaProjectDelegateHash.Value];
        if (projectDelegateInfo == null || projectDelegateInfo.DelegateeHashList.Count == 0)
        {
            return;
        }

        State.ProjectDelegationFee.Value ??= new ProjectDelegationFee()
        {
            Amount = CAContractConstants.DefaultProjectDelegationFee
        };

        var delegations = new Dictionary<string, long>
        {
            [CAContractConstants.ELFTokenSymbol] = State.ProjectDelegationFee.Value.Amount
        };
        // Randomly select a delegatee based on the address
        var selectIndex = (int)Math.Abs(delegatorAddress.ToByteArray().ToInt64(true) %
                                        projectDelegateInfo.DelegateeHashList.Count);
        State.TokenContract.SetTransactionFeeDelegations.VirtualSend(projectDelegateInfo.DelegateeHashList[selectIndex],
            new SetTransactionFeeDelegationsInput
            {
                DelegatorAddress = delegatorAddress,
                Delegations =
                {
                    delegations
                }
            });
    }

    private bool IsValidInviteCode(string code)
    {
        return !string.IsNullOrWhiteSpace(code) && code.Length <= CAContractConstants.ReferralCodeLength;
    }

    private Address RecoverVerifierAddress(string verificationDoc, ByteString signature)
    {
        var data = HashHelper.ComputeFrom(verificationDoc);
        var publicKey = Context.RecoverPublicKey(signature.ToByteArray(),
            data.ToByteArray());
        return Address.FromPublicKey(publicKey);
    }

    private int GetVerificationDocLength(string verificationDoc)
    {
        return string.IsNullOrWhiteSpace(verificationDoc) ? 0 : verificationDoc.Split(",").Length;
    }

    /// <summary>
    /// State.CheckOperationDetailsInSignatureEnabled==true: must verify the Hash value of operationDetails
    /// verifierDoc.Length >= 8: only verify registration and social recovery
    /// </summary>
    /// <param name="operationTypeName"></param>
    /// <param name="verifierDoc"></param>
    /// <param name="operationDetails"></param>
    /// <returns></returns>
    private bool CheckVerifierSignatureOperationDetail(string operationTypeName, string[] verifierDoc,
        string operationDetails)
    {
        if (State.CheckOperationDetailsInSignatureEnabled.Value
            || verifierDoc.Length >= 8)
        {
            if (verifierDoc.Length < 8 || string.IsNullOrWhiteSpace(verifierDoc[7]) ||
                string.IsNullOrWhiteSpace(operationDetails))
            {
                return false;
            }

            return verifierDoc[7] == HashHelper.ComputeFrom(operationDetails).ToHex();
        }

        return true;
    }
}