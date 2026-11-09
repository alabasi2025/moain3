import openai,sys,time,re,os
c=openai.OpenAI(timeout=1800)
common=open('/tmp/gen/common.txt').read()
k,model=sys.argv[1],sys.argv[2]
p=open(f'/tmp/gen/p_{k}.txt').read()
t=time.time(); out=[]
for attempt in range(3):
    try:
        out=[]
        st=c.chat.completions.create(model=model,stream=True,messages=[{"role":"system","content":common},{"role":"user","content":p}])
        for ch in st:
            if ch.choices and ch.choices[0].delta and ch.choices[0].delta.content:
                out.append(ch.choices[0].delta.content)
                if len(out)%200==0: open(f'/tmp/gen/partial_{k}.md','w').write("".join(out))
        break
    except Exception as e:
        print("attempt",attempt,"ERR",str(e)[:150],flush=True); time.sleep(5)
out="".join(out)
open(f'/tmp/gen/out_{k}.md','w').write(out)
files=re.findall(r"=== FILE: (.+?) ===\n(.*?)\n=== END FILE ===",out,re.S)
for path,body in files:
    body=re.sub(r"^```\w*\n|```\s*$","",body.strip())+"\n"
    fp=os.path.join('/tmp/gen/code2',path.strip()); os.makedirs(os.path.dirname(fp),exist_ok=True); open(fp,'w').write(body)
print(k,model,len(out),"files",len(files),int(time.time()-t),"s",flush=True)
