-- HRMS schema for SQL Server 2019+ (generated). Schema: hr
-- Run order: 01_schema.sql then 02_seed.sql
SET NOCOUNT ON;
GO
IF SCHEMA_ID('hr') IS NULL EXEC('CREATE SCHEMA hr');
GO

CREATE TABLE hr.[Companies] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [Code] nvarchar(20) NOT NULL,
  [NameAr] nvarchar(200) NOT NULL,
  [NameEn] nvarchar(200) NULL,
  [LogoAttachmentId] bigint NULL,
  [TimeZone] nvarchar(50) NOT NULL CONSTRAINT DF_Companies_TimeZone DEFAULT ('Asia/Riyadh'),
  [CurrencyCode] nvarchar(10) NOT NULL CONSTRAINT DF_Companies_CurrencyCode DEFAULT ('YER'),
  [DefaultLanguage] nvarchar(5) NOT NULL CONSTRAINT DF_Companies_DefaultLanguage DEFAULT ('ar'),
  [HijriAdjustDays] int NOT NULL CONSTRAINT DF_Companies_HijriAdjustDays DEFAULT (0),
  [IsActive] bit NOT NULL CONSTRAINT DF_Companies_IsActive DEFAULT (1),
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_Companies_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_Companies_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_Companies PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[FiscalYears] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [FiscalYear] int NOT NULL,
  [StartDate] date NOT NULL,
  [EndDate] date NOT NULL,
  [IsClosed] bit NOT NULL CONSTRAINT DF_FiscalYears_IsClosed DEFAULT (0),
  [IsCurrent] bit NOT NULL CONSTRAINT DF_FiscalYears_IsCurrent DEFAULT (0),
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_FiscalYears_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_FiscalYears_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_FiscalYears PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[UserGroups] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [Code] int NOT NULL,
  [NameAr] nvarchar(200) NOT NULL,
  [NameEn] nvarchar(200) NULL,
  [Notes] nvarchar(1000) NULL,
  [IsActive] bit NOT NULL CONSTRAINT DF_UserGroups_IsActive DEFAULT (1),
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_UserGroups_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_UserGroups_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_UserGroups PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[Roles] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [Code] int NOT NULL,
  [NameAr] nvarchar(200) NOT NULL,
  [NameEn] nvarchar(200) NULL,
  [Notes] nvarchar(1000) NULL,
  [IsActive] bit NOT NULL CONSTRAINT DF_Roles_IsActive DEFAULT (1),
  [IsSystem] bit NOT NULL CONSTRAINT DF_Roles_IsSystem DEFAULT (0),
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_Roles_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_Roles_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_Roles PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[Users] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [Username] nvarchar(100) NOT NULL,
  [PasswordHash] nvarchar(500) NOT NULL,
  [DisplayName] nvarchar(200) NOT NULL,
  [Email] nvarchar(200) NULL,
  [Mobile] nvarchar(30) NULL,
  [Language] nvarchar(5) NOT NULL CONSTRAINT DF_Users_Language DEFAULT ('ar'),
  [EmployeeId] bigint NULL,
  [UserGroupId] bigint NULL,
  [UserType] nvarchar(20) NOT NULL CONSTRAINT DF_Users_UserType DEFAULT ('normal'),
  [IsAdmin] bit NOT NULL CONSTRAINT DF_Users_IsAdmin DEFAULT (0),
  [IsActive] bit NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
  [FailedLogins] int NOT NULL CONSTRAINT DF_Users_FailedLogins DEFAULT (0),
  [LockedUntil] datetimeoffset(0) NULL,
  [LastLoginAt] datetimeoffset(0) NULL,
  [MustChangePassword] bit NOT NULL CONSTRAINT DF_Users_MustChangePassword DEFAULT (0),
  [TwoFactorEnabled] bit NOT NULL CONSTRAINT DF_Users_TwoFactorEnabled DEFAULT (0),
  [TwoFactorSecret] nvarchar(200) NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_Users_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_Users PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[Permissions] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [PermCode] nvarchar(150) NOT NULL,
  [Module] nvarchar(50) NOT NULL,
  [DescriptionAr] nvarchar(300) NULL,
  [DescriptionEn] nvarchar(300) NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_Permissions_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_Permissions_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_Permissions PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[RolePermissions] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [RoleId] bigint NOT NULL,
  [PermissionId] bigint NOT NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_RolePermissions_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_RolePermissions_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_RolePermissions PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[UserRoles] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [UserId] bigint NOT NULL,
  [RoleId] bigint NOT NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_UserRoles_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_UserRoles_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_UserRoles PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[UserGroupRoles] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [UserGroupId] bigint NOT NULL,
  [RoleId] bigint NOT NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_UserGroupRoles_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_UserGroupRoles_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_UserGroupRoles PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[UserDataScopes] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [UserId] bigint NOT NULL,
  [OrgNodeId] bigint NOT NULL,
  [IncludeChildren] bit NOT NULL CONSTRAINT DF_UserDataScopes_IncludeChildren DEFAULT (1),
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_UserDataScopes_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_UserDataScopes_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_UserDataScopes PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[RefreshTokens] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [UserId] bigint NOT NULL,
  [TokenHash] nvarchar(200) NOT NULL,
  [ExpiresAt] datetimeoffset(0) NOT NULL,
  [RevokedAt] datetimeoffset(0) NULL,
  [ReplacedByTokenId] bigint NULL,
  [DeviceUid] nvarchar(100) NULL,
  [IpAddress] nvarchar(60) NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_RefreshTokens_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  CONSTRAINT PK_RefreshTokens PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[UserDevices] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [UserId] bigint NOT NULL,
  [DeviceUid] nvarchar(100) NOT NULL,
  [Platform] nvarchar(20) NULL,
  [Model] nvarchar(100) NULL,
  [PushToken] nvarchar(500) NULL,
  [IsApproved] bit NOT NULL CONSTRAINT DF_UserDevices_IsApproved DEFAULT (0),
  [ApprovedBy] bigint NULL,
  [LastSeenAt] datetimeoffset(0) NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_UserDevices_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_UserDevices_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_UserDevices PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[Settings] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [SettingKey] nvarchar(100) NOT NULL,
  [SettingValue] nvarchar(max) NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_Settings_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_Settings_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_Settings PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[AuditLogs] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [UserId] bigint NULL,
  [EntityName] nvarchar(100) NOT NULL,
  [EntityId] nvarchar(50) NOT NULL,
  [ActionType] nvarchar(20) NOT NULL,
  [OldValues] nvarchar(max) NULL,
  [NewValues] nvarchar(max) NULL,
  [IpAddress] nvarchar(60) NULL,
  [DeviceName] nvarchar(100) NULL,
  [Reason] nvarchar(300) NULL,
  [OccurredAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_AuditLogs_OccurredAt DEFAULT (SYSDATETIMEOFFSET()),
  CONSTRAINT PK_AuditLogs PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[Notifications] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [UserId] bigint NOT NULL,
  [Title] nvarchar(200) NOT NULL,
  [Body] nvarchar(1000) NULL,
  [NotifType] nvarchar(50) NULL,
  [LinkRoute] nvarchar(300) NULL,
  [IsRead] bit NOT NULL CONSTRAINT DF_Notifications_IsRead DEFAULT (0),
  [ReadAt] datetimeoffset(0) NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_Notifications_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_Notifications_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_Notifications PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[Attachments] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [OwnerType] nvarchar(50) NOT NULL,
  [OwnerId] bigint NOT NULL,
  [FileName] nvarchar(300) NOT NULL,
  [ContentType] nvarchar(100) NULL,
  [SizeBytes] bigint NULL,
  [StoragePath] nvarchar(500) NOT NULL,
  [Checksum] nvarchar(100) NULL,
  [Title] nvarchar(200) NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_Attachments_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_Attachments_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_Attachments PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[ReportHeaders] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [ReportKey] nvarchar(100) NOT NULL,
  [TitleAr] nvarchar(300) NULL,
  [TitleEn] nvarchar(300) NULL,
  [HeaderHtml] nvarchar(max) NULL,
  [FooterHtml] nvarchar(max) NULL,
  [LogoAttachmentId] bigint NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_ReportHeaders_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_ReportHeaders_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_ReportHeaders PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[ReportViews] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [UserId] bigint NOT NULL,
  [ReportKey] nvarchar(100) NOT NULL,
  [ViewName] nvarchar(200) NOT NULL,
  [LayoutJson] nvarchar(max) NOT NULL,
  [IsShared] bit NOT NULL CONSTRAINT DF_ReportViews_IsShared DEFAULT (0),
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_ReportViews_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_ReportViews_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_ReportViews PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[OrgNodes] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [Code] int NOT NULL,
  [NameAr] nvarchar(200) NOT NULL,
  [NameEn] nvarchar(200) NULL,
  [Notes] nvarchar(1000) NULL,
  [IsActive] bit NOT NULL CONSTRAINT DF_OrgNodes_IsActive DEFAULT (1),
  [ParentId] bigint NULL,
  [NodeType] nvarchar(20) NOT NULL,
  [ManagerEmployeeId] bigint NULL,
  [NodePath] nvarchar(500) NULL,
  [NodeLevel] int NOT NULL CONSTRAINT DF_OrgNodes_NodeLevel DEFAULT (0),
  [SortOrder] int NOT NULL CONSTRAINT DF_OrgNodes_SortOrder DEFAULT (0),
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_OrgNodes_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_OrgNodes_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_OrgNodes PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[JobGrades] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [Code] int NOT NULL,
  [NameAr] nvarchar(200) NOT NULL,
  [NameEn] nvarchar(200) NULL,
  [Notes] nvarchar(1000) NULL,
  [IsActive] bit NOT NULL CONSTRAINT DF_JobGrades_IsActive DEFAULT (1),
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_JobGrades_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_JobGrades_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_JobGrades PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[Jobs] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [Code] int NOT NULL,
  [NameAr] nvarchar(200) NOT NULL,
  [NameEn] nvarchar(200) NULL,
  [Notes] nvarchar(1000) NULL,
  [IsActive] bit NOT NULL CONSTRAINT DF_Jobs_IsActive DEFAULT (1),
  [JobGradeId] bigint NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_Jobs_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_Jobs_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_Jobs PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[CostCenters] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [Code] int NOT NULL,
  [NameAr] nvarchar(200) NOT NULL,
  [NameEn] nvarchar(200) NULL,
  [Notes] nvarchar(1000) NULL,
  [IsActive] bit NOT NULL CONSTRAINT DF_CostCenters_IsActive DEFAULT (1),
  [AccountCode] nvarchar(50) NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_CostCenters_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_CostCenters_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_CostCenters PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[EmployeeGroups] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [Code] int NOT NULL,
  [NameAr] nvarchar(200) NOT NULL,
  [NameEn] nvarchar(200) NULL,
  [Notes] nvarchar(1000) NULL,
  [IsActive] bit NOT NULL CONSTRAINT DF_EmployeeGroups_IsActive DEFAULT (1),
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_EmployeeGroups_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_EmployeeGroups_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_EmployeeGroups PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[Projects] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [Code] int NOT NULL,
  [NameAr] nvarchar(200) NOT NULL,
  [NameEn] nvarchar(200) NULL,
  [Notes] nvarchar(1000) NULL,
  [IsActive] bit NOT NULL CONSTRAINT DF_Projects_IsActive DEFAULT (1),
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_Projects_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_Projects_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_Projects PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[Banks] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [Code] int NOT NULL,
  [NameAr] nvarchar(200) NOT NULL,
  [NameEn] nvarchar(200) NULL,
  [Notes] nvarchar(1000) NULL,
  [IsActive] bit NOT NULL CONSTRAINT DF_Banks_IsActive DEFAULT (1),
  [SwiftCode] nvarchar(20) NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_Banks_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_Banks_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_Banks PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[Countries] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [Code] int NOT NULL,
  [NameAr] nvarchar(200) NOT NULL,
  [NameEn] nvarchar(200) NULL,
  [Notes] nvarchar(1000) NULL,
  [IsActive] bit NOT NULL CONSTRAINT DF_Countries_IsActive DEFAULT (1),
  [IsoCode] nvarchar(3) NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_Countries_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_Countries_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_Countries PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[Cities] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [Code] int NOT NULL,
  [NameAr] nvarchar(200) NOT NULL,
  [NameEn] nvarchar(200) NULL,
  [Notes] nvarchar(1000) NULL,
  [IsActive] bit NOT NULL CONSTRAINT DF_Cities_IsActive DEFAULT (1),
  [CountryId] bigint NOT NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_Cities_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_Cities_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_Cities PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[InsuranceCompanies] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [Code] int NOT NULL,
  [NameAr] nvarchar(200) NOT NULL,
  [NameEn] nvarchar(200) NULL,
  [Notes] nvarchar(1000) NULL,
  [IsActive] bit NOT NULL CONSTRAINT DF_InsuranceCompanies_IsActive DEFAULT (1),
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_InsuranceCompanies_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_InsuranceCompanies_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_InsuranceCompanies PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[DocumentTypes] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [Code] int NOT NULL,
  [NameAr] nvarchar(200) NOT NULL,
  [NameEn] nvarchar(200) NULL,
  [Notes] nvarchar(1000) NULL,
  [IsActive] bit NOT NULL CONSTRAINT DF_DocumentTypes_IsActive DEFAULT (1),
  [RequiresExpiry] bit NOT NULL CONSTRAINT DF_DocumentTypes_RequiresExpiry DEFAULT (1),
  [AlertDaysBefore] int NOT NULL CONSTRAINT DF_DocumentTypes_AlertDaysBefore DEFAULT (30),
  [AppliesTo] nvarchar(20) NOT NULL CONSTRAINT DF_DocumentTypes_AppliesTo DEFAULT ('employee'),
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_DocumentTypes_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_DocumentTypes_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_DocumentTypes PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[Sponsors] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [Code] int NOT NULL,
  [NameAr] nvarchar(200) NOT NULL,
  [NameEn] nvarchar(200) NULL,
  [Notes] nvarchar(1000) NULL,
  [IsActive] bit NOT NULL CONSTRAINT DF_Sponsors_IsActive DEFAULT (1),
  [Mobile] nvarchar(30) NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_Sponsors_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_Sponsors_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_Sponsors PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[Nationalities] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [Code] int NOT NULL,
  [NameAr] nvarchar(200) NOT NULL,
  [NameEn] nvarchar(200) NULL,
  [Notes] nvarchar(1000) NULL,
  [IsActive] bit NOT NULL CONSTRAINT DF_Nationalities_IsActive DEFAULT (1),
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_Nationalities_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_Nationalities_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_Nationalities PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[WorkNatures] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [Code] int NOT NULL,
  [NameAr] nvarchar(200) NOT NULL,
  [NameEn] nvarchar(200) NULL,
  [Notes] nvarchar(1000) NULL,
  [IsActive] bit NOT NULL CONSTRAINT DF_WorkNatures_IsActive DEFAULT (1),
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_WorkNatures_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_WorkNatures_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_WorkNatures PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[Lookups] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [Code] int NOT NULL,
  [NameAr] nvarchar(200) NOT NULL,
  [NameEn] nvarchar(200) NULL,
  [Notes] nvarchar(1000) NULL,
  [IsActive] bit NOT NULL CONSTRAINT DF_Lookups_IsActive DEFAULT (1),
  [LookupType] nvarchar(50) NOT NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_Lookups_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_Lookups_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_Lookups PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[GeoLocations] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [Code] int NOT NULL,
  [NameAr] nvarchar(200) NOT NULL,
  [NameEn] nvarchar(200) NULL,
  [Notes] nvarchar(1000) NULL,
  [IsActive] bit NOT NULL CONSTRAINT DF_GeoLocations_IsActive DEFAULT (1),
  [Latitude] decimal(9,6) NOT NULL,
  [Longitude] decimal(9,6) NOT NULL,
  [RadiusMeters] int NOT NULL CONSTRAINT DF_GeoLocations_RadiusMeters DEFAULT (100),
  [OrgNodeId] bigint NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_GeoLocations_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_GeoLocations_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_GeoLocations PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[Employees] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [EmployeeNo] int NOT NULL,
  [FingerprintNo] int NULL,
  [Barcode] nvarchar(50) NULL,
  [FirstNameAr] nvarchar(100) NOT NULL,
  [MiddleNameAr] nvarchar(100) NULL,
  [LastNameAr] nvarchar(100) NOT NULL,
  [FullNameAr] nvarchar(300) NOT NULL,
  [FirstNameEn] nvarchar(100) NULL,
  [MiddleNameEn] nvarchar(100) NULL,
  [LastNameEn] nvarchar(100) NULL,
  [FullNameEn] nvarchar(300) NULL,
  [Gender] nvarchar(1) NOT NULL,
  [NationalityId] bigint NULL,
  [ReligionId] bigint NULL,
  [MaritalStatusId] bigint NULL,
  [BirthDate] date NULL,
  [BirthCountryId] bigint NULL,
  [BirthCityId] bigint NULL,
  [Mobile] nvarchar(30) NULL,
  [Email] nvarchar(200) NULL,
  [Notes] nvarchar(1000) NULL,
  [PhotoAttachmentId] bigint NULL,
  [OrgNodeId] bigint NOT NULL,
  [JobGradeId] bigint NULL,
  [JobId] bigint NULL,
  [IdentityProfession] nvarchar(200) NULL,
  [SponsorId] bigint NULL,
  [EmployeeGroupId] bigint NULL,
  [CostCenterId] bigint NULL,
  [ProjectId] bigint NULL,
  [BankId] bigint NULL,
  [BankAccount] nvarchar(200) NULL,
  [HireDate] date NOT NULL,
  [WorkNatureId] bigint NULL,
  [ContractTypeId] bigint NULL,
  [ContractStart] date NULL,
  [ContractEnd] date NULL,
  [IsOpenContract] bit NOT NULL CONSTRAINT DF_Employees_IsOpenContract DEFAULT (0),
  [StatusId] bigint NOT NULL,
  [DailyHours] decimal(5,2) NOT NULL CONSTRAINT DF_Employees_DailyHours DEFAULT (8),
  [MonthDays] int NOT NULL CONSTRAINT DF_Employees_MonthDays DEFAULT (30),
  [HourRate] decimal(18,2) NOT NULL CONSTRAINT DF_Employees_HourRate DEFAULT (0),
  [DayRate] decimal(18,2) NOT NULL CONSTRAINT DF_Employees_DayRate DEFAULT (0),
  [OvertimeEnabled] bit NOT NULL CONSTRAINT DF_Employees_OvertimeEnabled DEFAULT (0),
  [MobileAttendanceEnabled] bit NOT NULL CONSTRAINT DF_Employees_MobileAttendanceEnabled DEFAULT (0),
  [WorkStartDate] date NULL,
  [LastWorkStartDate] date NULL,
  [IdentityNo] nvarchar(200) NULL,
  [IdentityIssueDate] date NULL,
  [IdentityExpiryDate] date NULL,
  [IdentityCopyNo] nvarchar(20) NULL,
  [IdentityOther] nvarchar(200) NULL,
  [PassportNo] nvarchar(200) NULL,
  [PassportIssuePlace] nvarchar(100) NULL,
  [PassportIssueDate] date NULL,
  [PassportExpiryDate] date NULL,
  [BorderNo] nvarchar(50) NULL,
  [InsuranceCompanyId] bigint NULL,
  [InsuranceClass] nvarchar(50) NULL,
  [InsurancePolicyNo] nvarchar(50) NULL,
  [InsuranceDueDate] date NULL,
  [SocialInsuranceNo] nvarchar(50) NULL,
  [LaborOfficeNo] nvarchar(50) NULL,
  [SocialCompanyPct] decimal(5,2) NOT NULL CONSTRAINT DF_Employees_SocialCompanyPct DEFAULT (0),
  [SocialEmployeePct] decimal(5,2) NOT NULL CONSTRAINT DF_Employees_SocialEmployeePct DEFAULT (0),
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_Employees_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_Employees_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_Employees PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[EmployeeSalaryItems] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [EmployeeId] bigint NOT NULL,
  [PayrollItemId] bigint NOT NULL,
  [Amount] decimal(18,2) NOT NULL CONSTRAINT DF_EmployeeSalaryItems_Amount DEFAULT (0),
  [Percentage] decimal(5,2) NULL,
  [Notes] nvarchar(500) NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_EmployeeSalaryItems_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_EmployeeSalaryItems_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_EmployeeSalaryItems PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[EmployeeDocuments] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [EmployeeId] bigint NOT NULL,
  [DocumentTypeId] bigint NOT NULL,
  [DocumentNo] nvarchar(100) NULL,
  [IssueDate] date NULL,
  [ExpiryDate] date NULL,
  [AttachmentId] bigint NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_EmployeeDocuments_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_EmployeeDocuments_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_EmployeeDocuments PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[EmployeeArchive] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [EmployeeId] bigint NOT NULL,
  [Title] nvarchar(200) NOT NULL,
  [AttachmentId] bigint NOT NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_EmployeeArchive_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_EmployeeArchive_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_EmployeeArchive PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[EmployeeCompanions] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [EmployeeId] bigint NOT NULL,
  [RelationId] bigint NOT NULL,
  [FullName] nvarchar(200) NOT NULL,
  [ResidenceNo] nvarchar(50) NULL,
  [IssueDate] date NULL,
  [ExpiryDate] date NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_EmployeeCompanions_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_EmployeeCompanions_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_EmployeeCompanions PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[EmployeeQualifications] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [EmployeeId] bigint NOT NULL,
  [QualificationId] bigint NULL,
  [Major] nvarchar(200) NULL,
  [IssueDate] date NULL,
  [Notes] nvarchar(500) NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_EmployeeQualifications_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_EmployeeQualifications_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_EmployeeQualifications PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[EmployeeExperiences] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [EmployeeId] bigint NOT NULL,
  [JobTitle] nvarchar(200) NULL,
  [YearsOfExperience] decimal(4,1) NULL,
  [Details] nvarchar(1000) NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_EmployeeExperiences_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_EmployeeExperiences_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_EmployeeExperiences PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[EmployeePortalAccounts] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [EmployeeId] bigint NOT NULL,
  [UserId] bigint NOT NULL,
  [IsEnabled] bit NOT NULL CONSTRAINT DF_EmployeePortalAccounts_IsEnabled DEFAULT (1),
  [AccessFinancial] bit NOT NULL CONSTRAINT DF_EmployeePortalAccounts_AccessFinancial DEFAULT (0),
  [AccessAttendance] bit NOT NULL CONSTRAINT DF_EmployeePortalAccounts_AccessAttendance DEFAULT (1),
  [AccessRequests] bit NOT NULL CONSTRAINT DF_EmployeePortalAccounts_AccessRequests DEFAULT (1),
  [AllowMobilePunch] bit NOT NULL CONSTRAINT DF_EmployeePortalAccounts_AllowMobilePunch DEFAULT (0),
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_EmployeePortalAccounts_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_EmployeePortalAccounts_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_EmployeePortalAccounts PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[EmployeeWorkStarts] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [EmployeeId] bigint NOT NULL,
  [StartDate] date NOT NULL,
  [ApprovedBy] bigint NULL,
  [ApprovedAt] datetimeoffset(0) NULL,
  [Notes] nvarchar(500) NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_EmployeeWorkStarts_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_EmployeeWorkStarts_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_EmployeeWorkStarts PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[Shifts] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [Code] int NOT NULL,
  [NameAr] nvarchar(200) NOT NULL,
  [NameEn] nvarchar(200) NULL,
  [Notes] nvarchar(1000) NULL,
  [IsActive] bit NOT NULL CONSTRAINT DF_Shifts_IsActive DEFAULT (1),
  [CycleType] nvarchar(20) NOT NULL CONSTRAINT DF_Shifts_CycleType DEFAULT ('weekly'),
  [StartTime] time(0) NOT NULL,
  [EndTime] time(0) NOT NULL,
  [BreakMinutes] int NOT NULL CONSTRAINT DF_Shifts_BreakMinutes DEFAULT (0),
  [WorkMinutes] int NOT NULL,
  [CrossesMidnight] bit NOT NULL CONSTRAINT DF_Shifts_CrossesMidnight DEFAULT (0),
  [BeforeInMin] int NOT NULL CONSTRAINT DF_Shifts_BeforeInMin DEFAULT (0),
  [AfterInMin] int NOT NULL CONSTRAINT DF_Shifts_AfterInMin DEFAULT (0),
  [LateGraceEnabled] bit NOT NULL CONSTRAINT DF_Shifts_LateGraceEnabled DEFAULT (0),
  [LateGraceMin] int NOT NULL CONSTRAINT DF_Shifts_LateGraceMin DEFAULT (0),
  [LateAfterGrace] bit NOT NULL CONSTRAINT DF_Shifts_LateAfterGrace DEFAULT (0),
  [BeforeOutMin] int NOT NULL CONSTRAINT DF_Shifts_BeforeOutMin DEFAULT (0),
  [AfterOutMin] int NOT NULL CONSTRAINT DF_Shifts_AfterOutMin DEFAULT (0),
  [EarlyGraceEnabled] bit NOT NULL CONSTRAINT DF_Shifts_EarlyGraceEnabled DEFAULT (0),
  [EarlyGraceMin] int NOT NULL CONSTRAINT DF_Shifts_EarlyGraceMin DEFAULT (0),
  [EarlyBeforeGrace] bit NOT NULL CONSTRAINT DF_Shifts_EarlyBeforeGrace DEFAULT (0),
  [PunchMode] nvarchar(20) NOT NULL CONSTRAINT DF_Shifts_PunchMode DEFAULT ('inout'),
  [FlexibleHours] decimal(5,2) NULL,
  [RoundWorkTime] bit NOT NULL CONSTRAINT DF_Shifts_RoundWorkTime DEFAULT (0),
  [PresentIfNoOut] bit NOT NULL CONSTRAINT DF_Shifts_PresentIfNoOut DEFAULT (0),
  [PresentIfNoIn] bit NOT NULL CONSTRAINT DF_Shifts_PresentIfNoIn DEFAULT (0),
  [OvertimeEnabled] bit NOT NULL CONSTRAINT DF_Shifts_OvertimeEnabled DEFAULT (0),
  [OvertimeFrom] time(0) NULL,
  [OvertimeTo] time(0) NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_Shifts_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_Shifts_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_Shifts PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[ShiftDays] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [ShiftId] bigint NOT NULL,
  [DayOfWeek] int NOT NULL,
  [IsWorkDay] bit NOT NULL CONSTRAINT DF_ShiftDays_IsWorkDay DEFAULT (1),
  [StartTime] time(0) NULL,
  [EndTime] time(0) NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_ShiftDays_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_ShiftDays_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_ShiftDays PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[ShiftRotations] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [Code] int NOT NULL,
  [NameAr] nvarchar(200) NOT NULL,
  [NameEn] nvarchar(200) NULL,
  [Notes] nvarchar(1000) NULL,
  [IsActive] bit NOT NULL CONSTRAINT DF_ShiftRotations_IsActive DEFAULT (1),
  [CycleDays] int NOT NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_ShiftRotations_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_ShiftRotations_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_ShiftRotations PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[ShiftRotationDays] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [RotationId] bigint NOT NULL,
  [DayIndex] int NOT NULL,
  [ShiftId] bigint NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_ShiftRotationDays_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_ShiftRotationDays_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_ShiftRotationDays PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[EmployeeShifts] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [EmployeeId] bigint NOT NULL,
  [ShiftId] bigint NOT NULL,
  [EffectiveFrom] date NOT NULL,
  [EffectiveTo] date NULL,
  [RotationId] bigint NULL,
  [RotationStartDate] date NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_EmployeeShifts_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_EmployeeShifts_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_EmployeeShifts PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[BiometricDevices] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [Code] int NOT NULL,
  [NameAr] nvarchar(200) NOT NULL,
  [NameEn] nvarchar(200) NULL,
  [Notes] nvarchar(1000) NULL,
  [IsActive] bit NOT NULL CONSTRAINT DF_BiometricDevices_IsActive DEFAULT (1),
  [SerialNumber] nvarchar(100) NULL,
  [ConnectionType] nvarchar(10) NOT NULL CONSTRAINT DF_BiometricDevices_ConnectionType DEFAULT ('ip'),
  [IpAddress] nvarchar(100) NULL,
  [DomainName] nvarchar(200) NULL,
  [Port] int NOT NULL CONSTRAINT DF_BiometricDevices_Port DEFAULT (4370),
  [Protocol] nvarchar(30) NOT NULL CONSTRAINT DF_BiometricDevices_Protocol DEFAULT ('zk'),
  [OrgNodeId] bigint NULL,
  [AutoPullMinutes] int NOT NULL CONSTRAINT DF_BiometricDevices_AutoPullMinutes DEFAULT (0),
  [DeviceKeyHash] nvarchar(200) NULL,
  [IsOnline] bit NOT NULL CONSTRAINT DF_BiometricDevices_IsOnline DEFAULT (0),
  [LastSyncAt] datetimeoffset(0) NULL,
  [LastPunchTime] datetimeoffset(0) NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_BiometricDevices_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_BiometricDevices_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_BiometricDevices PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[PunchLogs] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [DeviceId] bigint NULL,
  [Source] nvarchar(20) NOT NULL,
  [FingerprintNo] int NULL,
  [EmployeeId] bigint NULL,
  [PunchTime] datetimeoffset(0) NOT NULL,
  [LocalTime] datetime2(0) NOT NULL,
  [Direction] nvarchar(10) NOT NULL CONSTRAINT DF_PunchLogs_Direction DEFAULT ('unknown'),
  [ClientId] uniqueidentifier NULL,
  [Latitude] decimal(9,6) NULL,
  [Longitude] decimal(9,6) NULL,
  [AccuracyM] decimal(7,1) NULL,
  [IsMock] bit NOT NULL CONSTRAINT DF_PunchLogs_IsMock DEFAULT (0),
  [IsOffline] bit NOT NULL CONSTRAINT DF_PunchLogs_IsOffline DEFAULT (0),
  [GeoLocationId] bigint NULL,
  [DistanceM] decimal(9,1) NULL,
  [Status] nvarchar(20) NOT NULL CONSTRAINT DF_PunchLogs_Status DEFAULT ('accepted'),
  [ReviewedBy] bigint NULL,
  [ReviewedAt] datetimeoffset(0) NULL,
  [Reason] nvarchar(300) NULL,
  [RawPayload] nvarchar(max) NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_PunchLogs_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_PunchLogs_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_PunchLogs PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[OfficialHolidays] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [Code] int NOT NULL,
  [NameAr] nvarchar(200) NOT NULL,
  [NameEn] nvarchar(200) NULL,
  [Notes] nvarchar(1000) NULL,
  [IsActive] bit NOT NULL CONSTRAINT DF_OfficialHolidays_IsActive DEFAULT (1),
  [DayOfMonth] int NULL,
  [MonthOfYear] int NULL,
  [CalendarType] nvarchar(10) NOT NULL CONSTRAINT DF_OfficialHolidays_CalendarType DEFAULT ('gregorian'),
  [DurationDays] int NOT NULL CONSTRAINT DF_OfficialHolidays_DurationDays DEFAULT (1),
  [RepeatsYearly] bit NOT NULL CONSTRAINT DF_OfficialHolidays_RepeatsYearly DEFAULT (1),
  [SpecificStartDate] date NULL,
  [OrgNodeId] bigint NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_OfficialHolidays_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_OfficialHolidays_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_OfficialHolidays PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[ExcuseTypes] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [Code] int NOT NULL,
  [NameAr] nvarchar(200) NOT NULL,
  [NameEn] nvarchar(200) NULL,
  [Notes] nvarchar(1000) NULL,
  [IsActive] bit NOT NULL CONSTRAINT DF_ExcuseTypes_IsActive DEFAULT (1),
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_ExcuseTypes_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_ExcuseTypes_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_ExcuseTypes PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[Excuses] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [DocNo] int NOT NULL,
  [ExcuseTypeId] bigint NOT NULL,
  [EmployeeId] bigint NOT NULL,
  [ExcuseDate] date NOT NULL,
  [FromTime] time(0) NULL,
  [ToTime] time(0) NULL,
  [Description] nvarchar(1000) NULL,
  [HasAttachment] bit NOT NULL CONSTRAINT DF_Excuses_HasAttachment DEFAULT (0),
  [IsApproved] bit NOT NULL CONSTRAINT DF_Excuses_IsApproved DEFAULT (1),
  [RequestId] bigint NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_Excuses_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_Excuses_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_Excuses PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[ExcuseShifts] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [ExcuseId] bigint NOT NULL,
  [ShiftId] bigint NOT NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_ExcuseShifts_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_ExcuseShifts_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_ExcuseShifts PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[MonthlyAttendances] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [FiscalYearId] bigint NOT NULL,
  [PeriodYear] int NOT NULL,
  [PeriodMonth] int NOT NULL,
  [PeriodFrom] date NOT NULL,
  [PeriodTo] date NOT NULL,
  [CalculatedOn] date NULL,
  [Notes] nvarchar(500) NULL,
  [IsClosed] bit NOT NULL CONSTRAINT DF_MonthlyAttendances_IsClosed DEFAULT (0),
  [ClosedAt] datetimeoffset(0) NULL,
  [ClosedBy] bigint NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_MonthlyAttendances_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_MonthlyAttendances_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_MonthlyAttendances PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[MonthlyAttendanceLines] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [MonthlyId] bigint NOT NULL,
  [EmployeeId] bigint NOT NULL,
  [RequiredMinutes] int NOT NULL CONSTRAINT DF_MonthlyAttendanceLines_RequiredMinutes DEFAULT (0),
  [WorkMinutes] int NOT NULL CONSTRAINT DF_MonthlyAttendanceLines_WorkMinutes DEFAULT (0),
  [LateMin] int NOT NULL CONSTRAINT DF_MonthlyAttendanceLines_LateMin DEFAULT (0),
  [EarlyLeaveMin] int NOT NULL CONSTRAINT DF_MonthlyAttendanceLines_EarlyLeaveMin DEFAULT (0),
  [AbsentMin] int NOT NULL CONSTRAINT DF_MonthlyAttendanceLines_AbsentMin DEFAULT (0),
  [AbsentDays] decimal(5,2) NOT NULL CONSTRAINT DF_MonthlyAttendanceLines_AbsentDays DEFAULT (0),
  [ExcusedMin] int NOT NULL CONSTRAINT DF_MonthlyAttendanceLines_ExcusedMin DEFAULT (0),
  [OvertimeMin] int NOT NULL CONSTRAINT DF_MonthlyAttendanceLines_OvertimeMin DEFAULT (0),
  [LeaveDays] decimal(5,2) NOT NULL CONSTRAINT DF_MonthlyAttendanceLines_LeaveDays DEFAULT (0),
  [Notes] nvarchar(500) NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_MonthlyAttendanceLines_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_MonthlyAttendanceLines_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_MonthlyAttendanceLines PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[DailyAttendance] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [EmployeeId] bigint NOT NULL,
  [WorkDate] date NOT NULL,
  [ShiftId] bigint NULL,
  [InTime] datetimeoffset(0) NULL,
  [OutTime] datetimeoffset(0) NULL,
  [RequiredMinutes] int NOT NULL CONSTRAINT DF_DailyAttendance_RequiredMinutes DEFAULT (0),
  [WorkMinutes] int NOT NULL CONSTRAINT DF_DailyAttendance_WorkMinutes DEFAULT (0),
  [LateMin] int NOT NULL CONSTRAINT DF_DailyAttendance_LateMin DEFAULT (0),
  [EarlyLeaveMin] int NOT NULL CONSTRAINT DF_DailyAttendance_EarlyLeaveMin DEFAULT (0),
  [AbsentMin] int NOT NULL CONSTRAINT DF_DailyAttendance_AbsentMin DEFAULT (0),
  [OvertimeMin] int NOT NULL CONSTRAINT DF_DailyAttendance_OvertimeMin DEFAULT (0),
  [ExcusedMin] int NOT NULL CONSTRAINT DF_DailyAttendance_ExcusedMin DEFAULT (0),
  [Status] nvarchar(20) NOT NULL,
  [LeaveId] bigint NULL,
  [ExcuseId] bigint NULL,
  [HolidayId] bigint NULL,
  [IsManualOverride] bit NOT NULL CONSTRAINT DF_DailyAttendance_IsManualOverride DEFAULT (0),
  [OverrideReason] nvarchar(300) NULL,
  [ComputedAt] datetimeoffset(0) NULL,
  [MonthlyId] bigint NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_DailyAttendance_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_DailyAttendance_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_DailyAttendance PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[LeaveTypes] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [Code] int NOT NULL,
  [NameAr] nvarchar(200) NOT NULL,
  [NameEn] nvarchar(200) NULL,
  [Notes] nvarchar(1000) NULL,
  [IsActive] bit NOT NULL CONSTRAINT DF_LeaveTypes_IsActive DEFAULT (1),
  [IsPaid] bit NOT NULL CONSTRAINT DF_LeaveTypes_IsPaid DEFAULT (1),
  [DeductsBalance] bit NOT NULL CONSTRAINT DF_LeaveTypes_DeductsBalance DEFAULT (1),
  [RequiresAttachment] bit NOT NULL CONSTRAINT DF_LeaveTypes_RequiresAttachment DEFAULT (0),
  [MaxDays] int NULL,
  [AllowHalfDay] bit NOT NULL CONSTRAINT DF_LeaveTypes_AllowHalfDay DEFAULT (0),
  [GenderRestriction] nvarchar(1) NULL,
  [CountsHolidays] bit NOT NULL CONSTRAINT DF_LeaveTypes_CountsHolidays DEFAULT (0),
  [AnnualEntitlementDays] decimal(5,2) NULL,
  [AccrualMode] nvarchar(20) NOT NULL CONSTRAINT DF_LeaveTypes_AccrualMode DEFAULT ('monthly'),
  [CarryOverMaxDays] decimal(5,2) NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_LeaveTypes_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_LeaveTypes_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_LeaveTypes PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[LeavePolicies] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [Code] int NOT NULL,
  [NameAr] nvarchar(200) NOT NULL,
  [NameEn] nvarchar(200) NULL,
  [Notes] nvarchar(1000) NULL,
  [IsActive] bit NOT NULL CONSTRAINT DF_LeavePolicies_IsActive DEFAULT (1),
  [LeaveTypeId] bigint NOT NULL,
  [EmployeeGroupId] bigint NULL,
  [MinServiceMonths] int NOT NULL CONSTRAINT DF_LeavePolicies_MinServiceMonths DEFAULT (0),
  [DaysPerYear] decimal(5,2) NOT NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_LeavePolicies_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_LeavePolicies_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_LeavePolicies PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[LeaveBalances] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [EmployeeId] bigint NOT NULL,
  [LeaveTypeId] bigint NOT NULL,
  [BalanceYear] int NOT NULL,
  [OpeningDays] decimal(6,2) NOT NULL CONSTRAINT DF_LeaveBalances_OpeningDays DEFAULT (0),
  [AccruedDays] decimal(6,2) NOT NULL CONSTRAINT DF_LeaveBalances_AccruedDays DEFAULT (0),
  [UsedDays] decimal(6,2) NOT NULL CONSTRAINT DF_LeaveBalances_UsedDays DEFAULT (0),
  [AdjustedDays] decimal(6,2) NOT NULL CONSTRAINT DF_LeaveBalances_AdjustedDays DEFAULT (0),
  [IsApproved] bit NOT NULL CONSTRAINT DF_LeaveBalances_IsApproved DEFAULT (0),
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_LeaveBalances_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_LeaveBalances_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_LeaveBalances PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[LeaveLedger] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [EmployeeId] bigint NOT NULL,
  [LeaveTypeId] bigint NOT NULL,
  [EntryDate] date NOT NULL,
  [EntryType] nvarchar(20) NOT NULL,
  [Days] decimal(6,2) NOT NULL,
  [LeaveId] bigint NULL,
  [Notes] nvarchar(500) NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_LeaveLedger_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_LeaveLedger_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_LeaveLedger PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[Leaves] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [DocNo] int NOT NULL,
  [EmployeeId] bigint NOT NULL,
  [LeaveTypeId] bigint NOT NULL,
  [FromDate] date NOT NULL,
  [ToDate] date NOT NULL,
  [Days] decimal(6,2) NOT NULL,
  [IsHalfDay] bit NOT NULL CONSTRAINT DF_Leaves_IsHalfDay DEFAULT (0),
  [SubstituteEmployeeId] bigint NULL,
  [Reason] nvarchar(1000) NULL,
  [AttachmentId] bigint NULL,
  [Status] nvarchar(20) NOT NULL CONSTRAINT DF_Leaves_Status DEFAULT ('draft'),
  [ActualReturnDate] date NULL,
  [RequestId] bigint NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_Leaves_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_Leaves_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_Leaves PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[ApprovalFlows] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [Code] int NOT NULL,
  [NameAr] nvarchar(200) NOT NULL,
  [NameEn] nvarchar(200) NULL,
  [Notes] nvarchar(1000) NULL,
  [IsActive] bit NOT NULL CONSTRAINT DF_ApprovalFlows_IsActive DEFAULT (1),
  [RequestType] nvarchar(30) NOT NULL,
  [MinAmount] decimal(18,2) NULL,
  [MaxAmount] decimal(18,2) NULL,
  [MinDays] decimal(6,2) NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_ApprovalFlows_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_ApprovalFlows_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_ApprovalFlows PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[ApprovalFlowSteps] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [FlowId] bigint NOT NULL,
  [StepNo] int NOT NULL,
  [ApproverKind] nvarchar(20) NOT NULL,
  [RoleId] bigint NULL,
  [ApproverUserId] bigint NULL,
  [NodeLevelUp] int NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_ApprovalFlowSteps_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_ApprovalFlowSteps_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_ApprovalFlowSteps PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[Requests] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [DocNo] int NOT NULL,
  [RequestType] nvarchar(30) NOT NULL,
  [EmployeeId] bigint NOT NULL,
  [Status] nvarchar(20) NOT NULL CONSTRAINT DF_Requests_Status DEFAULT ('draft'),
  [PayloadJson] nvarchar(max) NOT NULL,
  [Reason] nvarchar(1000) NULL,
  [FlowId] bigint NULL,
  [CurrentStepNo] int NULL,
  [SubmittedAt] datetimeoffset(0) NULL,
  [DecidedAt] datetimeoffset(0) NULL,
  [ResultEntityType] nvarchar(50) NULL,
  [ResultEntityId] bigint NULL,
  [ClientId] uniqueidentifier NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_Requests_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_Requests_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_Requests PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[RequestSteps] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [RequestId] bigint NOT NULL,
  [StepNo] int NOT NULL,
  [ApproverUserId] bigint NULL,
  [Decision] nvarchar(20) NOT NULL CONSTRAINT DF_RequestSteps_Decision DEFAULT ('pending'),
  [DecidedAt] datetimeoffset(0) NULL,
  [Comment] nvarchar(1000) NULL,
  [DelegatedFromUserId] bigint NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_RequestSteps_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_RequestSteps_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_RequestSteps PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[RequestComments] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [RequestId] bigint NOT NULL,
  [UserId] bigint NOT NULL,
  [CommentText] nvarchar(2000) NOT NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_RequestComments_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_RequestComments_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_RequestComments PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[Delegations] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [FromUserId] bigint NOT NULL,
  [ToUserId] bigint NOT NULL,
  [FromDate] date NOT NULL,
  [ToDate] date NOT NULL,
  [IsActive] bit NOT NULL CONSTRAINT DF_Delegations_IsActive DEFAULT (1),
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_Delegations_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_Delegations_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_Delegations PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[PayrollItems] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [Code] int NOT NULL,
  [NameAr] nvarchar(200) NOT NULL,
  [NameEn] nvarchar(200) NULL,
  [Notes] nvarchar(1000) NULL,
  [IsActive] bit NOT NULL CONSTRAINT DF_PayrollItems_IsActive DEFAULT (1),
  [ItemType] nvarchar(10) NOT NULL,
  [CalcMethod] nvarchar(20) NOT NULL CONSTRAINT DF_PayrollItems_CalcMethod DEFAULT ('fixed'),
  [IsBasic] bit NOT NULL CONSTRAINT DF_PayrollItems_IsBasic DEFAULT (0),
  [IsFixed] bit NOT NULL CONSTRAINT DF_PayrollItems_IsFixed DEFAULT (1),
  [SubjectToInsurance] bit NOT NULL CONSTRAINT DF_PayrollItems_SubjectToInsurance DEFAULT (0),
  [SubjectToTax] bit NOT NULL CONSTRAINT DF_PayrollItems_SubjectToTax DEFAULT (0),
  [AccountCode] nvarchar(50) NULL,
  [SystemKey] nvarchar(30) NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_PayrollItems_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_PayrollItems_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_PayrollItems PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[VariableItems] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [EmployeeId] bigint NOT NULL,
  [PayrollItemId] bigint NOT NULL,
  [PeriodYear] int NOT NULL,
  [PeriodMonth] int NOT NULL,
  [Amount] decimal(18,2) NOT NULL CONSTRAINT DF_VariableItems_Amount DEFAULT (0),
  [Percentage] decimal(5,2) NULL,
  [Notes] nvarchar(500) NULL,
  [IsApplied] bit NOT NULL CONSTRAINT DF_VariableItems_IsApplied DEFAULT (0),
  [PayrollRunId] bigint NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_VariableItems_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_VariableItems_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_VariableItems PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[Advances] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [DocNo] int NOT NULL,
  [EmployeeId] bigint NOT NULL,
  [AdvanceDate] date NOT NULL,
  [Amount] decimal(18,2) NOT NULL,
  [InstallmentsCount] int NOT NULL CONSTRAINT DF_Advances_InstallmentsCount DEFAULT (1),
  [InstallmentAmount] decimal(18,2) NOT NULL,
  [FirstDeductYear] int NOT NULL,
  [FirstDeductMonth] int NOT NULL,
  [Reason] nvarchar(1000) NULL,
  [Status] nvarchar(20) NOT NULL CONSTRAINT DF_Advances_Status DEFAULT ('active'),
  [RemainingAmount] decimal(18,2) NOT NULL,
  [RequestId] bigint NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_Advances_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_Advances_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_Advances PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[AdvanceInstallments] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [AdvanceId] bigint NOT NULL,
  [InstallmentNo] int NOT NULL,
  [PeriodYear] int NOT NULL,
  [PeriodMonth] int NOT NULL,
  [Amount] decimal(18,2) NOT NULL,
  [PaidAmount] decimal(18,2) NOT NULL CONSTRAINT DF_AdvanceInstallments_PaidAmount DEFAULT (0),
  [Status] nvarchar(20) NOT NULL CONSTRAINT DF_AdvanceInstallments_Status DEFAULT ('pending'),
  [PayrollRunLineId] bigint NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_AdvanceInstallments_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_AdvanceInstallments_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_AdvanceInstallments PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[AdvanceRepayments] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [AdvanceId] bigint NOT NULL,
  [RepayDate] date NOT NULL,
  [Amount] decimal(18,2) NOT NULL,
  [Method] nvarchar(20) NULL,
  [Notes] nvarchar(500) NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_AdvanceRepayments_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_AdvanceRepayments_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_AdvanceRepayments PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[Custodies] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [DocNo] int NOT NULL,
  [EmployeeId] bigint NOT NULL,
  [Description] nvarchar(500) NOT NULL,
  [SerialNo] nvarchar(100) NULL,
  [CustodyValue] decimal(18,2) NULL,
  [DeliveredOn] date NOT NULL,
  [ExpectedReturnOn] date NULL,
  [ReturnedOn] date NULL,
  [ItemCondition] nvarchar(20) NULL,
  [DeductedAmount] decimal(18,2) NULL,
  [Status] nvarchar(20) NOT NULL CONSTRAINT DF_Custodies_Status DEFAULT ('delivered'),
  [AttachmentId] bigint NULL,
  [RequestId] bigint NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_Custodies_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_Custodies_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_Custodies PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[PayrollRuns] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [DocNo] int NOT NULL,
  [PeriodYear] int NOT NULL,
  [PeriodMonth] int NOT NULL,
  [RunType] nvarchar(10) NOT NULL CONSTRAINT DF_PayrollRuns_RunType DEFAULT ('standard'),
  [ScopeJson] nvarchar(max) NULL,
  [IssueDate] date NOT NULL,
  [Description] nvarchar(500) NULL,
  [Status] nvarchar(20) NOT NULL CONSTRAINT DF_PayrollRuns_Status DEFAULT ('draft'),
  [ApprovedBy] bigint NULL,
  [ApprovedAt] datetimeoffset(0) NULL,
  [PaidAt] datetimeoffset(0) NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_PayrollRuns_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_PayrollRuns_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_PayrollRuns PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[PayrollRunLines] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [RunId] bigint NOT NULL,
  [EmployeeId] bigint NOT NULL,
  [WorkDays] decimal(6,2) NOT NULL CONSTRAINT DF_PayrollRunLines_WorkDays DEFAULT (0),
  [BasicSalary] decimal(18,2) NOT NULL CONSTRAINT DF_PayrollRunLines_BasicSalary DEFAULT (0),
  [TotalEarnings] decimal(18,2) NOT NULL CONSTRAINT DF_PayrollRunLines_TotalEarnings DEFAULT (0),
  [TotalDeductions] decimal(18,2) NOT NULL CONSTRAINT DF_PayrollRunLines_TotalDeductions DEFAULT (0),
  [NetPay] decimal(18,2) NOT NULL CONSTRAINT DF_PayrollRunLines_NetPay DEFAULT (0),
  [Notes] nvarchar(500) NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_PayrollRunLines_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_PayrollRunLines_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_PayrollRunLines PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[PayrollRunLineItems] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [LineId] bigint NOT NULL,
  [PayrollItemId] bigint NOT NULL,
  [ItemType] nvarchar(10) NOT NULL,
  [Amount] decimal(18,2) NOT NULL,
  [Source] nvarchar(20) NOT NULL,
  [SourceRef] bigint NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_PayrollRunLineItems_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_PayrollRunLineItems_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_PayrollRunLineItems PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[SocialInsuranceRuns] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [PeriodYear] int NOT NULL,
  [PeriodMonth] int NOT NULL,
  [IssueDate] date NOT NULL,
  [Description] nvarchar(500) NULL,
  [Status] nvarchar(20) NOT NULL CONSTRAINT DF_SocialInsuranceRuns_Status DEFAULT ('draft'),
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_SocialInsuranceRuns_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_SocialInsuranceRuns_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_SocialInsuranceRuns PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[SocialInsuranceRunLines] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [RunId] bigint NOT NULL,
  [EmployeeId] bigint NOT NULL,
  [InsurableWage] decimal(18,2) NOT NULL,
  [EmployeeShare] decimal(18,2) NOT NULL,
  [CompanyShare] decimal(18,2) NOT NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_SocialInsuranceRunLines_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_SocialInsuranceRunLines_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_SocialInsuranceRunLines PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[EndOfServices] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [DocNo] int NOT NULL,
  [EmployeeId] bigint NOT NULL,
  [EndDate] date NOT NULL,
  [Reason] nvarchar(30) NOT NULL,
  [ServiceYears] decimal(6,2) NULL,
  [LeaveCashAmount] decimal(18,2) NOT NULL CONSTRAINT DF_EndOfServices_LeaveCashAmount DEFAULT (0),
  [GratuityAmount] decimal(18,2) NOT NULL CONSTRAINT DF_EndOfServices_GratuityAmount DEFAULT (0),
  [OtherEarnings] decimal(18,2) NOT NULL CONSTRAINT DF_EndOfServices_OtherEarnings DEFAULT (0),
  [Deductions] decimal(18,2) NOT NULL CONSTRAINT DF_EndOfServices_Deductions DEFAULT (0),
  [NetAmount] decimal(18,2) NOT NULL CONSTRAINT DF_EndOfServices_NetAmount DEFAULT (0),
  [Status] nvarchar(20) NOT NULL CONSTRAINT DF_EndOfServices_Status DEFAULT ('draft'),
  [ApprovedBy] bigint NULL,
  [Notes] nvarchar(1000) NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_EndOfServices_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_EndOfServices_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_EndOfServices PRIMARY KEY CLUSTERED ([Id])
);
GO
CREATE TABLE hr.[Tasks] (
  [Id] bigint IDENTITY(1,1) NOT NULL,
  [CompanyId] bigint NOT NULL,
  [Title] nvarchar(300) NOT NULL,
  [Description] nvarchar(max) NULL,
  [AssignedUserId] bigint NULL,
  [DueAt] datetimeoffset(0) NULL,
  [Recurrence] nvarchar(100) NULL,
  [Status] nvarchar(20) NOT NULL CONSTRAINT DF_Tasks_Status DEFAULT ('open'),
  [RemindBeforeMin] int NULL,
  [CreatedAt] datetimeoffset(0) NOT NULL CONSTRAINT DF_Tasks_CreatedAt DEFAULT (SYSDATETIMEOFFSET()),
  [CreatedBy] bigint NULL,
  [UpdatedAt] datetimeoffset(0) NULL,
  [UpdatedBy] bigint NULL,
  [IsDeleted] bit NOT NULL CONSTRAINT DF_Tasks_IsDeleted DEFAULT (0),
  [RowVersion] rowversion NOT NULL,
  CONSTRAINT PK_Tasks PRIMARY KEY CLUSTERED ([Id])
);
GO

