import openai,json,time,concurrent.futures as cf
c=openai.OpenAI(timeout=120)
ids=json.load(open('/tmp/proxy_models.json'))
def t(m):
    t0=time.time()
    try:
        r=c.chat.completions.create(model=m,messages=[{"role":"user","content":"Reply with exactly: OK"}])
        out=(r.choices[0].message.content or "").strip().replace("\n"," ")[:40]
        return m,"OK",round(time.time()-t0,1),out
    except Exception as e:
        return m,"FAIL",round(time.time()-t0,1),str(e)[:120].replace("\n"," ")
res=[]
with cf.ThreadPoolExecutor(16) as ex:
    for r in ex.map(t,ids): res.append(r); print(*r,sep=" | ",flush=True)
json.dump(res,open('/tmp/model_test.json','w'),ensure_ascii=False,indent=1)
print("OK:",sum(r[1]=="OK" for r in res),"/",len(res))
