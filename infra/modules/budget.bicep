@description('Environment tag: dev, test, prod')
param env string

@description('Monthly budget limit in USD')
param monthlyBudgetUsd int

@description('Budget start date (first day of a month, yyyy-MM-dd). Defaults to current month.')
param startDate string = '${substring(utcNow('yyyy-MM-dd'), 0, 7)}-01'

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
      startDate: startDate
    }
    filter: {}
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
