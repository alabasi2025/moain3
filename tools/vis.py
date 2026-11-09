import openai,base64,glob,concurrent.futures as cf,json,sys
c=openai.OpenAI()
P="""هذه صفحة من «دليل استخدام نظام شؤون الموظفين» (نظام قائم). استخرج بدقة كاملة وبالعربية:
1) عنوان الصفحة/الشاشة. 2) كل النصوص الظاهرة حرفياً. 3) كل حقول الشاشة (اسم الحقل، نوعه المتوقع: نص/قائمة/تاريخ/رقم/خيار، إلزامي؟). 4) الأزرار والإجراءات والتبويبات والقوائم. 5) قواعد العمل والمعادلات أو الحالات المذكورة. 6) الجداول وأعمدتها. أخرج Markdown منظماً دون تخمين؛ ما لا يُقرأ اكتب [غير مقروء]."""
def run(f):
    b=base64.b64encode(open(f,'rb').read()).decode()
    for m in ["gpt-5.5","gpt-5.2"]:
        try:
            r=c.chat.completions.create(model=m,messages=[{"role":"user","content":[{"type":"text","text":P},{"type":"image_url","image_url":{"url":"data:image/jpeg;base64,"+b}}]}])
            return f,m,r.choices[0].message.content
        except Exception as e: err=str(e)[:200]
    return f,"ERR",err
files=sorted(glob.glob(sys.argv[1]))
with cf.ThreadPoolExecutor(8) as ex:
    for f,m,t in ex.map(run,files):
        open(f+".md","w").write(f"<!-- {m} -->\n"+t); print(f,m,len(t))
