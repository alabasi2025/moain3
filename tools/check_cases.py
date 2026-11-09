"""Invariant check for attendance acceptance table (docs/02 §1): Work+Late+Early+Absent+Excused == Required."""
cases = {  # id: (Required, Work, Late, Early, Absent, Excused)
 1:(480,480,0,0,0,0), 2:(480,471,0,0,9,0), 3:(480,455,15,0,10,0), 4:(480,450,0,20,10,0),
 5:(480,0,0,0,480,0), 6:(480,0,0,0,480,0), 7:(480,480,0,0,0,0), 8:(480,480,0,0,0,0),
 13:(480,360,0,0,0,120), 14:(480,480,0,0,0,0), 15:(480,475,0,0,5,0)}
bad = {k:v for k,v in cases.items() if sum(v[1:]) != v[0]}
print("OK: all", len(cases), "work-day cases satisfy the invariant" if not bad else f"FAIL {bad}")
# payroll example (docs/02 §3)
basic=300000; day=basic/30; hour=day/8; ot=2*hour*1.5
earn=basic+ot; ded=day+hour+basic*0.06+20000
assert (day,hour,ot,earn,ded,earn-ded)==(10000,1250,3750,303750,49250,254500)
print("OK: payroll example 254,500 YER")
