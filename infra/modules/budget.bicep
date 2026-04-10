@description('Environment tag: dev, test, prod')
param env string

@description('Monthly budget limit in USD')
param monthlyBudgetUsd int

// Budget alerts are resource-group scoped.
// Alerts at 80% (forecast) and 100% (actual) of monthly spend.
var budgetName = 'budget-tripstracker-${env}'

resource budget 'Microsoft.Consumption/budgets@2023-11-01' = {
  name: budgetName
  properties: {
    category: 'Cost'
    amount: monthlyBudgetUsd
    timeGrain: 'Monthly'
    timePeriod: {
      // Start on the first of the current month; no end date
      startDate: '${substring(utcNow('yyyy-MM-dd'), 0, 7)}-01'
    }
    filter: {
      // Scope to this resource group only
    }
    notifications: {
      forecastedAt80: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 80
        thresholdType: 'Forecasted'
        contactEmails: []
        contactRoles: ['Owner', 'Contributor']
      }
      actualAt100: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 100
        thresholdType: 'Actual'
        contactEmails: []
        contactRoles: ['Owner', 'Contributor']
      }
    }
  }
}
