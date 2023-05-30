# Portkey Contracts

BRANCH | AZURE PIPELINES | TESTS | CODE COVERAGE
-------|-----------------|-------|--------------
MASTER | [![Build Status](https://dev.azure.com/Portkey-Finance/Portkey-Finance/_apis/build/status%2FPortkey-Wallet.portkey-contracts?branchName=master)](https://dev.azure.com/Portkey-Finance/Portkey-Finance/_build/latest?definitionId=7&branchName=master) | [![Test Status](https://img.shields.io/azure-devops/tests/Portkey-Finance/Portkey-Finance/7/master)](https://dev.azure.com/Portkey-Finance/Portkey-Finance/_build/latest?definitionId=7&branchName=master) | [![codecov](https://codecov.io/github/Portkey-Wallet/portkey-contracts/branch/master/graph/badge.svg?token=BFTABBNST5)](https://app.codecov.io/github/Portkey-Wallet/portkey-contracts)
DEV    | [![Build Status](https://dev.azure.com/Portkey-Finance/Portkey-Finance/_apis/build/status%2FPortkey-Wallet.portkey-contracts?branchName=dev)](https://dev.azure.com/Portkey-Finance/Portkey-Finance/_build/latest?definitionId=7&branchName=dev) | [![Test Status](https://img.shields.io/azure-devops/tests/Portkey-Finance/Portkey-Finance/7/dev)](https://dev.azure.com/Portkey-Finance/Portkey-Finance/_build/latest?definitionId=7&branchName=dev) | [![codecov](https://codecov.io/github/Portkey-Wallet/portkey-contracts/branch/master/graph/badge.svg?token=BFTABBNST5)](https://app.codecov.io/github/Portkey-Wallet/portkey-contracts)


The Portkey Contract is a new and cool smart contract wallet in aelf ecosystem that provides a familiar Web2 experience for controlling your Web3 identities and assets. It allows users to protect their identities and assets with guardians they trust. Portkey enables the management of devices/guardians at risk so that the owners keep control over their possessions. It also synchronizes users' identities through all aelf's chains. The contract is built on the aelf blockchain, a high-performance blockchain platform that uses a unique consensus algorithm called the aelf's Delegated Proof of Stake (AEDPoS).

## Installation

Before cloning the code and deploying the contract, command dependencies and development tools are needed. You can follow:

- [Common dependencies](https://aelf-boilerplate-docs.readthedocs.io/en/latest/overview/dependencies.html)
- [Building sources and development tools](https://aelf-boilerplate-docs.readthedocs.io/en/latest/overview/tools.html)

The following command will clone Portkey Contract into a folder. Please open a terminal and enter the following command:

```Bash
git clone https://github.com/Portkey-Wallet/portkey-contracts
```

The next step is to build the contract to ensure everything is working correctly. Once everything is built, you can run as follows:

```Bash
# enter the Launcher folder and build 
cd src/AElf.Boilerplate.PortkeyContracts.Launcher

# build
dotnet build

# run the node 
dotnet run
```

It will run a local temporary aelf node and automatically deploy the Portkey Contract on it. You can access the node from `localhost:1235`.

This temporary aelf node runs on a framework called Boilerplate for deploying smart contract easily. When running it, you might see errors showing incorrect password. To solve this, you need to back up your `aelf/keys`folder and start with an empty keys folder. Once you have cleaned the keys folder, stop and restart the node with `dotnet run`command shown above. It will automatically generate a new aelf account for you. This account will be used for running the aelf node and deploying the Portkey Contract.

### Test

You can easily run unit tests on Portkey Contracts. Navigate to the Portkey.Contracts.CA.Tests and run:

```Bash
cd ../../test/Portkey.Contracts.CA.Tests
dotnet test
```

## Usage

The Portkey Contract provides the following modules:

- `Actions`: Main functions to initialize the contract and create Web3 identities.
- `Managers`: Recover users' identities and manage the managers of their identities.
- `Guardians`: Manage the guardians which protect users' identities.
- `Synchronize`: Validate and synchronize users' identities through all aelf's chains.
- `CAServers/Verifiers`: Manage third-party servers.
- `Strategy`: Manage strategy for validating guardians.

To use these modules, you must first deploy the Portkey Contract on aelf blockchain. Once it's deployed, you can interact with the contract using any aelf-compatible wallet or client.

## Contributing

We welcome contributions to the Portkey Contract project. If you would like to contribute, please fork the repository and submit a pull request with your changes. Before submitting a pull request, please ensure that your code is well-tested and adheres to the aelf coding standards.

## License

Portkey Contract is licensed under [MIT](https://github.com/Portkey-Wallet/portkey-contracts/blob/master/LICENSE).

## Contact

If you have any questions or feedback, please feel free to contact us at the Portkey community channels. You can find us on Discord, Telegram, and other social media platforms.

Links:

- Website: https://portkey.finance/
- Twitter: https://twitter.com/Portkey_DID
- Discord: https://discord.com/invite/EUBq3rHQhr
