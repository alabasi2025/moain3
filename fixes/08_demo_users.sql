/* 08_demo_users.sql — DEMO ONLY. Fixed test accounts: admin / 1  and  user / 1 (hr_manager). NEVER run in production. */
SET NOCOUNT ON; SET QUOTED_IDENTIFIER ON;
DECLARE @c bigint = (SELECT Id FROM hr.Companies WHERE Code = N'C1' AND IsDeleted = 0);
DECLARE @h nvarchar(500) = N'PBKDF2$100000$Pc4hEb9VQG+HC8Iexx+qog==$jsQwkbwbhYuX/1FYs+2LWMHMIrBnCIL0b1je2ANXB5A=';
IF NOT EXISTS (SELECT 1 FROM hr.Users WHERE CompanyId=@c AND Username=N'admin' AND IsDeleted=0)
  INSERT hr.Users(CompanyId,Username,PasswordHash,DisplayName,IsAdmin) VALUES(@c,N'admin',@h,N'مدير النظام',1);
UPDATE hr.Users SET PasswordHash=@h, FailedLogins=0, LockedUntil=NULL, IsActive=1 WHERE CompanyId=@c AND Username=N'admin';
IF NOT EXISTS (SELECT 1 FROM hr.Users WHERE CompanyId=@c AND Username=N'user' AND IsDeleted=0)
  INSERT hr.Users(CompanyId,Username,PasswordHash,DisplayName,IsAdmin) VALUES(@c,N'user',@h,N'مستخدم تجريبي (موارد بشرية)',0);
UPDATE hr.Users SET PasswordHash=@h, FailedLogins=0, LockedUntil=NULL, IsActive=1 WHERE CompanyId=@c AND Username=N'user';
INSERT hr.UserRoles(CompanyId,UserId,RoleId)
SELECT @c,u.Id,r.Id FROM hr.Users u JOIN hr.Roles r ON r.CompanyId=@c AND r.NameEn=N'hr_manager' AND r.IsDeleted=0
WHERE u.CompanyId=@c AND u.Username=N'user' AND NOT EXISTS (SELECT 1 FROM hr.UserRoles x WHERE x.UserId=u.Id AND x.RoleId=r.Id AND x.IsDeleted=0);
SELECT Username, IsAdmin FROM hr.Users WHERE CompanyId=@c AND IsDeleted=0;