-- ---------- Foreign keys
ALTER TABLE hr.[FiscalYears] ADD CONSTRAINT FK_FiscalYears_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[UserGroups] ADD CONSTRAINT FK_UserGroups_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[Roles] ADD CONSTRAINT FK_Roles_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[Users] ADD CONSTRAINT FK_Users_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[Users] ADD CONSTRAINT FK_Users_EmployeeId FOREIGN KEY ([EmployeeId]) REFERENCES hr.[Employees]([Id]);
ALTER TABLE hr.[Users] ADD CONSTRAINT FK_Users_UserGroupId FOREIGN KEY ([UserGroupId]) REFERENCES hr.[UserGroups]([Id]);
ALTER TABLE hr.[RolePermissions] ADD CONSTRAINT FK_RolePermissions_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[RolePermissions] ADD CONSTRAINT FK_RolePermissions_RoleId FOREIGN KEY ([RoleId]) REFERENCES hr.[Roles]([Id]);
ALTER TABLE hr.[RolePermissions] ADD CONSTRAINT FK_RolePermissions_PermissionId FOREIGN KEY ([PermissionId]) REFERENCES hr.[Permissions]([Id]);
ALTER TABLE hr.[UserRoles] ADD CONSTRAINT FK_UserRoles_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[UserRoles] ADD CONSTRAINT FK_UserRoles_UserId FOREIGN KEY ([UserId]) REFERENCES hr.[Users]([Id]);
ALTER TABLE hr.[UserRoles] ADD CONSTRAINT FK_UserRoles_RoleId FOREIGN KEY ([RoleId]) REFERENCES hr.[Roles]([Id]);
ALTER TABLE hr.[UserGroupRoles] ADD CONSTRAINT FK_UserGroupRoles_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[UserGroupRoles] ADD CONSTRAINT FK_UserGroupRoles_UserGroupId FOREIGN KEY ([UserGroupId]) REFERENCES hr.[UserGroups]([Id]);
ALTER TABLE hr.[UserGroupRoles] ADD CONSTRAINT FK_UserGroupRoles_RoleId FOREIGN KEY ([RoleId]) REFERENCES hr.[Roles]([Id]);
ALTER TABLE hr.[UserDataScopes] ADD CONSTRAINT FK_UserDataScopes_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[UserDataScopes] ADD CONSTRAINT FK_UserDataScopes_UserId FOREIGN KEY ([UserId]) REFERENCES hr.[Users]([Id]);
ALTER TABLE hr.[UserDataScopes] ADD CONSTRAINT FK_UserDataScopes_OrgNodeId FOREIGN KEY ([OrgNodeId]) REFERENCES hr.[OrgNodes]([Id]);
ALTER TABLE hr.[RefreshTokens] ADD CONSTRAINT FK_RefreshTokens_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[RefreshTokens] ADD CONSTRAINT FK_RefreshTokens_UserId FOREIGN KEY ([UserId]) REFERENCES hr.[Users]([Id]);
ALTER TABLE hr.[UserDevices] ADD CONSTRAINT FK_UserDevices_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[UserDevices] ADD CONSTRAINT FK_UserDevices_UserId FOREIGN KEY ([UserId]) REFERENCES hr.[Users]([Id]);
ALTER TABLE hr.[Settings] ADD CONSTRAINT FK_Settings_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[AuditLogs] ADD CONSTRAINT FK_AuditLogs_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[Notifications] ADD CONSTRAINT FK_Notifications_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[Notifications] ADD CONSTRAINT FK_Notifications_UserId FOREIGN KEY ([UserId]) REFERENCES hr.[Users]([Id]);
ALTER TABLE hr.[Attachments] ADD CONSTRAINT FK_Attachments_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[ReportHeaders] ADD CONSTRAINT FK_ReportHeaders_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[ReportViews] ADD CONSTRAINT FK_ReportViews_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[ReportViews] ADD CONSTRAINT FK_ReportViews_UserId FOREIGN KEY ([UserId]) REFERENCES hr.[Users]([Id]);
ALTER TABLE hr.[OrgNodes] ADD CONSTRAINT FK_OrgNodes_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[OrgNodes] ADD CONSTRAINT FK_OrgNodes_ParentId FOREIGN KEY ([ParentId]) REFERENCES hr.[OrgNodes]([Id]);
ALTER TABLE hr.[OrgNodes] ADD CONSTRAINT FK_OrgNodes_ManagerEmployeeId FOREIGN KEY ([ManagerEmployeeId]) REFERENCES hr.[Employees]([Id]);
ALTER TABLE hr.[JobGrades] ADD CONSTRAINT FK_JobGrades_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[Jobs] ADD CONSTRAINT FK_Jobs_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[Jobs] ADD CONSTRAINT FK_Jobs_JobGradeId FOREIGN KEY ([JobGradeId]) REFERENCES hr.[JobGrades]([Id]);
ALTER TABLE hr.[CostCenters] ADD CONSTRAINT FK_CostCenters_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[EmployeeGroups] ADD CONSTRAINT FK_EmployeeGroups_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[Projects] ADD CONSTRAINT FK_Projects_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[Banks] ADD CONSTRAINT FK_Banks_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[Countries] ADD CONSTRAINT FK_Countries_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[Cities] ADD CONSTRAINT FK_Cities_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[Cities] ADD CONSTRAINT FK_Cities_CountryId FOREIGN KEY ([CountryId]) REFERENCES hr.[Countries]([Id]);
ALTER TABLE hr.[InsuranceCompanies] ADD CONSTRAINT FK_InsuranceCompanies_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[DocumentTypes] ADD CONSTRAINT FK_DocumentTypes_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[Sponsors] ADD CONSTRAINT FK_Sponsors_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[Nationalities] ADD CONSTRAINT FK_Nationalities_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[WorkNatures] ADD CONSTRAINT FK_WorkNatures_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[Lookups] ADD CONSTRAINT FK_Lookups_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[GeoLocations] ADD CONSTRAINT FK_GeoLocations_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[GeoLocations] ADD CONSTRAINT FK_GeoLocations_OrgNodeId FOREIGN KEY ([OrgNodeId]) REFERENCES hr.[OrgNodes]([Id]);
ALTER TABLE hr.[Employees] ADD CONSTRAINT FK_Employees_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[Employees] ADD CONSTRAINT FK_Employees_NationalityId FOREIGN KEY ([NationalityId]) REFERENCES hr.[Nationalities]([Id]);
ALTER TABLE hr.[Employees] ADD CONSTRAINT FK_Employees_ReligionId FOREIGN KEY ([ReligionId]) REFERENCES hr.[Lookups]([Id]);
ALTER TABLE hr.[Employees] ADD CONSTRAINT FK_Employees_MaritalStatusId FOREIGN KEY ([MaritalStatusId]) REFERENCES hr.[Lookups]([Id]);
ALTER TABLE hr.[Employees] ADD CONSTRAINT FK_Employees_BirthCountryId FOREIGN KEY ([BirthCountryId]) REFERENCES hr.[Countries]([Id]);
ALTER TABLE hr.[Employees] ADD CONSTRAINT FK_Employees_BirthCityId FOREIGN KEY ([BirthCityId]) REFERENCES hr.[Cities]([Id]);
ALTER TABLE hr.[Employees] ADD CONSTRAINT FK_Employees_OrgNodeId FOREIGN KEY ([OrgNodeId]) REFERENCES hr.[OrgNodes]([Id]);
ALTER TABLE hr.[Employees] ADD CONSTRAINT FK_Employees_JobGradeId FOREIGN KEY ([JobGradeId]) REFERENCES hr.[JobGrades]([Id]);
ALTER TABLE hr.[Employees] ADD CONSTRAINT FK_Employees_JobId FOREIGN KEY ([JobId]) REFERENCES hr.[Jobs]([Id]);
ALTER TABLE hr.[Employees] ADD CONSTRAINT FK_Employees_SponsorId FOREIGN KEY ([SponsorId]) REFERENCES hr.[Sponsors]([Id]);
ALTER TABLE hr.[Employees] ADD CONSTRAINT FK_Employees_EmployeeGroupId FOREIGN KEY ([EmployeeGroupId]) REFERENCES hr.[EmployeeGroups]([Id]);
ALTER TABLE hr.[Employees] ADD CONSTRAINT FK_Employees_CostCenterId FOREIGN KEY ([CostCenterId]) REFERENCES hr.[CostCenters]([Id]);
ALTER TABLE hr.[Employees] ADD CONSTRAINT FK_Employees_ProjectId FOREIGN KEY ([ProjectId]) REFERENCES hr.[Projects]([Id]);
ALTER TABLE hr.[Employees] ADD CONSTRAINT FK_Employees_BankId FOREIGN KEY ([BankId]) REFERENCES hr.[Banks]([Id]);
ALTER TABLE hr.[Employees] ADD CONSTRAINT FK_Employees_WorkNatureId FOREIGN KEY ([WorkNatureId]) REFERENCES hr.[WorkNatures]([Id]);
ALTER TABLE hr.[Employees] ADD CONSTRAINT FK_Employees_ContractTypeId FOREIGN KEY ([ContractTypeId]) REFERENCES hr.[Lookups]([Id]);
ALTER TABLE hr.[Employees] ADD CONSTRAINT FK_Employees_StatusId FOREIGN KEY ([StatusId]) REFERENCES hr.[Lookups]([Id]);
ALTER TABLE hr.[Employees] ADD CONSTRAINT FK_Employees_InsuranceCompanyId FOREIGN KEY ([InsuranceCompanyId]) REFERENCES hr.[InsuranceCompanies]([Id]);
ALTER TABLE hr.[EmployeeSalaryItems] ADD CONSTRAINT FK_EmployeeSalaryItems_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[EmployeeSalaryItems] ADD CONSTRAINT FK_EmployeeSalaryItems_EmployeeId FOREIGN KEY ([EmployeeId]) REFERENCES hr.[Employees]([Id]);
ALTER TABLE hr.[EmployeeSalaryItems] ADD CONSTRAINT FK_EmployeeSalaryItems_PayrollItemId FOREIGN KEY ([PayrollItemId]) REFERENCES hr.[PayrollItems]([Id]);
ALTER TABLE hr.[EmployeeDocuments] ADD CONSTRAINT FK_EmployeeDocuments_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[EmployeeDocuments] ADD CONSTRAINT FK_EmployeeDocuments_EmployeeId FOREIGN KEY ([EmployeeId]) REFERENCES hr.[Employees]([Id]);
ALTER TABLE hr.[EmployeeDocuments] ADD CONSTRAINT FK_EmployeeDocuments_DocumentTypeId FOREIGN KEY ([DocumentTypeId]) REFERENCES hr.[DocumentTypes]([Id]);
ALTER TABLE hr.[EmployeeArchive] ADD CONSTRAINT FK_EmployeeArchive_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[EmployeeArchive] ADD CONSTRAINT FK_EmployeeArchive_EmployeeId FOREIGN KEY ([EmployeeId]) REFERENCES hr.[Employees]([Id]);
ALTER TABLE hr.[EmployeeCompanions] ADD CONSTRAINT FK_EmployeeCompanions_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[EmployeeCompanions] ADD CONSTRAINT FK_EmployeeCompanions_EmployeeId FOREIGN KEY ([EmployeeId]) REFERENCES hr.[Employees]([Id]);
ALTER TABLE hr.[EmployeeCompanions] ADD CONSTRAINT FK_EmployeeCompanions_RelationId FOREIGN KEY ([RelationId]) REFERENCES hr.[Lookups]([Id]);
ALTER TABLE hr.[EmployeeQualifications] ADD CONSTRAINT FK_EmployeeQualifications_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[EmployeeQualifications] ADD CONSTRAINT FK_EmployeeQualifications_EmployeeId FOREIGN KEY ([EmployeeId]) REFERENCES hr.[Employees]([Id]);
ALTER TABLE hr.[EmployeeQualifications] ADD CONSTRAINT FK_EmployeeQualifications_QualificationId FOREIGN KEY ([QualificationId]) REFERENCES hr.[Lookups]([Id]);
ALTER TABLE hr.[EmployeeExperiences] ADD CONSTRAINT FK_EmployeeExperiences_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[EmployeeExperiences] ADD CONSTRAINT FK_EmployeeExperiences_EmployeeId FOREIGN KEY ([EmployeeId]) REFERENCES hr.[Employees]([Id]);
ALTER TABLE hr.[EmployeePortalAccounts] ADD CONSTRAINT FK_EmployeePortalAccounts_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[EmployeePortalAccounts] ADD CONSTRAINT FK_EmployeePortalAccounts_EmployeeId FOREIGN KEY ([EmployeeId]) REFERENCES hr.[Employees]([Id]);
ALTER TABLE hr.[EmployeePortalAccounts] ADD CONSTRAINT FK_EmployeePortalAccounts_UserId FOREIGN KEY ([UserId]) REFERENCES hr.[Users]([Id]);
ALTER TABLE hr.[EmployeeWorkStarts] ADD CONSTRAINT FK_EmployeeWorkStarts_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[EmployeeWorkStarts] ADD CONSTRAINT FK_EmployeeWorkStarts_EmployeeId FOREIGN KEY ([EmployeeId]) REFERENCES hr.[Employees]([Id]);
ALTER TABLE hr.[Shifts] ADD CONSTRAINT FK_Shifts_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[ShiftDays] ADD CONSTRAINT FK_ShiftDays_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[ShiftDays] ADD CONSTRAINT FK_ShiftDays_ShiftId FOREIGN KEY ([ShiftId]) REFERENCES hr.[Shifts]([Id]);
ALTER TABLE hr.[ShiftRotations] ADD CONSTRAINT FK_ShiftRotations_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[ShiftRotationDays] ADD CONSTRAINT FK_ShiftRotationDays_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[ShiftRotationDays] ADD CONSTRAINT FK_ShiftRotationDays_RotationId FOREIGN KEY ([RotationId]) REFERENCES hr.[ShiftRotations]([Id]);
ALTER TABLE hr.[ShiftRotationDays] ADD CONSTRAINT FK_ShiftRotationDays_ShiftId FOREIGN KEY ([ShiftId]) REFERENCES hr.[Shifts]([Id]);
ALTER TABLE hr.[EmployeeShifts] ADD CONSTRAINT FK_EmployeeShifts_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[EmployeeShifts] ADD CONSTRAINT FK_EmployeeShifts_EmployeeId FOREIGN KEY ([EmployeeId]) REFERENCES hr.[Employees]([Id]);
ALTER TABLE hr.[EmployeeShifts] ADD CONSTRAINT FK_EmployeeShifts_ShiftId FOREIGN KEY ([ShiftId]) REFERENCES hr.[Shifts]([Id]);
ALTER TABLE hr.[EmployeeShifts] ADD CONSTRAINT FK_EmployeeShifts_RotationId FOREIGN KEY ([RotationId]) REFERENCES hr.[ShiftRotations]([Id]);
ALTER TABLE hr.[BiometricDevices] ADD CONSTRAINT FK_BiometricDevices_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[BiometricDevices] ADD CONSTRAINT FK_BiometricDevices_OrgNodeId FOREIGN KEY ([OrgNodeId]) REFERENCES hr.[OrgNodes]([Id]);
ALTER TABLE hr.[PunchLogs] ADD CONSTRAINT FK_PunchLogs_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[PunchLogs] ADD CONSTRAINT FK_PunchLogs_DeviceId FOREIGN KEY ([DeviceId]) REFERENCES hr.[BiometricDevices]([Id]);
ALTER TABLE hr.[PunchLogs] ADD CONSTRAINT FK_PunchLogs_EmployeeId FOREIGN KEY ([EmployeeId]) REFERENCES hr.[Employees]([Id]);
ALTER TABLE hr.[PunchLogs] ADD CONSTRAINT FK_PunchLogs_GeoLocationId FOREIGN KEY ([GeoLocationId]) REFERENCES hr.[GeoLocations]([Id]);
ALTER TABLE hr.[OfficialHolidays] ADD CONSTRAINT FK_OfficialHolidays_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[OfficialHolidays] ADD CONSTRAINT FK_OfficialHolidays_OrgNodeId FOREIGN KEY ([OrgNodeId]) REFERENCES hr.[OrgNodes]([Id]);
ALTER TABLE hr.[ExcuseTypes] ADD CONSTRAINT FK_ExcuseTypes_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[Excuses] ADD CONSTRAINT FK_Excuses_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[Excuses] ADD CONSTRAINT FK_Excuses_ExcuseTypeId FOREIGN KEY ([ExcuseTypeId]) REFERENCES hr.[ExcuseTypes]([Id]);
ALTER TABLE hr.[Excuses] ADD CONSTRAINT FK_Excuses_EmployeeId FOREIGN KEY ([EmployeeId]) REFERENCES hr.[Employees]([Id]);
ALTER TABLE hr.[Excuses] ADD CONSTRAINT FK_Excuses_RequestId FOREIGN KEY ([RequestId]) REFERENCES hr.[Requests]([Id]);
ALTER TABLE hr.[ExcuseShifts] ADD CONSTRAINT FK_ExcuseShifts_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[ExcuseShifts] ADD CONSTRAINT FK_ExcuseShifts_ExcuseId FOREIGN KEY ([ExcuseId]) REFERENCES hr.[Excuses]([Id]);
ALTER TABLE hr.[ExcuseShifts] ADD CONSTRAINT FK_ExcuseShifts_ShiftId FOREIGN KEY ([ShiftId]) REFERENCES hr.[Shifts]([Id]);
ALTER TABLE hr.[MonthlyAttendances] ADD CONSTRAINT FK_MonthlyAttendances_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[MonthlyAttendances] ADD CONSTRAINT FK_MonthlyAttendances_FiscalYearId FOREIGN KEY ([FiscalYearId]) REFERENCES hr.[FiscalYears]([Id]);
ALTER TABLE hr.[MonthlyAttendanceLines] ADD CONSTRAINT FK_MonthlyAttendanceLines_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[MonthlyAttendanceLines] ADD CONSTRAINT FK_MonthlyAttendanceLines_MonthlyId FOREIGN KEY ([MonthlyId]) REFERENCES hr.[MonthlyAttendances]([Id]);
ALTER TABLE hr.[MonthlyAttendanceLines] ADD CONSTRAINT FK_MonthlyAttendanceLines_EmployeeId FOREIGN KEY ([EmployeeId]) REFERENCES hr.[Employees]([Id]);
ALTER TABLE hr.[DailyAttendance] ADD CONSTRAINT FK_DailyAttendance_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[DailyAttendance] ADD CONSTRAINT FK_DailyAttendance_EmployeeId FOREIGN KEY ([EmployeeId]) REFERENCES hr.[Employees]([Id]);
ALTER TABLE hr.[DailyAttendance] ADD CONSTRAINT FK_DailyAttendance_ShiftId FOREIGN KEY ([ShiftId]) REFERENCES hr.[Shifts]([Id]);
ALTER TABLE hr.[DailyAttendance] ADD CONSTRAINT FK_DailyAttendance_LeaveId FOREIGN KEY ([LeaveId]) REFERENCES hr.[Leaves]([Id]);
ALTER TABLE hr.[DailyAttendance] ADD CONSTRAINT FK_DailyAttendance_ExcuseId FOREIGN KEY ([ExcuseId]) REFERENCES hr.[Excuses]([Id]);
ALTER TABLE hr.[DailyAttendance] ADD CONSTRAINT FK_DailyAttendance_HolidayId FOREIGN KEY ([HolidayId]) REFERENCES hr.[OfficialHolidays]([Id]);
ALTER TABLE hr.[DailyAttendance] ADD CONSTRAINT FK_DailyAttendance_MonthlyId FOREIGN KEY ([MonthlyId]) REFERENCES hr.[MonthlyAttendances]([Id]);
ALTER TABLE hr.[LeaveTypes] ADD CONSTRAINT FK_LeaveTypes_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[LeavePolicies] ADD CONSTRAINT FK_LeavePolicies_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[LeavePolicies] ADD CONSTRAINT FK_LeavePolicies_LeaveTypeId FOREIGN KEY ([LeaveTypeId]) REFERENCES hr.[LeaveTypes]([Id]);
ALTER TABLE hr.[LeavePolicies] ADD CONSTRAINT FK_LeavePolicies_EmployeeGroupId FOREIGN KEY ([EmployeeGroupId]) REFERENCES hr.[EmployeeGroups]([Id]);
ALTER TABLE hr.[LeaveBalances] ADD CONSTRAINT FK_LeaveBalances_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[LeaveBalances] ADD CONSTRAINT FK_LeaveBalances_EmployeeId FOREIGN KEY ([EmployeeId]) REFERENCES hr.[Employees]([Id]);
ALTER TABLE hr.[LeaveBalances] ADD CONSTRAINT FK_LeaveBalances_LeaveTypeId FOREIGN KEY ([LeaveTypeId]) REFERENCES hr.[LeaveTypes]([Id]);
ALTER TABLE hr.[LeaveLedger] ADD CONSTRAINT FK_LeaveLedger_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[LeaveLedger] ADD CONSTRAINT FK_LeaveLedger_EmployeeId FOREIGN KEY ([EmployeeId]) REFERENCES hr.[Employees]([Id]);
ALTER TABLE hr.[LeaveLedger] ADD CONSTRAINT FK_LeaveLedger_LeaveTypeId FOREIGN KEY ([LeaveTypeId]) REFERENCES hr.[LeaveTypes]([Id]);
ALTER TABLE hr.[LeaveLedger] ADD CONSTRAINT FK_LeaveLedger_LeaveId FOREIGN KEY ([LeaveId]) REFERENCES hr.[Leaves]([Id]);
ALTER TABLE hr.[Leaves] ADD CONSTRAINT FK_Leaves_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[Leaves] ADD CONSTRAINT FK_Leaves_EmployeeId FOREIGN KEY ([EmployeeId]) REFERENCES hr.[Employees]([Id]);
ALTER TABLE hr.[Leaves] ADD CONSTRAINT FK_Leaves_LeaveTypeId FOREIGN KEY ([LeaveTypeId]) REFERENCES hr.[LeaveTypes]([Id]);
ALTER TABLE hr.[Leaves] ADD CONSTRAINT FK_Leaves_SubstituteEmployeeId FOREIGN KEY ([SubstituteEmployeeId]) REFERENCES hr.[Employees]([Id]);
ALTER TABLE hr.[Leaves] ADD CONSTRAINT FK_Leaves_RequestId FOREIGN KEY ([RequestId]) REFERENCES hr.[Requests]([Id]);
ALTER TABLE hr.[ApprovalFlows] ADD CONSTRAINT FK_ApprovalFlows_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[ApprovalFlowSteps] ADD CONSTRAINT FK_ApprovalFlowSteps_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[ApprovalFlowSteps] ADD CONSTRAINT FK_ApprovalFlowSteps_FlowId FOREIGN KEY ([FlowId]) REFERENCES hr.[ApprovalFlows]([Id]);
ALTER TABLE hr.[ApprovalFlowSteps] ADD CONSTRAINT FK_ApprovalFlowSteps_RoleId FOREIGN KEY ([RoleId]) REFERENCES hr.[Roles]([Id]);
ALTER TABLE hr.[ApprovalFlowSteps] ADD CONSTRAINT FK_ApprovalFlowSteps_ApproverUserId FOREIGN KEY ([ApproverUserId]) REFERENCES hr.[Users]([Id]);
ALTER TABLE hr.[Requests] ADD CONSTRAINT FK_Requests_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[Requests] ADD CONSTRAINT FK_Requests_EmployeeId FOREIGN KEY ([EmployeeId]) REFERENCES hr.[Employees]([Id]);
ALTER TABLE hr.[Requests] ADD CONSTRAINT FK_Requests_FlowId FOREIGN KEY ([FlowId]) REFERENCES hr.[ApprovalFlows]([Id]);
ALTER TABLE hr.[RequestSteps] ADD CONSTRAINT FK_RequestSteps_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[RequestSteps] ADD CONSTRAINT FK_RequestSteps_RequestId FOREIGN KEY ([RequestId]) REFERENCES hr.[Requests]([Id]);
ALTER TABLE hr.[RequestSteps] ADD CONSTRAINT FK_RequestSteps_ApproverUserId FOREIGN KEY ([ApproverUserId]) REFERENCES hr.[Users]([Id]);
ALTER TABLE hr.[RequestSteps] ADD CONSTRAINT FK_RequestSteps_DelegatedFromUserId FOREIGN KEY ([DelegatedFromUserId]) REFERENCES hr.[Users]([Id]);
ALTER TABLE hr.[RequestComments] ADD CONSTRAINT FK_RequestComments_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[RequestComments] ADD CONSTRAINT FK_RequestComments_RequestId FOREIGN KEY ([RequestId]) REFERENCES hr.[Requests]([Id]);
ALTER TABLE hr.[RequestComments] ADD CONSTRAINT FK_RequestComments_UserId FOREIGN KEY ([UserId]) REFERENCES hr.[Users]([Id]);
ALTER TABLE hr.[Delegations] ADD CONSTRAINT FK_Delegations_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[Delegations] ADD CONSTRAINT FK_Delegations_FromUserId FOREIGN KEY ([FromUserId]) REFERENCES hr.[Users]([Id]);
ALTER TABLE hr.[Delegations] ADD CONSTRAINT FK_Delegations_ToUserId FOREIGN KEY ([ToUserId]) REFERENCES hr.[Users]([Id]);
ALTER TABLE hr.[PayrollItems] ADD CONSTRAINT FK_PayrollItems_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[VariableItems] ADD CONSTRAINT FK_VariableItems_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[VariableItems] ADD CONSTRAINT FK_VariableItems_EmployeeId FOREIGN KEY ([EmployeeId]) REFERENCES hr.[Employees]([Id]);
ALTER TABLE hr.[VariableItems] ADD CONSTRAINT FK_VariableItems_PayrollItemId FOREIGN KEY ([PayrollItemId]) REFERENCES hr.[PayrollItems]([Id]);
ALTER TABLE hr.[VariableItems] ADD CONSTRAINT FK_VariableItems_PayrollRunId FOREIGN KEY ([PayrollRunId]) REFERENCES hr.[PayrollRuns]([Id]);
ALTER TABLE hr.[Advances] ADD CONSTRAINT FK_Advances_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[Advances] ADD CONSTRAINT FK_Advances_EmployeeId FOREIGN KEY ([EmployeeId]) REFERENCES hr.[Employees]([Id]);
ALTER TABLE hr.[Advances] ADD CONSTRAINT FK_Advances_RequestId FOREIGN KEY ([RequestId]) REFERENCES hr.[Requests]([Id]);
ALTER TABLE hr.[AdvanceInstallments] ADD CONSTRAINT FK_AdvanceInstallments_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[AdvanceInstallments] ADD CONSTRAINT FK_AdvanceInstallments_AdvanceId FOREIGN KEY ([AdvanceId]) REFERENCES hr.[Advances]([Id]);
ALTER TABLE hr.[AdvanceInstallments] ADD CONSTRAINT FK_AdvanceInstallments_PayrollRunLineId FOREIGN KEY ([PayrollRunLineId]) REFERENCES hr.[PayrollRunLines]([Id]);
ALTER TABLE hr.[AdvanceRepayments] ADD CONSTRAINT FK_AdvanceRepayments_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[AdvanceRepayments] ADD CONSTRAINT FK_AdvanceRepayments_AdvanceId FOREIGN KEY ([AdvanceId]) REFERENCES hr.[Advances]([Id]);
ALTER TABLE hr.[Custodies] ADD CONSTRAINT FK_Custodies_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[Custodies] ADD CONSTRAINT FK_Custodies_EmployeeId FOREIGN KEY ([EmployeeId]) REFERENCES hr.[Employees]([Id]);
ALTER TABLE hr.[Custodies] ADD CONSTRAINT FK_Custodies_RequestId FOREIGN KEY ([RequestId]) REFERENCES hr.[Requests]([Id]);
ALTER TABLE hr.[PayrollRuns] ADD CONSTRAINT FK_PayrollRuns_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[PayrollRunLines] ADD CONSTRAINT FK_PayrollRunLines_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[PayrollRunLines] ADD CONSTRAINT FK_PayrollRunLines_RunId FOREIGN KEY ([RunId]) REFERENCES hr.[PayrollRuns]([Id]);
ALTER TABLE hr.[PayrollRunLines] ADD CONSTRAINT FK_PayrollRunLines_EmployeeId FOREIGN KEY ([EmployeeId]) REFERENCES hr.[Employees]([Id]);
ALTER TABLE hr.[PayrollRunLineItems] ADD CONSTRAINT FK_PayrollRunLineItems_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[PayrollRunLineItems] ADD CONSTRAINT FK_PayrollRunLineItems_LineId FOREIGN KEY ([LineId]) REFERENCES hr.[PayrollRunLines]([Id]);
ALTER TABLE hr.[PayrollRunLineItems] ADD CONSTRAINT FK_PayrollRunLineItems_PayrollItemId FOREIGN KEY ([PayrollItemId]) REFERENCES hr.[PayrollItems]([Id]);
ALTER TABLE hr.[SocialInsuranceRuns] ADD CONSTRAINT FK_SocialInsuranceRuns_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[SocialInsuranceRunLines] ADD CONSTRAINT FK_SocialInsuranceRunLines_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[SocialInsuranceRunLines] ADD CONSTRAINT FK_SocialInsuranceRunLines_RunId FOREIGN KEY ([RunId]) REFERENCES hr.[SocialInsuranceRuns]([Id]);
ALTER TABLE hr.[SocialInsuranceRunLines] ADD CONSTRAINT FK_SocialInsuranceRunLines_EmployeeId FOREIGN KEY ([EmployeeId]) REFERENCES hr.[Employees]([Id]);
ALTER TABLE hr.[EndOfServices] ADD CONSTRAINT FK_EndOfServices_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[EndOfServices] ADD CONSTRAINT FK_EndOfServices_EmployeeId FOREIGN KEY ([EmployeeId]) REFERENCES hr.[Employees]([Id]);
ALTER TABLE hr.[Tasks] ADD CONSTRAINT FK_Tasks_CompanyId FOREIGN KEY ([CompanyId]) REFERENCES hr.[Companies]([Id]);
ALTER TABLE hr.[Tasks] ADD CONSTRAINT FK_Tasks_AssignedUserId FOREIGN KEY ([AssignedUserId]) REFERENCES hr.[Users]([Id]);
GO

