targetScope = 'resourceGroup'

@description('Short lowercase workload prefix used in resource names.')
@minLength(3)
@maxLength(12)
param prefix string = 'angleflow'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@allowed([
  'dev'
  'prod'
])
param environment string = 'dev'

@description('PostgreSQL administrator login. Do not use a reserved PostgreSQL name.')
param postgresAdministratorLogin string = 'angleflowadmin'

@secure()
@description('PostgreSQL administrator password. Supply this at deployment time; never commit it.')
param postgresAdministratorPassword string

@description('AKS system node count.')
@minValue(1)
@maxValue(10)
param nodeCount int = 2

@description('AKS system node VM size.')
param nodeVmSize string = 'Standard_D4s_v5'

@allowed([
  'Free'
  'Standard'
])
@description('Use Standard for a production control-plane SLA.')
param aksTier string = environment == 'prod' ? 'Standard' : 'Free'

var suffix = take(uniqueString(resourceGroup().id), 6)
var cleanPrefix = toLower(replace(prefix, '-', ''))
var baseName = '${prefix}-${environment}-${suffix}'
var acrName = take('${cleanPrefix}${environment}${suffix}', 50)
var keyVaultName = take('${cleanPrefix}-${environment}-${suffix}-kv', 24)
var postgresName = take('${cleanPrefix}-${environment}-${suffix}-pg', 63)
var acrPullRoleDefinitionId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '7f951dda-4ed3-4680-a7ca-43fe172d538d'
)
var networkContributorRoleDefinitionId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '4d97b98b-1d4f-4787-a291-c67834d212e7'
)

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${baseName}-logs'
  location: location
  properties: {
    retentionInDays: environment == 'prod' ? 30 : 14
    sku: {
      name: 'PerGB2018'
    }
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
  tags: tags
}

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: acrName
  location: location
  sku: {
    name: environment == 'prod' ? 'Premium' : 'Basic'
  }
  properties: {
    adminUserEnabled: false
    publicNetworkAccess: 'Enabled'
  }
  tags: tags
}

resource network 'Microsoft.Network/virtualNetworks@2024-05-01' = {
  name: '${baseName}-vnet'
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        '10.20.0.0/16'
      ]
    }
    subnets: [
      {
        name: 'aks'
        properties: {
          addressPrefix: '10.20.0.0/22'
        }
      }
      {
        name: 'postgres'
        properties: {
          addressPrefix: '10.20.4.0/24'
          delegations: [
            {
              name: 'postgres-flexible-server'
              properties: {
                serviceName: 'Microsoft.DBforPostgreSQL/flexibleServers'
              }
            }
          ]
        }
      }
    ]
  }
  tags: tags
}

resource aksSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' existing = {
  name: 'aks'
  parent: network
}

resource postgresSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' existing = {
  name: 'postgres'
  parent: network
}

resource postgresDns 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: 'privatelink.postgres.database.azure.com'
  location: 'global'
  tags: tags
}

resource postgresDnsLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  name: '${baseName}-postgres-link'
  parent: postgresDns
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: network.id
    }
  }
}

resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: postgresName
  location: location
  sku: {
    name: environment == 'prod' ? 'Standard_D4ds_v5' : 'Standard_B2ms'
    tier: environment == 'prod' ? 'GeneralPurpose' : 'Burstable'
  }
  properties: {
    version: '16'
    administratorLogin: postgresAdministratorLogin
    administratorLoginPassword: postgresAdministratorPassword
    authConfig: {
      activeDirectoryAuth: 'Disabled'
      passwordAuth: 'Enabled'
    }
    network: {
      delegatedSubnetResourceId: postgresSubnet.id
      privateDnsZoneArmResourceId: postgresDns.id
      publicNetworkAccess: 'Disabled'
    }
    highAvailability: {
      mode: environment == 'prod' ? 'ZoneRedundant' : 'Disabled'
    }
    storage: {
      storageSizeGB: environment == 'prod' ? 128 : 32
      autoGrow: 'Enabled'
    }
    backup: {
      backupRetentionDays: environment == 'prod' ? 14 : 7
      geoRedundantBackup: 'Disabled'
    }
  }
  dependsOn: [
    postgresDnsLink
  ]
  tags: tags
}

resource applicationDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  name: 'angleflow'
  parent: postgres
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    tenantId: tenant().tenantId
    enableRbacAuthorization: true
    enablePurgeProtection: environment == 'prod'
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    publicNetworkAccess: 'Enabled'
    sku: {
      family: 'A'
      name: 'standard'
    }
  }
  tags: tags
}

resource aksIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${baseName}-aks-id'
  location: location
  tags: tags
}

resource aksNetworkContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(network.id, aksIdentity.id, networkContributorRoleDefinitionId)
  scope: network
  properties: {
    roleDefinitionId: networkContributorRoleDefinitionId
    principalId: aksIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource cluster 'Microsoft.ContainerService/managedClusters@2026-03-01' = {
  name: '${baseName}-aks'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${aksIdentity.id}': {}
    }
  }
  sku: {
    name: 'Base'
    tier: aksTier
  }
  properties: {
    dnsPrefix: baseName
    enableRBAC: true
    nodeResourceGroup: '${resourceGroup().name}-${baseName}-nodes'
    oidcIssuerProfile: {
      enabled: true
    }
    securityProfile: {
      workloadIdentity: {
        enabled: true
      }
    }
    agentPoolProfiles: [
      {
        name: 'system'
        count: nodeCount
        vmSize: nodeVmSize
        osType: 'Linux'
        osSKU: 'AzureLinux'
        mode: 'System'
        type: 'VirtualMachineScaleSets'
        vnetSubnetID: aksSubnet.id
        maxPods: 50
        osDiskSizeGB: 128
        osDiskType: 'Managed'
        upgradeSettings: {
          maxSurge: '33%'
        }
      }
    ]
    networkProfile: {
      networkPlugin: 'azure'
      networkPluginMode: 'overlay'
      networkDataplane: 'cilium'
      loadBalancerSku: 'standard'
      outboundType: 'loadBalancer'
      serviceCidr: '10.21.0.0/16'
      dnsServiceIP: '10.21.0.10'
      podCidr: '10.22.0.0/16'
    }
    addonProfiles: {
      omsagent: {
        enabled: true
        config: {
          logAnalyticsWorkspaceResourceID: logs.id
        }
      }
    }
    autoUpgradeProfile: {
      upgradeChannel: 'stable'
      nodeOSUpgradeChannel: 'NodeImage'
    }
  }
  dependsOn: [
    aksNetworkContributor
  ]
  tags: tags
}

resource registryPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, cluster.id, acrPullRoleDefinitionId)
  scope: registry
  properties: {
    roleDefinitionId: acrPullRoleDefinitionId
    principalId: cluster.properties.identityProfile.kubeletidentity.objectId
    principalType: 'ServicePrincipal'
  }
}

var tags = {
  application: 'AngleFlow'
  environment: environment
  managedBy: 'Bicep'
}

output aksClusterName string = cluster.name
output containerRegistryName string = registry.name
output containerRegistryLoginServer string = registry.properties.loginServer
output keyVaultName string = keyVault.name
output postgresHost string = postgres.properties.fullyQualifiedDomainName
output postgresDatabase string = applicationDatabase.name
