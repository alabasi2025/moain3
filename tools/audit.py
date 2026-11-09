import yaml,re,collections,json
from openapi_spec_validator import validate
spec=yaml.safe_load(open('03_openapi.yaml'))
try: validate(spec); print("OpenAPI: VALID")
except Exception as e: print("OpenAPI INVALID:",str(e)[:300])
ops=[];perms_used=collections.Counter()
for p,item in spec['paths'].items():
    for m,op in item.items():
        if m in('get','post','put','patch','delete'):
            ops.append((m,p,op.get('x-permission'),op.get('operationId')))
            if op.get('x-permission'): perms_used[op['x-permission']]+=1
print("paths",len(spec['paths']),"ops",len(ops))
nop=[o for o in ops if not o[2]]; print("ops without x-permission:",len(nop)); [print("  ",o) for o in nop]
seed=open('02_seed.sql').read()
seedperms=set(re.findall(r"\(N'([a-z_]+\.[a-z_]+\.[a-z_]+)', N'",seed))
print("seed perms",len(seedperms))
missing=sorted(set(perms_used)-seedperms); print("x-permission not in seed:",missing)
unused=sorted(seedperms-set(perms_used)); print("seed perms unused by API:",len(unused)); print("  ",unused)
ids=collections.Counter(o[3] for o in ops); print("dup operationIds:",[k for k,v in ids.items() if v>1])
# schema
sql=open('01_schema.sql').read()
tables={}
for m in re.finditer(r"CREATE TABLE hr\.\[(\w+)\] \((.*?)\n\);",sql,re.S):
    cols=re.findall(r"^\s+\[(\w+)\]\s+(\w+)",m.group(2),re.M); tables[m.group(1)]=dict(cols)
print("tables",len(tables))
fks=re.findall(r"FOREIGN KEY \(\[?(\w+)\]?\) REFERENCES hr\.\[?(\w+)\]?",sql)
print("FKs",len(fks))
bad=[f for f in fks if f[1] not in tables]; print("FK to unknown table:",bad)
# columns ending with Id without FK
fkcols=collections.defaultdict(set)
for m in re.finditer(r"ALTER TABLE hr\.\[?(\w+)\]? (?:WITH CHECK )?ADD CONSTRAINT \w+ FOREIGN KEY \(\[?(\w+)\]?\)",sql): fkcols[m.group(1)].add(m.group(2))
noFk=[(t,c) for t,cs in tables.items() for c in cs if c.endswith('Id') and c!='Id' and c not in fkcols[t]]
print("Id-columns without FK:",len(noFk)); print("  ",noFk)
nocomp=[t for t,cs in tables.items() if 'CompanyId' not in cs]; print("tables without CompanyId:",nocomp)
json.dump({"tables":tables},open('/tmp/tables.json','w'))