-- ---------- Check constraints
ALTER TABLE hr.[Employees] ADD CONSTRAINT CK_Employees_Gender CHECK ([Gender] IN ('M','F'));
ALTER TABLE hr.[OrgNodes] ADD CONSTRAINT CK_OrgNodes_NodeType CHECK ([NodeType] IN ('root','region','general_admin','company','branch','department','section'));
ALTER TABLE hr.[Users] ADD CONSTRAINT CK_Users_UserType CHECK ([UserType] IN ('normal','manager'));
ALTER TABLE hr.[Shifts] ADD CONSTRAINT CK_Shifts_CycleType CHECK ([CycleType] IN ('weekly','rotation'));
ALTER TABLE hr.[Shifts] ADD CONSTRAINT CK_Shifts_PunchMode CHECK ([PunchMode] IN ('inout','inonly','outonly','any','flexible_hours'));
ALTER TABLE hr.[BiometricDevices] ADD CONSTRAINT CK_BiometricDevices_ConnectionType CHECK ([ConnectionType] IN ('ip','domain'));
ALTER TABLE hr.[PunchLogs] ADD CONSTRAINT CK_PunchLogs_Source CHECK ([Source] IN ('device','manual','admin','mobile','import'));
ALTER TABLE hr.[PunchLogs] ADD CONSTRAINT CK_PunchLogs_Direction CHECK ([Direction] IN ('in','out','unknown'));
ALTER TABLE hr.[PunchLogs] ADD CONSTRAINT CK_PunchLogs_Status CHECK ([Status] IN ('accepted','pending','rejected','voided'));
ALTER TABLE hr.[DailyAttendance] ADD CONSTRAINT CK_DailyAttendance_Status CHECK ([Status] IN ('present','absent','leave','holiday','holiday_work','weekend','excused'));
ALTER TABLE hr.[OfficialHolidays] ADD CONSTRAINT CK_OfficialHolidays_CalendarType CHECK ([CalendarType] IN ('gregorian','hijri'));
ALTER TABLE hr.[LeaveLedger] ADD CONSTRAINT CK_LeaveLedger_EntryType CHECK ([EntryType] IN ('opening','accrual','usage','adjust','encash','recall','expire'));
ALTER TABLE hr.[Leaves] ADD CONSTRAINT CK_Leaves_Status CHECK ([Status] IN ('draft','pending','approved','rejected','cancelled'));
ALTER TABLE hr.[Requests] ADD CONSTRAINT CK_Requests_RequestType CHECK ([RequestType] IN ('leave','advance','custody','excuse','punch_fix','other'));
ALTER TABLE hr.[Requests] ADD CONSTRAINT CK_Requests_Status CHECK ([Status] IN ('draft','submitted','in_review','approved','rejected','cancelled'));
ALTER TABLE hr.[RequestSteps] ADD CONSTRAINT CK_RequestSteps_Decision CHECK ([Decision] IN ('pending','approved','rejected','skipped'));
ALTER TABLE hr.[ApprovalFlowSteps] ADD CONSTRAINT CK_ApprovalFlowSteps_ApproverKind CHECK ([ApproverKind] IN ('direct_manager','node_manager','role','user'));
ALTER TABLE hr.[PayrollItems] ADD CONSTRAINT CK_PayrollItems_ItemType CHECK ([ItemType] IN ('earning','deduction'));
ALTER TABLE hr.[PayrollItems] ADD CONSTRAINT CK_PayrollItems_CalcMethod CHECK ([CalcMethod] IN ('fixed','pct_basic','pct_gross'));
ALTER TABLE hr.[PayrollRunLineItems] ADD CONSTRAINT CK_PayrollRunLineItems_ItemType CHECK ([ItemType] IN ('earning','deduction'));
ALTER TABLE hr.[PayrollRuns] ADD CONSTRAINT CK_PayrollRuns_Status CHECK ([Status] IN ('draft','review','approved','paid'));
ALTER TABLE hr.[PayrollRuns] ADD CONSTRAINT CK_PayrollRuns_RunType CHECK ([RunType] IN ('standard','custom'));
ALTER TABLE hr.[AdvanceInstallments] ADD CONSTRAINT CK_AdvanceInstallments_Status CHECK ([Status] IN ('pending','deducted','settled'));
ALTER TABLE hr.[Advances] ADD CONSTRAINT CK_Advances_Status CHECK ([Status] IN ('active','settled','cancelled'));
ALTER TABLE hr.[Custodies] ADD CONSTRAINT CK_Custodies_Status CHECK ([Status] IN ('delivered','returned','lost'));
ALTER TABLE hr.[Custodies] ADD CONSTRAINT CK_Custodies_ItemCondition CHECK ([ItemCondition] IN ('good','damaged','lost'));
ALTER TABLE hr.[LeaveTypes] ADD CONSTRAINT CK_LeaveTypes_AccrualMode CHECK ([AccrualMode] IN ('monthly','yearly','none'));
ALTER TABLE hr.[SocialInsuranceRuns] ADD CONSTRAINT CK_SocialInsuranceRuns_Status CHECK ([Status] IN ('draft','approved'));
ALTER TABLE hr.[EndOfServices] ADD CONSTRAINT CK_EndOfServices_Status CHECK ([Status] IN ('draft','approved','paid'));
ALTER TABLE hr.[EndOfServices] ADD CONSTRAINT CK_EndOfServices_Reason CHECK ([Reason] IN ('resignation','termination','contract_end','retirement','death'));
ALTER TABLE hr.[Tasks] ADD CONSTRAINT CK_Tasks_Status CHECK ([Status] IN ('open','done','cancelled'));
GO

