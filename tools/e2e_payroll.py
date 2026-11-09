"""E2E payroll cycle against live Hrms.Api + SQL Server.
Env: HRMS_URL, HRMS_PASSWORD (admin), HRMS_SQL_PASSWORD (sa, for test-data setup only)."""
import os, sys, json, subprocess, urllib.request, urllib.error, datetime as dt
B = os.environ.get("HRMS_URL", "http://localhost:5001") + "/api/v1"
SQL = ["/opt/mssql-tools18/bin/sqlcmd", "-C", "-S", "localhost", "-U", "sa", "-P", os.environ["HRMS_SQL_PASSWORD"],
       "-d", "HRMS", "-I", "-h", "-1", "-W", "-b", "-f", "65001"]
TOK = None; results = []
def sql(q):
    r = subprocess.run(SQL + ["-Q", "SET NOCOUNT ON; " + q], capture_output=True, text=True)
    if r.returncode: raise SystemExit("SQL error: " + r.stdout + r.stderr)
    return [l for l in r.stdout.strip().splitlines() if l.strip()]
def call(m, p, body=None, expect=None):
    req = urllib.request.Request(B + p, method=m, data=json.dumps(body).encode() if body is not None else None,
          headers={"Content-Type": "application/json", **({"Authorization": "Bearer " + TOK} if TOK else {})})
    try:
        with urllib.request.urlopen(req) as r: code, txt = r.status, r.read().decode()
    except urllib.error.HTTPError as e: code, txt = e.code, e.read().decode()
    js = json.loads(txt) if txt else None
    ok = (code == expect) if expect else (200 <= code < 300)
    results.append(ok); print("PASS" if ok else "FAIL", code, m, p, "" if ok else str(js)[:400])
    return js
def check(name, got, exp, tol=0.02):
    ok = abs(float(got) - float(exp)) <= tol if isinstance(exp, (int, float)) else got == exp
    results.append(ok); print("PASS" if ok else "FAIL", "CHECK", name, "expected", exp, "got", got)
def g(d, *ks):
    for k in ks:
        if isinstance(d, dict) and k in d: return d[k]

r = call("POST", "/auth/login", {"companyCode": "C1", "username": "admin", "password": os.environ["HRMS_PASSWORD"]})
TOK = r["data"]["accessToken"]
tag = dt.datetime.now().strftime("%H%M%S")
root = call("POST", "/org-nodes", {"parentId": None, "nameAr": "شركة الرواتب " + tag, "nameEn": "PR", "nodeType": "root"})
dept = call("POST", "/org-nodes", {"parentId": g(root["data"], "id"), "nameAr": "قسم " + tag, "nameEn": "D", "nodeType": "department"})
deptId = g(dept["data"], "id")
st = call("GET", "/lookups?lookupType=employee_status"); statusId = g(st["data"][0], "id")
emp = call("POST", "/employees", {"firstNameAr": "سالم", "lastNameAr": "الرواتب " + tag, "gender": "M", "orgNodeId": deptId,
                                   "hireDate": "2026-10-01", "statusId": statusId})
empId = g(emp["data"], "id")
call("POST", f"/employees/{empId}/start-work", {"startDate": "2026-10-01", "notes": "e2e"})
# --- test data that has no API yet: salary item, insurance %, advance installment
sql(f"""UPDATE hr.Employees SET DailyHours=8, SocialEmployeePct=6, SocialInsuranceNo=N'SI-{tag}' WHERE Id={empId};
INSERT hr.EmployeeSalaryItems(CompanyId,EmployeeId,PayrollItemId,Amount)
  SELECT 1,{empId},Id,300000 FROM hr.PayrollItems WHERE CompanyId=1 AND Code=1 AND IsDeleted=0;
DECLARE @doc int=(SELECT ISNULL(MAX(DocNo),0)+1 FROM hr.Advances WHERE CompanyId=1);
INSERT hr.Advances(CompanyId,DocNo,EmployeeId,AdvanceDate,Amount,InstallmentsCount,InstallmentAmount,FirstDeductYear,FirstDeductMonth,Status,RemainingAmount)
  VALUES(1,@doc,{empId},'2026-10-15',20000,1,20000,2026,11,'active',20000);
INSERT hr.AdvanceInstallments(CompanyId,AdvanceId,InstallmentNo,PeriodYear,PeriodMonth,Amount,Status)
  VALUES(1,SCOPE_IDENTITY(),1,2026,11,20000,'pending');""")
