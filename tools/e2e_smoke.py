"""End-to-end smoke test against a running Hrms.Api + SQL Server.
Usage: HRMS_URL=http://localhost:5001 HRMS_PASSWORD=... python3 tools/e2e_smoke.py"""
import os, sys, json, urllib.request, urllib.error, datetime as dt
B = os.environ.get("HRMS_URL", "http://localhost:5001") + "/api/v1"
TOK = None; results = []
def call(m, p, body=None, expect=None):
    req = urllib.request.Request(B + p, method=m, data=json.dumps(body).encode() if body is not None else None,
                                 headers={"Content-Type": "application/json", **({"Authorization": "Bearer " + TOK} if TOK else {})})
    try:
        with urllib.request.urlopen(req) as r: code, txt = r.status, r.read().decode()
    except urllib.error.HTTPError as e: code, txt = e.code, e.read().decode()
    try: js = json.loads(txt) if txt else None
    except Exception: js = txt
    ok = (code == expect) if expect else (200 <= code < 300)
    results.append((ok, m, p, code)); print(("PASS" if ok else "FAIL"), code, m, p, "" if ok else str(js)[:300])
    return js
def g(d, *ks):
    for k in ks:
        if isinstance(d, dict) and k in d: return d[k]
    return None

call("POST", "/auth/login", {"companyCode": "C1", "username": "admin", "password": "wrong"}, expect=401)
r = call("POST", "/auth/login", {"companyCode": "C1", "username": "admin", "password": os.environ["HRMS_PASSWORD"]})
TOK = r["data"]["accessToken"]; refresh = r["data"]["refreshToken"]
call("GET", "/auth/me")
r2 = call("POST", "/auth/refresh", {"refreshToken": refresh}); TOK = r2["data"]["accessToken"]
call("POST", "/auth/refresh", {"refreshToken": refresh}, expect=401)   # rotated token must be rejected
saved = TOK; TOK = None; call("GET", "/employees", expect=401); TOK = saved

tag = dt.datetime.now().strftime("%H%M%S")
root = call("POST", "/org-nodes", {"parentId": None, "nameAr": "الإدارة العامة " + tag, "nameEn": "HQ", "nodeType": "root"})
rootId = g(root["data"], "id", "Id")
dept = call("POST", "/org-nodes", {"parentId": rootId, "nameAr": "إدارة الموارد البشرية " + tag, "nameEn": "HR", "nodeType": "department"})
deptId = g(dept["data"], "id", "Id")
call("PUT", f"/org-nodes/{rootId}", {"parentId": deptId, "nameAr": "الإدارة العامة " + tag, "nameEn": "HQ", "nodeType": "root"}, expect=400)  # cycle
call("GET", "/org-nodes/tree")
call("DELETE", f"/org-nodes/{rootId}", expect=422)  # has children

job = call("POST", "/defs/jobs", {"nameAr": "محاسب " + tag, "nameEn": "Accountant", "isActive": True})
call("GET", "/defs/jobs?search=" + tag)
call("GET", "/defs/unknown-kind", expect=404)
st = call("GET", "/lookups?lookupType=employee_status"); statusId = g(st["data"][0], "id", "Id")

emp = call("POST", "/employees", {"firstNameAr": "أحمد", "middleNameAr": "علي", "lastNameAr": "صالح " + tag, "gender": "M",
                                   "orgNodeId": deptId, "hireDate": "2026-10-01", "statusId": statusId,
                                   "identityNo": "01010101", "bankAccount": "YE-123-456"})
empId = g(emp["data"], "id", "Id")
full = call("GET", f"/employees/{empId}")
call("POST", "/employees", {"firstNameAr": "x", "lastNameAr": "y", "gender": "M", "orgNodeId": deptId, "hireDate": "2026-10-01",
                            "statusId": statusId, "birthDate": "2020-01-01"}, expect=400)  # under 15
shift = call("POST", "/shifts", {"code": int(tag[-4:]) + 100, "nameAr": "دوام صباحي " + tag, "startTime": "08:00:00", "endTime": "16:00:00",
     "workMinutes": 480, "breakMinutes": 0, "cycleType": "weekly", "punchMode": "inout", "beforeInMin": 60, "afterInMin": 240,
     "beforeOutMin": 240, "afterOutMin": 240, "lateGraceEnabled": True, "lateGraceMin": 10, "lateAfterGrace": True,
     "earlyGraceEnabled": True, "earlyGraceMin": 10, "earlyBeforeGrace": True, "isActive": True,
     "shiftDays": [{"dayOfWeek": d, "isWorkDay": d != 5, "startTime": None, "endTime": None} for d in range(7)]})
shiftId = g(shift["data"], "id", "Id")
a1 = call("POST", "/shift-assignments/apply", {"employeeIds": [empId], "shiftId": shiftId, "effectiveFrom": "2026-11-01"})
print("   before start-work ->", json.dumps(a1["data"], ensure_ascii=False)[:200])
call("POST", f"/employees/{empId}/start-work", {"startDate": "2026-09-01", "notes": "قبل التعيين"}, expect=400)
call("POST", f"/employees/{empId}/start-work", {"startDate": "2026-10-01", "notes": "مباشرة"})
a2 = call("POST", "/shift-assignments/apply", {"employeeIds": [empId], "shiftId": shiftId, "effectiveFrom": "2026-11-01"})
print("   after start-work  ->", json.dumps(a2["data"], ensure_ascii=False)[:200])
for t, d in [("2026-11-02T08:25:00", "in"), ("2026-11-02T16:00:00", "out"), ("2026-11-03T08:00:00", "in"), ("2026-11-03T15:30:00", "out")]:
    call("POST", "/punches/manual", {"employeeId": empId, "punchTime": t, "direction": d, "reason": "اختبار"})
call("POST", "/punches/manual", {"employeeId": empId, "punchTime": "2026-11-02T09:00:00", "direction": "in", "reason": ""}, expect=400)
call("POST", "/attendance/process", {"from": "2026-11-01", "to": "2026-11-07", "employeeIds": [empId]})
daily = call("GET", f"/attendance/daily?employeeId={empId}&from=2026-11-01&to=2026-11-07")
rows = {str(g(x, "workDate", "WorkDate"))[:10]: x for x in daily["data"]}
def chk(day, **exp):
    row = rows.get(day); got = {k: g(row, k, k[0].upper() + k[1:]) for k in exp} if row else None
    ok = got == exp; results.append((ok, "CHECK", day, 0)); print("PASS" if ok else "FAIL", "CHECK", day, exp, "" if ok else got)
chk("2026-11-02", status="present", lateMin=15, absentMin=10, workMinutes=455)   # Monday, late 08:25
chk("2026-11-03", status="present", earlyLeaveMin=20, absentMin=10, workMinutes=450)  # Tuesday, early 15:30
chk("2026-11-04", status="absent", absentMin=480)   # Wednesday, no punches
chk("2026-11-06", status="weekend")                 # Friday rest day (Art.77)
call("DELETE", f"/employees/{empId}")
call("GET", f"/employees/{empId}", expect=404)
p = sum(1 for r in results if r[0]); print(f"\nRESULT: {p}/{len(results)} passed")
sys.exit(0 if p == len(results) else 1)
