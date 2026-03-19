using 'main.bicep'

param env = 'dev'
param uniqueSuffix = 'REPLACE_ME'             // e.g. 'a1b2c' — set once, never change
param sqlEntraAdminObjectId = 'REPLACE_ME'    // az ad group show --group "YourAdminGroup" --query id -o tsv
param sqlEntraAdminDisplayName = 'REPLACE_ME' // e.g. 'TripsTracker Dev Admins'