-- ---------- Indexes
CREATE UNIQUE INDEX UX_Companies_Code ON hr.[Companies] ([Code]) WHERE [IsDeleted] = 0;
CREATE UNIQUE INDEX UX_FiscalYears_FiscalYear ON hr.[FiscalYears] ([CompanyId], [FiscalYear]) WHERE [IsDeleted] = 0;
CREATE UNIQUE INDEX UX_UserGroups_Code ON hr.[UserGroups] ([CompanyId], [Code]) WHERE [IsDeleted] = 0;
CREATE UNIQUE INDEX UX_Roles_Code ON hr.[Roles] ([CompanyId], [Code]) WHERE [IsDeleted] = 0;
CREATE UNIQUE INDEX UX_Users_Username ON hr.[Users] ([CompanyId], [Username]) WHERE [IsDeleted] = 0;
CREATE UNIQUE INDEX UX_Users_EmployeeId ON hr.[Users] ([CompanyId], [EmployeeId]) WHERE [IsDeleted] = 0 AND [EmployeeId] IS NOT NULL;
CREATE INDEX IX_Users_EmployeeId ON hr.[Users] ([EmployeeId]);
CREATE INDEX IX_Users_UserGroupId ON hr.[Users] ([UserGroupId]);
CREATE UNIQUE INDEX UX_Permissions_PermCode ON hr.[Permissions] ([PermCode]) WHERE [IsDeleted] = 0;
CREATE UNIQUE INDEX UX_RolePermissions_RoleId_PermissionId ON hr.[RolePermissions] ([CompanyId], [RoleId], [PermissionId]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_RolePermissions_RoleId ON hr.[RolePermissions] ([RoleId]);
CREATE INDEX IX_RolePermissions_PermissionId ON hr.[RolePermissions] ([PermissionId]);
CREATE UNIQUE INDEX UX_UserRoles_UserId_RoleId ON hr.[UserRoles] ([CompanyId], [UserId], [RoleId]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_UserRoles_UserId ON hr.[UserRoles] ([UserId]);
CREATE INDEX IX_UserRoles_RoleId ON hr.[UserRoles] ([RoleId]);
CREATE UNIQUE INDEX UX_UserGroupRoles_UserGroupId_RoleId ON hr.[UserGroupRoles] ([CompanyId], [UserGroupId], [RoleId]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_UserGroupRoles_UserGroupId ON hr.[UserGroupRoles] ([UserGroupId]);
CREATE INDEX IX_UserGroupRoles_RoleId ON hr.[UserGroupRoles] ([RoleId]);
CREATE UNIQUE INDEX UX_UserDataScopes_UserId_OrgNodeId ON hr.[UserDataScopes] ([CompanyId], [UserId], [OrgNodeId]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_UserDataScopes_UserId ON hr.[UserDataScopes] ([UserId]);
CREATE INDEX IX_UserDataScopes_OrgNodeId ON hr.[UserDataScopes] ([OrgNodeId]);
CREATE INDEX IX_RefreshTokens_UserId ON hr.[RefreshTokens] ([UserId]);
CREATE UNIQUE INDEX UX_UserDevices_UserId_DeviceUid ON hr.[UserDevices] ([CompanyId], [UserId], [DeviceUid]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_UserDevices_UserId ON hr.[UserDevices] ([UserId]);
CREATE UNIQUE INDEX UX_Settings_SettingKey ON hr.[Settings] ([CompanyId], [SettingKey]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_AuditLogs_EntityName_EntityId ON hr.[AuditLogs] ([EntityName], [EntityId]);
CREATE INDEX IX_Notifications_UserId ON hr.[Notifications] ([UserId]);
CREATE INDEX IX_Attachments_OwnerType_OwnerId ON hr.[Attachments] ([OwnerType], [OwnerId]);
CREATE UNIQUE INDEX UX_ReportHeaders_ReportKey ON hr.[ReportHeaders] ([CompanyId], [ReportKey]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_ReportViews_UserId ON hr.[ReportViews] ([UserId]);
CREATE UNIQUE INDEX UX_OrgNodes_Code ON hr.[OrgNodes] ([CompanyId], [Code]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_OrgNodes_ParentId ON hr.[OrgNodes] ([ParentId]);
CREATE INDEX IX_OrgNodes_ManagerEmployeeId ON hr.[OrgNodes] ([ManagerEmployeeId]);
CREATE UNIQUE INDEX UX_JobGrades_Code ON hr.[JobGrades] ([CompanyId], [Code]) WHERE [IsDeleted] = 0;
CREATE UNIQUE INDEX UX_Jobs_Code ON hr.[Jobs] ([CompanyId], [Code]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_Jobs_JobGradeId ON hr.[Jobs] ([JobGradeId]);
CREATE UNIQUE INDEX UX_CostCenters_Code ON hr.[CostCenters] ([CompanyId], [Code]) WHERE [IsDeleted] = 0;
CREATE UNIQUE INDEX UX_EmployeeGroups_Code ON hr.[EmployeeGroups] ([CompanyId], [Code]) WHERE [IsDeleted] = 0;
CREATE UNIQUE INDEX UX_Projects_Code ON hr.[Projects] ([CompanyId], [Code]) WHERE [IsDeleted] = 0;
CREATE UNIQUE INDEX UX_Banks_Code ON hr.[Banks] ([CompanyId], [Code]) WHERE [IsDeleted] = 0;
CREATE UNIQUE INDEX UX_Countries_Code ON hr.[Countries] ([CompanyId], [Code]) WHERE [IsDeleted] = 0;
CREATE UNIQUE INDEX UX_Cities_Code ON hr.[Cities] ([CompanyId], [Code]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_Cities_CountryId ON hr.[Cities] ([CountryId]);
CREATE UNIQUE INDEX UX_InsuranceCompanies_Code ON hr.[InsuranceCompanies] ([CompanyId], [Code]) WHERE [IsDeleted] = 0;
CREATE UNIQUE INDEX UX_DocumentTypes_Code ON hr.[DocumentTypes] ([CompanyId], [Code]) WHERE [IsDeleted] = 0;
CREATE UNIQUE INDEX UX_Sponsors_Code ON hr.[Sponsors] ([CompanyId], [Code]) WHERE [IsDeleted] = 0;
CREATE UNIQUE INDEX UX_Nationalities_Code ON hr.[Nationalities] ([CompanyId], [Code]) WHERE [IsDeleted] = 0;
CREATE UNIQUE INDEX UX_WorkNatures_Code ON hr.[WorkNatures] ([CompanyId], [Code]) WHERE [IsDeleted] = 0;
CREATE UNIQUE INDEX UX_Lookups_LookupType_Code ON hr.[Lookups] ([CompanyId], [LookupType], [Code]) WHERE [IsDeleted] = 0;
CREATE UNIQUE INDEX UX_GeoLocations_Code ON hr.[GeoLocations] ([CompanyId], [Code]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_GeoLocations_OrgNodeId ON hr.[GeoLocations] ([OrgNodeId]);
CREATE UNIQUE INDEX UX_Employees_EmployeeNo ON hr.[Employees] ([CompanyId], [EmployeeNo]) WHERE [IsDeleted] = 0;
CREATE UNIQUE INDEX UX_Employees_FingerprintNo ON hr.[Employees] ([CompanyId], [FingerprintNo]) WHERE [IsDeleted] = 0 AND [FingerprintNo] IS NOT NULL;
CREATE INDEX IX_Employees_OrgNodeId_StatusId ON hr.[Employees] ([OrgNodeId], [StatusId]);
CREATE INDEX IX_Employees_NationalityId ON hr.[Employees] ([NationalityId]);
CREATE INDEX IX_Employees_ReligionId ON hr.[Employees] ([ReligionId]);
CREATE INDEX IX_Employees_MaritalStatusId ON hr.[Employees] ([MaritalStatusId]);
CREATE INDEX IX_Employees_BirthCountryId ON hr.[Employees] ([BirthCountryId]);
CREATE INDEX IX_Employees_BirthCityId ON hr.[Employees] ([BirthCityId]);
CREATE INDEX IX_Employees_OrgNodeId ON hr.[Employees] ([OrgNodeId]);
CREATE INDEX IX_Employees_JobGradeId ON hr.[Employees] ([JobGradeId]);
CREATE INDEX IX_Employees_JobId ON hr.[Employees] ([JobId]);
CREATE INDEX IX_Employees_SponsorId ON hr.[Employees] ([SponsorId]);
CREATE INDEX IX_Employees_EmployeeGroupId ON hr.[Employees] ([EmployeeGroupId]);
CREATE INDEX IX_Employees_CostCenterId ON hr.[Employees] ([CostCenterId]);
CREATE INDEX IX_Employees_ProjectId ON hr.[Employees] ([ProjectId]);
CREATE INDEX IX_Employees_BankId ON hr.[Employees] ([BankId]);
CREATE INDEX IX_Employees_WorkNatureId ON hr.[Employees] ([WorkNatureId]);
CREATE INDEX IX_Employees_ContractTypeId ON hr.[Employees] ([ContractTypeId]);
CREATE INDEX IX_Employees_StatusId ON hr.[Employees] ([StatusId]);
CREATE INDEX IX_Employees_InsuranceCompanyId ON hr.[Employees] ([InsuranceCompanyId]);
CREATE UNIQUE INDEX UX_EmployeeSalaryItems_EmployeeId_PayrollItemId ON hr.[EmployeeSalaryItems] ([CompanyId], [EmployeeId], [PayrollItemId]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_EmployeeSalaryItems_EmployeeId ON hr.[EmployeeSalaryItems] ([EmployeeId]);
CREATE INDEX IX_EmployeeSalaryItems_PayrollItemId ON hr.[EmployeeSalaryItems] ([PayrollItemId]);
CREATE INDEX IX_EmployeeDocuments_EmployeeId ON hr.[EmployeeDocuments] ([EmployeeId]);
CREATE INDEX IX_EmployeeDocuments_DocumentTypeId ON hr.[EmployeeDocuments] ([DocumentTypeId]);
CREATE INDEX IX_EmployeeArchive_EmployeeId ON hr.[EmployeeArchive] ([EmployeeId]);
CREATE INDEX IX_EmployeeCompanions_EmployeeId ON hr.[EmployeeCompanions] ([EmployeeId]);
CREATE INDEX IX_EmployeeCompanions_RelationId ON hr.[EmployeeCompanions] ([RelationId]);
CREATE INDEX IX_EmployeeQualifications_EmployeeId ON hr.[EmployeeQualifications] ([EmployeeId]);
CREATE INDEX IX_EmployeeQualifications_QualificationId ON hr.[EmployeeQualifications] ([QualificationId]);
CREATE INDEX IX_EmployeeExperiences_EmployeeId ON hr.[EmployeeExperiences] ([EmployeeId]);
CREATE UNIQUE INDEX UX_EmployeePortalAccounts_EmployeeId_UserId ON hr.[EmployeePortalAccounts] ([CompanyId], [EmployeeId], [UserId]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_EmployeePortalAccounts_EmployeeId ON hr.[EmployeePortalAccounts] ([EmployeeId]);
CREATE INDEX IX_EmployeePortalAccounts_UserId ON hr.[EmployeePortalAccounts] ([UserId]);
CREATE INDEX IX_EmployeeWorkStarts_EmployeeId ON hr.[EmployeeWorkStarts] ([EmployeeId]);
CREATE UNIQUE INDEX UX_Shifts_Code ON hr.[Shifts] ([CompanyId], [Code]) WHERE [IsDeleted] = 0;
CREATE UNIQUE INDEX UX_ShiftDays_ShiftId_DayOfWeek ON hr.[ShiftDays] ([CompanyId], [ShiftId], [DayOfWeek]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_ShiftDays_ShiftId ON hr.[ShiftDays] ([ShiftId]);
CREATE UNIQUE INDEX UX_ShiftRotations_Code ON hr.[ShiftRotations] ([CompanyId], [Code]) WHERE [IsDeleted] = 0;
CREATE UNIQUE INDEX UX_ShiftRotationDays_RotationId_DayIndex ON hr.[ShiftRotationDays] ([CompanyId], [RotationId], [DayIndex]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_ShiftRotationDays_RotationId ON hr.[ShiftRotationDays] ([RotationId]);
CREATE INDEX IX_ShiftRotationDays_ShiftId ON hr.[ShiftRotationDays] ([ShiftId]);
CREATE INDEX IX_EmployeeShifts_EmployeeId_EffectiveFrom ON hr.[EmployeeShifts] ([EmployeeId], [EffectiveFrom]);
CREATE INDEX IX_EmployeeShifts_EmployeeId ON hr.[EmployeeShifts] ([EmployeeId]);
CREATE INDEX IX_EmployeeShifts_ShiftId ON hr.[EmployeeShifts] ([ShiftId]);
CREATE INDEX IX_EmployeeShifts_RotationId ON hr.[EmployeeShifts] ([RotationId]);
CREATE UNIQUE INDEX UX_BiometricDevices_SerialNumber ON hr.[BiometricDevices] ([CompanyId], [SerialNumber]) WHERE [IsDeleted] = 0 AND [SerialNumber] IS NOT NULL;
CREATE UNIQUE INDEX UX_BiometricDevices_Code ON hr.[BiometricDevices] ([CompanyId], [Code]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_BiometricDevices_OrgNodeId ON hr.[BiometricDevices] ([OrgNodeId]);
CREATE UNIQUE INDEX UX_PunchLogs_ClientId ON hr.[PunchLogs] ([CompanyId], [ClientId]) WHERE [IsDeleted] = 0 AND [ClientId] IS NOT NULL;
CREATE UNIQUE INDEX UX_PunchLogs_DeviceId_FingerprintNo_PunchTime ON hr.[PunchLogs] ([CompanyId], [DeviceId], [FingerprintNo], [PunchTime]) WHERE [IsDeleted] = 0 AND [DeviceId] IS NOT NULL AND [FingerprintNo] IS NOT NULL;
CREATE INDEX IX_PunchLogs_EmployeeId_PunchTime ON hr.[PunchLogs] ([EmployeeId], [PunchTime]);
CREATE INDEX IX_PunchLogs_DeviceId ON hr.[PunchLogs] ([DeviceId]);
CREATE INDEX IX_PunchLogs_EmployeeId ON hr.[PunchLogs] ([EmployeeId]);
CREATE INDEX IX_PunchLogs_GeoLocationId ON hr.[PunchLogs] ([GeoLocationId]);
CREATE UNIQUE INDEX UX_OfficialHolidays_Code ON hr.[OfficialHolidays] ([CompanyId], [Code]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_OfficialHolidays_OrgNodeId ON hr.[OfficialHolidays] ([OrgNodeId]);
CREATE UNIQUE INDEX UX_ExcuseTypes_Code ON hr.[ExcuseTypes] ([CompanyId], [Code]) WHERE [IsDeleted] = 0;
CREATE UNIQUE INDEX UX_Excuses_DocNo ON hr.[Excuses] ([CompanyId], [DocNo]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_Excuses_ExcuseTypeId ON hr.[Excuses] ([ExcuseTypeId]);
CREATE INDEX IX_Excuses_EmployeeId ON hr.[Excuses] ([EmployeeId]);
CREATE INDEX IX_Excuses_RequestId ON hr.[Excuses] ([RequestId]);
CREATE UNIQUE INDEX UX_ExcuseShifts_ExcuseId_ShiftId ON hr.[ExcuseShifts] ([CompanyId], [ExcuseId], [ShiftId]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_ExcuseShifts_ExcuseId ON hr.[ExcuseShifts] ([ExcuseId]);
CREATE INDEX IX_ExcuseShifts_ShiftId ON hr.[ExcuseShifts] ([ShiftId]);
CREATE UNIQUE INDEX UX_MonthlyAttendances_FiscalYearId_PeriodMonth ON hr.[MonthlyAttendances] ([CompanyId], [FiscalYearId], [PeriodMonth]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_MonthlyAttendances_FiscalYearId ON hr.[MonthlyAttendances] ([FiscalYearId]);
CREATE UNIQUE INDEX UX_MonthlyAttendanceLines_MonthlyId_EmployeeId ON hr.[MonthlyAttendanceLines] ([CompanyId], [MonthlyId], [EmployeeId]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_MonthlyAttendanceLines_MonthlyId ON hr.[MonthlyAttendanceLines] ([MonthlyId]);
CREATE INDEX IX_MonthlyAttendanceLines_EmployeeId ON hr.[MonthlyAttendanceLines] ([EmployeeId]);
CREATE UNIQUE INDEX UX_DailyAttendance_EmployeeId_WorkDate_ShiftId ON hr.[DailyAttendance] ([CompanyId], [EmployeeId], [WorkDate], [ShiftId]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_DailyAttendance_EmployeeId ON hr.[DailyAttendance] ([EmployeeId]);
CREATE INDEX IX_DailyAttendance_ShiftId ON hr.[DailyAttendance] ([ShiftId]);
CREATE INDEX IX_DailyAttendance_LeaveId ON hr.[DailyAttendance] ([LeaveId]);
CREATE INDEX IX_DailyAttendance_ExcuseId ON hr.[DailyAttendance] ([ExcuseId]);
CREATE INDEX IX_DailyAttendance_HolidayId ON hr.[DailyAttendance] ([HolidayId]);
CREATE INDEX IX_DailyAttendance_MonthlyId ON hr.[DailyAttendance] ([MonthlyId]);
CREATE UNIQUE INDEX UX_LeaveTypes_Code ON hr.[LeaveTypes] ([CompanyId], [Code]) WHERE [IsDeleted] = 0;
CREATE UNIQUE INDEX UX_LeavePolicies_Code ON hr.[LeavePolicies] ([CompanyId], [Code]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_LeavePolicies_LeaveTypeId ON hr.[LeavePolicies] ([LeaveTypeId]);
CREATE INDEX IX_LeavePolicies_EmployeeGroupId ON hr.[LeavePolicies] ([EmployeeGroupId]);
CREATE UNIQUE INDEX UX_LeaveBalances_EmployeeId_LeaveTypeId_BalanceYear ON hr.[LeaveBalances] ([CompanyId], [EmployeeId], [LeaveTypeId], [BalanceYear]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_LeaveBalances_EmployeeId ON hr.[LeaveBalances] ([EmployeeId]);
CREATE INDEX IX_LeaveBalances_LeaveTypeId ON hr.[LeaveBalances] ([LeaveTypeId]);
CREATE INDEX IX_LeaveLedger_EmployeeId_EntryDate ON hr.[LeaveLedger] ([EmployeeId], [EntryDate]);
CREATE INDEX IX_LeaveLedger_EmployeeId ON hr.[LeaveLedger] ([EmployeeId]);
CREATE INDEX IX_LeaveLedger_LeaveTypeId ON hr.[LeaveLedger] ([LeaveTypeId]);
CREATE INDEX IX_LeaveLedger_LeaveId ON hr.[LeaveLedger] ([LeaveId]);
CREATE UNIQUE INDEX UX_Leaves_DocNo ON hr.[Leaves] ([CompanyId], [DocNo]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_Leaves_EmployeeId ON hr.[Leaves] ([EmployeeId]);
CREATE INDEX IX_Leaves_LeaveTypeId ON hr.[Leaves] ([LeaveTypeId]);
CREATE INDEX IX_Leaves_SubstituteEmployeeId ON hr.[Leaves] ([SubstituteEmployeeId]);
CREATE INDEX IX_Leaves_RequestId ON hr.[Leaves] ([RequestId]);
CREATE UNIQUE INDEX UX_ApprovalFlows_Code ON hr.[ApprovalFlows] ([CompanyId], [Code]) WHERE [IsDeleted] = 0;
CREATE UNIQUE INDEX UX_ApprovalFlowSteps_FlowId_StepNo ON hr.[ApprovalFlowSteps] ([CompanyId], [FlowId], [StepNo]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_ApprovalFlowSteps_FlowId ON hr.[ApprovalFlowSteps] ([FlowId]);
CREATE INDEX IX_ApprovalFlowSteps_RoleId ON hr.[ApprovalFlowSteps] ([RoleId]);
CREATE INDEX IX_ApprovalFlowSteps_ApproverUserId ON hr.[ApprovalFlowSteps] ([ApproverUserId]);
CREATE UNIQUE INDEX UX_Requests_DocNo ON hr.[Requests] ([CompanyId], [DocNo]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_Requests_EmployeeId ON hr.[Requests] ([EmployeeId]);
CREATE INDEX IX_Requests_FlowId ON hr.[Requests] ([FlowId]);
CREATE UNIQUE INDEX UX_RequestSteps_RequestId_StepNo ON hr.[RequestSteps] ([CompanyId], [RequestId], [StepNo]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_RequestSteps_RequestId ON hr.[RequestSteps] ([RequestId]);
CREATE INDEX IX_RequestSteps_ApproverUserId ON hr.[RequestSteps] ([ApproverUserId]);
CREATE INDEX IX_RequestSteps_DelegatedFromUserId ON hr.[RequestSteps] ([DelegatedFromUserId]);
CREATE INDEX IX_RequestComments_RequestId ON hr.[RequestComments] ([RequestId]);
CREATE INDEX IX_RequestComments_UserId ON hr.[RequestComments] ([UserId]);
CREATE INDEX IX_Delegations_FromUserId ON hr.[Delegations] ([FromUserId]);
CREATE INDEX IX_Delegations_ToUserId ON hr.[Delegations] ([ToUserId]);
CREATE UNIQUE INDEX UX_PayrollItems_Code ON hr.[PayrollItems] ([CompanyId], [Code]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_VariableItems_EmployeeId_PeriodYear_PeriodMonth ON hr.[VariableItems] ([EmployeeId], [PeriodYear], [PeriodMonth]);
CREATE INDEX IX_VariableItems_EmployeeId ON hr.[VariableItems] ([EmployeeId]);
CREATE INDEX IX_VariableItems_PayrollItemId ON hr.[VariableItems] ([PayrollItemId]);
CREATE INDEX IX_VariableItems_PayrollRunId ON hr.[VariableItems] ([PayrollRunId]);
CREATE UNIQUE INDEX UX_Advances_DocNo ON hr.[Advances] ([CompanyId], [DocNo]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_Advances_EmployeeId ON hr.[Advances] ([EmployeeId]);
CREATE INDEX IX_Advances_RequestId ON hr.[Advances] ([RequestId]);
CREATE UNIQUE INDEX UX_AdvanceInstallments_AdvanceId_InstallmentNo ON hr.[AdvanceInstallments] ([CompanyId], [AdvanceId], [InstallmentNo]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_AdvanceInstallments_AdvanceId ON hr.[AdvanceInstallments] ([AdvanceId]);
CREATE INDEX IX_AdvanceInstallments_PayrollRunLineId ON hr.[AdvanceInstallments] ([PayrollRunLineId]);
CREATE INDEX IX_AdvanceRepayments_AdvanceId ON hr.[AdvanceRepayments] ([AdvanceId]);
CREATE UNIQUE INDEX UX_Custodies_DocNo ON hr.[Custodies] ([CompanyId], [DocNo]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_Custodies_EmployeeId ON hr.[Custodies] ([EmployeeId]);
CREATE INDEX IX_Custodies_RequestId ON hr.[Custodies] ([RequestId]);
CREATE UNIQUE INDEX UX_PayrollRuns_DocNo ON hr.[PayrollRuns] ([CompanyId], [DocNo]) WHERE [IsDeleted] = 0;
CREATE UNIQUE INDEX UX_PayrollRunLines_RunId_EmployeeId ON hr.[PayrollRunLines] ([CompanyId], [RunId], [EmployeeId]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_PayrollRunLines_RunId ON hr.[PayrollRunLines] ([RunId]);
CREATE INDEX IX_PayrollRunLines_EmployeeId ON hr.[PayrollRunLines] ([EmployeeId]);
CREATE INDEX IX_PayrollRunLineItems_LineId ON hr.[PayrollRunLineItems] ([LineId]);
CREATE INDEX IX_PayrollRunLineItems_PayrollItemId ON hr.[PayrollRunLineItems] ([PayrollItemId]);
CREATE UNIQUE INDEX UX_SocialInsuranceRuns_PeriodYear_PeriodMonth ON hr.[SocialInsuranceRuns] ([CompanyId], [PeriodYear], [PeriodMonth]) WHERE [IsDeleted] = 0;
CREATE UNIQUE INDEX UX_SocialInsuranceRunLines_RunId_EmployeeId ON hr.[SocialInsuranceRunLines] ([CompanyId], [RunId], [EmployeeId]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_SocialInsuranceRunLines_RunId ON hr.[SocialInsuranceRunLines] ([RunId]);
CREATE INDEX IX_SocialInsuranceRunLines_EmployeeId ON hr.[SocialInsuranceRunLines] ([EmployeeId]);
CREATE UNIQUE INDEX UX_EndOfServices_DocNo ON hr.[EndOfServices] ([CompanyId], [DocNo]) WHERE [IsDeleted] = 0;
CREATE INDEX IX_EndOfServices_EmployeeId ON hr.[EndOfServices] ([EmployeeId]);
CREATE INDEX IX_Tasks_AssignedUserId ON hr.[Tasks] ([AssignedUserId]);
GO

-- ---------- Views
CREATE VIEW hr.vw_EmployeeHistory AS
SELECT a.Id, a.CompanyId, TRY_CAST(a.EntityId AS bigint) AS EmployeeId, a.UserId, a.ActionType, a.OldValues, a.NewValues,
       a.IpAddress, a.DeviceName, a.OccurredAt
FROM hr.AuditLogs a WHERE a.EntityName = 'Employee';
GO
CREATE VIEW hr.vw_EmployeesWithoutShift AS
SELECT e.* FROM hr.Employees e
WHERE e.IsDeleted = 0 AND e.WorkStartDate IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM hr.EmployeeShifts s WHERE s.EmployeeId = e.Id AND s.IsDeleted = 0
      AND s.EffectiveFrom <= CAST(SYSDATETIMEOFFSET() AS date)
      AND (s.EffectiveTo IS NULL OR s.EffectiveTo >= CAST(SYSDATETIMEOFFSET() AS date)));
GO
CREATE VIEW hr.vw_ExpiringDocuments AS
SELECT e.CompanyId, e.Id AS EmployeeId, N'identity' AS DocKind, CAST(NULL AS bigint) AS DocumentTypeId, e.IdentityExpiryDate AS ExpiryDate,
       DATEDIFF(day, CAST(SYSDATETIMEOFFSET() AS date), e.IdentityExpiryDate) AS DaysLeft
FROM hr.Employees e WHERE e.IsDeleted = 0 AND e.IdentityExpiryDate IS NOT NULL
UNION ALL
SELECT e.CompanyId, e.Id, N'passport', NULL, e.PassportExpiryDate, DATEDIFF(day, CAST(SYSDATETIMEOFFSET() AS date), e.PassportExpiryDate)
FROM hr.Employees e WHERE e.IsDeleted = 0 AND e.PassportExpiryDate IS NOT NULL
UNION ALL
SELECT e.CompanyId, e.Id, N'insurance', NULL, e.InsuranceDueDate, DATEDIFF(day, CAST(SYSDATETIMEOFFSET() AS date), e.InsuranceDueDate)
FROM hr.Employees e WHERE e.IsDeleted = 0 AND e.InsuranceDueDate IS NOT NULL
UNION ALL
SELECT d.CompanyId, d.EmployeeId, N'document', d.DocumentTypeId, d.ExpiryDate, DATEDIFF(day, CAST(SYSDATETIMEOFFSET() AS date), d.ExpiryDate)
FROM hr.EmployeeDocuments d WHERE d.IsDeleted = 0 AND d.ExpiryDate IS NOT NULL;
GO
