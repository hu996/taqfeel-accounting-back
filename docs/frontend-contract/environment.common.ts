export const commonEnv = {
  production: false,
  environmentName: 'development',
  url: 'https://localhost:7147/',
  baseUrl: 'https://localhost:7147/api/',
  tenantId: '',
  applicationType: 'Web',

  localStorage: {
    token: 'token',
    refreshToken: 'refreshToken',
    userData: 'UserData',
    activeTenant: 'activeTenant',
    activeTenantName: 'activeTenantName',
    permissions: 'permissions',
    language: 'i18n_locale'
  },

  API: {
    Auth: {
      login: 'Auth/Login',
      refreshToken: 'Auth/RefreshToken',
      logout: 'Auth/Logout',
      me: 'Auth/GetCurrentUser'
    },
    TenantSwitch: {
      myTenants: 'TenantSwitch/GetMyTenants',
      validate: 'TenantSwitch/ValidateTenantAccess'
    },
    Tenants: {
      getList: 'Tenants/GetTenants',
      getById: 'Tenants/GetTenantById',
      create: 'Tenants/AddTenant',
      update: 'Tenants/UpdateTenant',
      activate: 'Tenants/ActivateTenant',
      deactivate: 'Tenants/DeactivateTenant'
    },
    Users: {
      getList: 'Users/GetUsers',
      create: 'Users/AddUser',
      update: 'Users/UpdateUser',
      assignRoles: 'Users/AssignRoles',
      assignTenantAccess: 'Users/AssignTenantAccess'
    },
    FinancialYears: {
      getFilterList: 'FinancialYears/GetFinancialYearsByFilter',
      getById: 'FinancialYears/GetFinancialYearById',
      create: 'FinancialYears/AddFinancialYear',
      update: 'FinancialYears/UpdateFinancialYear',
      close: 'FinancialYears/CloseFinancialYear'
    },
    AccountingPeriods: {
      getFilterList: 'AccountingPeriods/GetAccountingPeriodsByFilter',
      getById: 'AccountingPeriods/GetAccountingPeriodById',
      create: 'AccountingPeriods/AddAccountingPeriod',
      update: 'AccountingPeriods/UpdateAccountingPeriod',
      lock: 'AccountingPeriods/LockAccountingPeriod',
      submitForReview: 'AccountingPeriods/SubmitForReview',
      close: 'AccountingPeriods/CloseAccountingPeriod',
      reopen: 'AccountingPeriods/ReopenAccountingPeriod'
    },
    ChartOfAccounts: {
      getFilterList: 'ChartOfAccounts/GetChartOfAccountsByFilter',
      getTree: 'ChartOfAccounts/GetChartOfAccountsTree',
      getById: 'ChartOfAccounts/GetChartOfAccountById',
      create: 'ChartOfAccounts/AddChartOfAccount',
      update: 'ChartOfAccounts/UpdateChartOfAccount',
      activate: 'ChartOfAccounts/ActivateChartOfAccount',
      deactivate: 'ChartOfAccounts/DeactivateChartOfAccount'
    },
    CostCenters: {
      getFilterList: 'CostCenters/GetCostCentersByFilter',
      create: 'CostCenters/AddCostCenter',
      update: 'CostCenters/UpdateCostCenter',
      activate: 'CostCenters/ActivateCostCenter',
      deactivate: 'CostCenters/DeactivateCostCenter'
    },
    JournalEntries: {
      getFilterList: 'JournalEntries/GetJournalEntriesByFilter',
      getById: 'JournalEntries/GetJournalEntryById',
      create: 'JournalEntries/AddJournalEntry',
      update: 'JournalEntries/UpdateJournalEntry',
      post: 'JournalEntries/PostJournalEntry',
      reverse: 'JournalEntries/ReverseJournalEntry',
      cancel: 'JournalEntries/CancelJournalEntry'
    },
    ClosingChecklist: {
      createTemplate: 'ClosingChecklist/AddTemplate',
      createDefaultTemplate: 'ClosingChecklist/AddDefaultTemplate',
      updateTemplate: 'ClosingChecklist/UpdateTemplate',
      createTemplateItem: 'ClosingChecklist/AddTemplateItem',
      updateTemplateItem: 'ClosingChecklist/UpdateTemplateItem',
      generateTasks: 'ClosingChecklist/GenerateClosingTasks'
    },
    ClosingTasks: {
      byPeriod: 'ClosingTasks/GetTasksByPeriod',
      assign: 'ClosingTasks/AssignTask',
      start: 'ClosingTasks/StartTask',
      submit: 'ClosingTasks/SubmitTask',
      approve: 'ClosingTasks/ApproveTask',
      reject: 'ClosingTasks/RejectTask',
      notApplicable: 'ClosingTasks/MarkTaskNotApplicable'
    },
    ClosingSubmissions: {
      byPeriod: 'ClosingSubmissions/GetSubmissionsByPeriod',
      submit: 'ClosingSubmissions/Submit',
      startReview: 'ClosingSubmissions/StartReview',
      approve: 'ClosingSubmissions/Approve',
      reject: 'ClosingSubmissions/Reject',
      closePeriod: 'ClosingSubmissions/ClosePeriod',
      reopenPeriod: 'ClosingSubmissions/ReopenPeriod'
    },
    Imports: {
      getFilterList: 'Imports/GetImportsByFilter',
      getById: 'Imports/GetImportById',
      upload: 'Imports/Upload',
      commit: 'Imports/Commit',
      cancel: 'Imports/Cancel',
      downloadTemplate: 'Imports/DownloadTemplate'
    },
    Documents: {
      getFilterList: 'Documents/GetDocumentsByFilter',
      upload: 'Documents/UploadDocument',
      download: 'Documents/DownloadDocument',
      delete: 'Documents/DeleteDocument'
    },
    AccountingReports: {
      trialBalance: 'AccountingReports/GetTrialBalance',
      generalLedger: 'AccountingReports/GetGeneralLedger',
      accountStatement: 'AccountingReports/GetAccountStatement',
      closingProgress: 'AccountingReports/GetClosingProgress'
    },
    Lookups: {
      byType: 'Lookups/GetByType',
      tenants: 'Lookups/GetTenants',
      users: 'Lookups/GetUsers',
      roles: 'Lookups/GetRoles',
      permissions: 'Lookups/GetPermissions',
      financialYears: 'Lookups/GetFinancialYears',
      accountingPeriods: 'Lookups/GetAccountingPeriods',
      chartOfAccounts: 'Lookups/GetChartOfAccounts',
      postingAccounts: 'Lookups/GetPostingAccounts',
      parentAccounts: 'Lookups/GetParentAccounts',
      costCenters: 'Lookups/GetCostCenters',
      accountTypes: 'Lookups/GetAccountTypes',
      normalBalances: 'Lookups/GetNormalBalances',
      documentTypes: 'Lookups/GetDocumentTypes',
      journalEntryStatuses: 'Lookups/GetJournalEntryStatuses',
      accountingPeriodStatuses: 'Lookups/GetAccountingPeriodStatuses',
      closingTaskStatuses: 'Lookups/GetClosingTaskStatuses',
      closingSubmissionStatuses: 'Lookups/GetClosingSubmissionStatuses',
      importTypes: 'Lookups/GetImportTypes'
    }
  }
};
