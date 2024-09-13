using AElf.Boilerplate.TestBase;
using AElf.GovernmentSystem;
using AElf.Kernel.SmartContract;
using AElf.Kernel.SmartContract.Application;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp.Modularity;

namespace ZkLoginVerifier;

[DependsOn(typeof(MainChainDAppContractTestModule),
    typeof(GovernmentSystemAElfModule))]
public class ZkLoginVerifierTestAElfModule : MainChainDAppContractTestModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<ContractOptions>(o => o.ContractDeploymentAuthorityRequired = false);
        context.Services.RemoveAll<IPreExecutionPlugin>();
    }
}