shift = call("POST", "/shifts", {"code": int(tag[-4:]) + 5000, "nameAr": "صباحي " + tag, "startTime": "08:00:00", "endTime": "16:00:00",
   "workMinutes": 480, "breakMinutes": 0, "cycleType": "weekly", "punchMode": "inout", "beforeInMin": 60, "afterInMin": 240,
   "beforeOutMin": 240, "afterOutMin": 240, "lateGraceEnabled": True, "lateGraceMin": 10, "lateAfterGrace": True,
   "earlyGraceEnabled": True, "earlyGraceMin": 10, "earlyBeforeGrace": True, "isActive": True,
   "shiftDays": [{"dayOfWeek": d, "isWorkDay": d != 5} for d in range(7)]})
call("POST", "/shift-assignments/apply", {"employeeIds": [empId], "shiftId": g(shift["data"], "id"), "effectiveFrom": "2026-11-01"})
for t, d in [("2026-11-02T08:25:00", "in"), ("2026-11-02T16:00:00", "out"), ("2026-11-03T08:00:00", "in"), ("2026-11-03T15:30:00", "out")]:
    call("POST", "/punches/manual", {"employeeId": empId, "punchTime": t, "direction": d, "reason": "e2e"})
call("POST", "/attendance/process", {"from": "2026-11-02", "to": "2026-11-04", "employeeIds": [empId]})
# Nov 2: late 15, absent 10 | Nov 3: early 20, absent 10 | Nov 4: absent day

call("POST", "/payroll-runs", {"periodYear": 2026, "periodMonth": 11, "runType": "custom"}, expect=400)  # custom needs orgNodeId
run = call("POST", "/payroll-runs", {"periodYear": 2026, "periodMonth": 11, "runType": "custom", "orgNodeId": g(root["data"], "id"),
                                     "description": "e2e " + tag})
runId = g(run["data"], "id")
calc = call("POST", f"/payroll-runs/{runId}/calculate")
print("   calc ->", json.dumps(calc["data"], ensure_ascii=False)[:300])
check("employees in custom scope (descendant node)", g(calc["data"], "employees", "employeesCount"), 1)
lines = call("GET", f"/payroll-runs/{runId}/lines")
line = next(x for x in lines["data"] if g(x, "employeeId") == empId)
# Expected (Law 5/1995 + Law 26/1991): day 10000, hour 1250
absence = 1 * 10000 + 20 / 60 * 1250        # 10416.67
late = (15 + 20) / 60 * 1250                # 729.17
check("BasicSalary", g(line, "basicSalary"), 300000)
check("TotalEarnings", g(line, "totalEarnings"), 300000)
check("TotalDeductions", g(line, "totalDeductions"), round(absence, 2) + round(late, 2) + 18000 + 20000)
check("NetPay", g(line, "netPay"), 300000 - (round(absence, 2) + round(late, 2) + 18000 + 20000))
check("EmployerInsurance 9%", g(calc["data"], "employerInsurance"), 27000)
items = call("GET", f"/payroll-runs/{runId}/lines/{g(line, 'id')}/items")
print("   items ->", [(g(i, "nameAr"), g(i, "amount")) for i in items["data"]])
check("line items count (BASIC, ABSENCE, LATE, ADVANCE, INSURANCE)", len(items["data"]), 5)

call("POST", f"/payroll-runs/{runId}/approve")
st1 = sql(f"SELECT i.Status+'|'+CAST(a.RemainingAmount AS varchar(20)) FROM hr.AdvanceInstallments i JOIN hr.Advances a ON a.Id=i.AdvanceId WHERE a.EmployeeId={empId}")
check("after approve: installment deducted, remaining 0", st1[0], "deducted|0.00")
call("POST", f"/payroll-runs/{runId}/calculate", expect=422)   # locked
call("POST", f"/payroll-runs/{runId}/unapprove")
st2 = sql(f"SELECT i.Status+'|'+CAST(a.RemainingAmount AS varchar(20)) FROM hr.AdvanceInstallments i JOIN hr.Advances a ON a.Id=i.AdvanceId WHERE a.EmployeeId={empId}")
check("after unapprove: installment reversed", st2[0], "pending|20000.00")
call("POST", f"/payroll-runs/{runId}/approve")
call("POST", f"/payroll-runs/{runId}/pay")
call("POST", f"/payroll-runs/{runId}/unapprove", expect=422)   # cannot reverse a paid run
call("DELETE", f"/payroll-runs/{runId}", expect=422)            # only drafts deletable
audit = sql(f"SELECT COUNT(*) FROM hr.AuditLogs WHERE EntityName='PayrollRun' AND EntityId='{runId}'")
check("audit entries for run >= 5", int(audit[0]) >= 5, True)
p = sum(results); print(f"\nRESULT: {p}/{len(results)} passed"); sys.exit(0 if p == len(results) else 1)
