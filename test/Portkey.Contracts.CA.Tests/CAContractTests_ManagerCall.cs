using System.Threading.Tasks;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Kernel.FeeCalculation.Extensions;
using AElf.Types;
using Shouldly;
using Xunit;

namespace Portkey.Contracts.CA;

public partial class CAContractTests
{
    [Fact]
    public async Task ManagerFrowardCallTest()
    {
        var caHash = await CreateHolder();
        Address caAddress;
        {
            var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
            {
                CaHash = caHash
            });
            caAddress = holderInfo.CaAddress;
        }
        await TokenContractStub.Transfer.SendAsync(new TransferInput
        {
            Amount = 1_0000000_00000000,
            Symbol = "ELF",
            To = caAddress
        });
        var input = new ManagerForwardCallInput
        {
            CaHash = caHash,
            ContractAddress = TokenContractAddress,
            MethodName = nameof(TokenContractContainer.TokenContractStub.Transfer),
            Args = new TransferInput
            {
                To = User2Address,
                Symbol = "ELF",
                Amount = 1_00000000,
                Memo = "ca transfer."
            }.ToBytesValue().Value
        };
        await CaContractStubManagerInfo1.ManagerForwardCall.SendAsync(input);
        {
            var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = User2Address,
                Symbol = "ELF"
            });
            balance.Balance.ShouldBe(1_00000000);
        }
    }

    [Fact]
    public async Task ManagerFrowardCallTest_Failed_NoPermission()
    {
        var caHash = await CreateCAHolderNoPermission();
        var input = new ManagerForwardCallInput
        {
            CaHash = caHash,
            ContractAddress = TokenContractAddress,
            MethodName = nameof(TokenContractContainer.TokenContractStub.Transfer),
            Args = new TransferInput
            {
                To = User2Address,
                Symbol = "ELF",
                Amount = 1_00000000,
                Memo = "ca transfer."
            }.ToBytesValue().Value
        };
        var executionResult = await CaContractStub.ManagerForwardCall.SendWithExceptionAsync(input);
        executionResult.TransactionResult.Error.ShouldContain("No Permission.");
    }

    [Fact]
    public async Task ManagerFrowardCallTest_Failed_HolderNotExist()
    {
        await TokenContractStub.Transfer.SendAsync(new TransferInput
        {
            Amount = 1000000000000,
            Symbol = "ELF",
            To = User1Address
        });
        var input = new ManagerForwardCallInput
        {
            CaHash = HashHelper.ComputeFrom("AAA"),
            ContractAddress = TokenContractAddress,
            MethodName = nameof(TokenContractStub.Transfer),
            Args = new TransferInput
            {
                To = User2Address,
                Symbol = "ELF",
                Amount = 1_00000000,
                Memo = "ca transfer."
            }.ToBytesValue().Value
        };
        var executionResult = await CaContractStubManagerInfo1.ManagerForwardCall.SendWithExceptionAsync(input);
        executionResult.TransactionResult.Error.ShouldContain("CA holder is null.");
    }

    [Fact]
    public async Task ManagerFrowardCallTest_Failed_InvalidInput()
    {
        var caHash = await CreateHolder();
        {
            var input = new ManagerForwardCallInput
            {
                CaHash = caHash,
                ContractAddress = User3Address,
                MethodName = nameof(TokenContractContainer.TokenContractStub.Transfer),
                Args = new TransferInput
                {
                    To = User2Address,
                    Symbol = "ELF",
                    Amount = 1_00000000,
                    Memo = "ca transfer."
                }.ToBytesValue().Value
            };
            var executionResult = await CaContractStubManagerInfo1.ManagerForwardCall.SendWithExceptionAsync(input);
            executionResult.TransactionResult.Error.ShouldContain("Invalid contract address.");
        }
        {
            var input = new ManagerForwardCallInput
            {
                CaHash = caHash,
                MethodName = nameof(TokenContractContainer.TokenContractStub.Transfer),
                Args = new TransferInput
                {
                    To = User2Address,
                    Symbol = "ELF",
                    Amount = 1_00000000,
                    Memo = "ca transfer."
                }.ToBytesValue().Value
            };
            var executionResult = await CaContractStubManagerInfo1.ManagerForwardCall.SendWithExceptionAsync(input);
            executionResult.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var input = new ManagerForwardCallInput
            {
                ContractAddress = TokenContractAddress,
                MethodName = nameof(TokenContractContainer.TokenContractStub.Transfer),
                Args = new TransferInput
                {
                    To = User2Address,
                    Symbol = "ELF",
                    Amount = 1_00000000,
                    Memo = "ca transfer."
                }.ToBytesValue().Value
            };
            var executionResult = await CaContractStubManagerInfo1.ManagerForwardCall.SendWithExceptionAsync(input);
            executionResult.TransactionResult.Error.ShouldContain("CA hash is null.");
        }
        {
            var input = new ManagerForwardCallInput
            {
                CaHash = caHash,
                ContractAddress = TokenContractAddress,
                Args = new TransferInput
                {
                    To = User2Address,
                    Symbol = "ELF",
                    Amount = 1_00000000,
                    Memo = "ca transfer."
                }.ToBytesValue().Value
            };
            var executionResult = await CaContractStubManagerInfo1.ManagerForwardCall.SendWithExceptionAsync(input);
            executionResult.TransactionResult.Error.ShouldContain("Invalid input.");
        }
    }

    [Fact]
    public async Task ManagerTransferTest()
    {
        var caHash = await CreateHolder();
        Address caAddress;
        {
            var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
            {
                CaHash = caHash
            });
            caAddress = holderInfo.CaAddress;
        }
        await TokenContractStub.Transfer.SendAsync(new TransferInput
        {
            Amount = 1_0000000_00000000,
            Symbol = "ELF",
            To = caAddress
        });
        await CaContractStubManagerInfo1.ManagerTransfer.SendAsync(new ManagerTransferInput
        {
            CaHash = caHash,
            To = User2Address,
            Symbol = "ELF",
            Amount = 1_00000000,
            Memo = "ca transfer."
        });
        {
            var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = User2Address,
                Symbol = "ELF"
            });
            balance.Balance.ShouldBe(1_00000000);
        }
    }

    [Fact]
    public async Task ManagerTransferTest_Failed_NoPermission()
    {
        var caHash = await CreateCAHolderNoPermission();
        Address caAddress;
        {
            var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
            {
                CaHash = caHash
            });
            caAddress = holderInfo.CaAddress;
        }
        await TokenContractStub.Transfer.SendAsync(new TransferInput
        {
            Amount = 1_0000000_00000000,
            Symbol = "ELF",
            To = caAddress
        });
        var executionResult = await CaContractStub.ManagerTransfer.SendWithExceptionAsync(new ManagerTransferInput
        {
            CaHash = caHash,
            To = User2Address,
            Symbol = "ELF",
            Amount = 1_00000000,
            Memo = "ca transfer."
        });
        executionResult.TransactionResult.Error.ShouldContain("No permission.");
    }

    [Fact]
    public async Task ManagerTransferTest_Failed_InvalidInput()
    {
        var caHash = await CreateHolder();
        {
            var executionResult = await CaContractStubManagerInfo1.ManagerTransfer.SendWithExceptionAsync(
                new ManagerTransferInput
                {
                    To = User2Address,
                    Symbol = "ELF",
                    Amount = 1_00000000,
                    Memo = "ca transfer."
                });
            executionResult.TransactionResult.Error.ShouldContain("CA hash is null.");
        }
        {
            var executionResult = await CaContractStubManagerInfo1.ManagerTransfer.SendWithExceptionAsync(
                new ManagerTransferInput
                {
                    CaHash = caHash,
                    Amount = 1_00000000,
                    Memo = "ca transfer."
                });
            executionResult.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var executionResult = await CaContractStubManagerInfo1.ManagerTransfer.SendWithExceptionAsync(
                new ManagerTransferInput
                {
                    CaHash = caHash,
                    Symbol = "ELF",
                    Amount = 1_00000000,
                    Memo = "ca transfer."
                });
            executionResult.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var executionResult = await CaContractStubManagerInfo1.ManagerTransfer.SendWithExceptionAsync(
                new ManagerTransferInput
                {
                    CaHash = caHash,
                    To = User2Address,
                    Amount = 1_00000000,
                    Memo = "ca transfer."
                });
            executionResult.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var executionResult = await CaContractStubManagerInfo1.ManagerTransfer.SendWithExceptionAsync(
                new ManagerTransferInput
                {
                    CaHash = caHash,
                    To = User2Address,
                    Symbol = "ELF",
                    Amount = -1_00000000,
                    Memo = "ca transfer."
                });
            executionResult.TransactionResult.Error.ShouldContain("Invalid amount.");
        }
        {
            var executionResult = await CaContractStubManagerInfo1.ManagerTransfer.SendWithExceptionAsync(
                new ManagerTransferInput
                {
                    CaHash = HashHelper.ComputeFrom("11111"),
                    To = User2Address,
                    Symbol = "ELF",
                    Amount = 1_00000000,
                    Memo = "ca transfer."
                });
            executionResult.TransactionResult.Error.ShouldContain("CA holder is null.");
        }

        {
            var executionResult = await CaContractStubManagerInfo1.ManagerTransfer.SendWithExceptionAsync(
                new ManagerTransferInput
                {
                    CaHash = caHash,
                    To = User2Address,
                    Symbol = "",
                    Amount = 1_00000000,
                    Memo = "ca transfer."
                });
            executionResult.TransactionResult.Error.ShouldContain("Invalid input.");
        }
    }

    [Fact]
    public async Task ManagerTransferFromTest()
    {
        var caHash = await CreateHolder();
        Address caAddress;
        {
            var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
            {
                CaHash = caHash
            });
            caAddress = holderInfo.CaAddress;
        }
        var balance1 = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = DefaultAddress,
            Symbol = "ELF"
        });
        balance1.Balance.ShouldBeGreaterThan(0);
        var balanceOutput = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = DefaultAddress,
            Symbol = "ELF"
        });
        var result = await TokenContractStub.Transfer.SendAsync(new TransferInput
        {
            Amount = 1_0000000_00000000,
            Symbol = "ELF",
            To = caAddress
        });
        var currentBalance = balanceOutput.Balance - 1_0000000_00000000 -
                             result.TransactionResult.GetChargedTransactionFees()["ELF"];
        {
            var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = DefaultAddress,
                Symbol = "ELF"
            });
            balance.Balance.ShouldBe(currentBalance);
        }
        {
            var approveResult = await TokenContractStub.Approve.SendAsync(new ApproveInput
            {
                Spender = caAddress,
                Amount = 1_00000000_00000000,
                Symbol = "ELF"
            });
            currentBalance = currentBalance - approveResult.TransactionResult.GetChargedTransactionFees()["ELF"];
        }
        var caBalanceOutput = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = caAddress,
            Symbol = "ELF"
        });
        var managerTransferResult = await CaContractStubManagerInfo1.ManagerTransferFrom.SendAsync(
            new ManagerTransferFromInput
            {
                CaHash = caHash,
                From = DefaultAddress,
                To = User2Address,
                Symbol = "ELF",
                Amount = 1_00000000,
                Memo = "ca transfer."
            });
        {
            var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = DefaultAddress,
                Symbol = "ELF"
            });
            balance.Balance.ShouldBe(currentBalance - 1_00000000);
        }
        {
            var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = User2Address,
                Symbol = "ELF"
            });
            balance.Balance.ShouldBe(1_00000000);
        }
        {
            var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = caAddress,
                Symbol = "ELF"
            });
            balance.Balance.ShouldBe(caBalanceOutput.Balance -
                                     managerTransferResult.TransactionResult.GetChargedTransactionFees()["ELF"]);
        }
    }

    [Fact]
    public async Task ManagerTransferFromTest_Failed_NoPermission()
    {
        var caHash = await CreateCAHolderNoPermission();
        Address caAddress;
        {
            var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
            {
                CaHash = caHash
            });
            caAddress = holderInfo.CaAddress;
        }
        await TokenContractStub.Transfer.SendAsync(new TransferInput
        {
            Amount = 1_0000000_00000000,
            Symbol = "ELF",
            To = caAddress
        });
        var executionResult = await CaContractStub.ManagerTransferFrom.SendWithExceptionAsync(
            new ManagerTransferFromInput
            {
                CaHash = caHash,
                From = DefaultAddress,
                To = User2Address,
                Symbol = "ELF",
                Amount = 1_00000000,
                Memo = "ca transfer."
            });
        executionResult.TransactionResult.Error.ShouldContain("No permission.");
    }

    [Fact]
    public async Task ManagerTransferFromTest_Failed_InvalidInput()
    {
        var caHash = await CreateHolder();
        {
            var executionResult = await CaContractStubManagerInfo1.ManagerTransferFrom.SendWithExceptionAsync(
                new ManagerTransferFromInput
                {
                    From = DefaultAddress,
                    To = User2Address,
                    Symbol = "ELF",
                    Amount = 1_00000000,
                    Memo = "ca transfer."
                });
            executionResult.TransactionResult.Error.ShouldContain("CA hash is null.");
        }

        {
            var executionResult = await CaContractStubManagerInfo1.ManagerTransferFrom.SendWithExceptionAsync(
                new ManagerTransferFromInput
                {
                    CaHash = HashHelper.ComputeFrom("12345"),
                    From = DefaultAddress,
                    To = User2Address,
                    Symbol = "ELF",
                    Amount = 1_00000000,
                    Memo = "ca transfer."
                });
            executionResult.TransactionResult.Error.ShouldContain("CA holder is null.");
        }
        {
            var executionResult = await CaContractStubManagerInfo1.ManagerTransferFrom.SendWithExceptionAsync(
                new ManagerTransferFromInput
                {
                    CaHash = caHash,
                    To = User2Address,
                    Symbol = "ELF",
                    Amount = 1_00000000,
                    Memo = "ca transfer."
                });
            executionResult.TransactionResult.Error.ShouldContain("Invalid input.");
        }


        {
            var executionResult = await CaContractStubManagerInfo1.ManagerTransferFrom.SendWithExceptionAsync(
                new ManagerTransferFromInput
                {
                    CaHash = caHash,
                    From = DefaultAddress,
                    Symbol = "ELF",
                    Amount = 1_00000000,
                    Memo = "ca transfer."
                });
            executionResult.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var executionResult = await CaContractStubManagerInfo1.ManagerTransferFrom.SendWithExceptionAsync(
                new ManagerTransferFromInput
                {
                    CaHash = caHash,
                    From = DefaultAddress,
                    To = User2Address,
                    Amount = 1_00000000,
                    Memo = "ca transfer."
                });
            executionResult.TransactionResult.Error.ShouldContain("Invalid input.");
        }
    }
}