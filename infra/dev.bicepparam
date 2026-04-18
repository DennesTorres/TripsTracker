using 'main.bicep'

param env = 'dev'
param uniqueSuffix = 'ttk'
param location = 'uksouth'
param swaLocation = 'westeurope'
param sqlEntraAdminObjectId = 'ff61575c-8f14-4a41-8597-48f7cdd6c973'
param sqlEntraAdminDisplayName = 'SQLAdmins'
param nominatimUserAgent = 'TripsTracker/1.0 (dennes@bufaloinfo.com.br)'
param authAudience = 'api://05aa01d6-ed6d-4bd2-8844-2c13c91a873f'
param monthlyBudgetUsd = 20
