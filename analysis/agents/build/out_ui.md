=== FILE: src/Hrms.Api/wwwroot/index.html ===
<!DOCTYPE html>
<html lang="ar" dir="rtl">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1" />
<title>نظام الموارد البشرية</title>
<style>
:root{
  --primary:#0f766e;--primary-dark:#115e59;--primary-light:#ccfbf1;
  --bg:#f3f4f6;--surface:#ffffff;--text:#1f2937;--muted:#6b7280;--border:#e5e7eb;
  --danger:#b91c1c;--danger-bg:#fee2e2;--success:#15803d;--success-bg:#dcfce7;
  --info:#1d4ed8;--info-bg:#dbeafe;--gray-bg:#e5e7eb;--warn:#b45309;--warn-bg:#fef3c7;
  --radius:10px;--shadow:0 1px 3px rgba(0,0,0,.08),0 1px 2px rgba(0,0,0,.04);
}
*{box-sizing:border-box}
html,body{margin:0;padding:0;height:100%}
body{font-family:system-ui,-apple-system,"Segoe UI",Roboto,"Noto Sans Arabic",Tahoma,sans-serif;background:var(--bg);color:var(--text);font-size:14px;line-height:1.6}
button,input,select,textarea{font:inherit;color:inherit}
h1,h2,h3{margin:0 0 12px;font-weight:600}
h1{font-size:22px}h2{font-size:18px}h3{font-size:15px}
.hidden{display:none !important}
/* Login */
#loginView{min-height:100vh;display:flex;align-items:center;justify-content:center;background:linear-gradient(135deg,#0f766e 0%,#134e4a 100%);padding:16px}
.login-card{background:var(--surface);border-radius:14px;box-shadow:0 20px 50px rgba(0,0,0,.25);padding:32px;width:100%;max-width:400px}
.login-card .logo{width:52px;height:52px;border-radius:14px;background:var(--primary);color:#fff;display:flex;align-items:center;justify-content:center;font-weight:700;font-size:22px;margin:0 auto 12px}
.login-card h1{text-align:center;font-size:20px;margin-bottom:4px}
.login-card .sub{text-align:center;color:var(--muted);margin-bottom:20px}
/* Layout */
#appView{display:flex;min-height:100vh}
.sidebar{width:240px;background:var(--surface);border-left:1px solid var(--border);display:flex;flex-direction:column;position:sticky;top:0;height:100vh}
.sidebar .brand{display:flex;align-items:center;gap:10px;padding:18px 16px;border-bottom:1px solid var(--border)}
.sidebar .brand .logo{width:36px;height:36px;border-radius:10px;background:var(--primary);color:#fff;display:flex;align-items:center;justify-content:center;font-weight:700}
.sidebar .brand .name{font-weight:600}
.sidebar nav{padding:10px;flex:1}
.sidebar nav a{display:flex;align-items:center;gap:10px;padding:10px 12px;border-radius:8px;color:var(--text);text-decoration:none;margin-bottom:2px}
.sidebar nav a:hover{background:var(--bg)}
.sidebar nav a.active{background:var(--primary-light);color:var(--primary-dark);font-weight:600}
.sidebar nav a .ic{width:20px;text-align:center}
.sidebar .user{padding:14px 16px;border-top:1px solid var(--border);display:flex;align-items:center;justify-content:space-between;gap:8px}
.sidebar .user .nm{font-weight:600;font-size:13px}
.sidebar .user .cc{color:var(--muted);font-size:12px}
.main{flex:1;padding:24px;min-width:0}
.page-head{display:flex;align-items:center;justify-content:space-between;gap:12px;margin-bottom:18px;flex-wrap:wrap}
.card{background:var(--surface);border-radius:var(--radius);box-shadow:var(--shadow);padding:18px;margin-bottom:18px}
.cards{display:grid;grid-template-columns:repeat(auto-fill,minmax(220px,1fr));gap:16px}
.stat{background:var(--surface);border-radius:var(--radius);box-shadow:var(--shadow);padding:20px;border-top:4px solid var(--primary)}
.stat .lbl{color:var(--muted);font-size:13px}
.stat .val{font-size:30px;font-weight:700;color:var(--primary-dark)}
/* Forms */
.form-row{display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));gap:12px}
.field{display:flex;flex-direction:column;gap:4px;margin-bottom:12px}
.field label{font-size:13px;color:var(--muted)}
.field input,.field select,.field textarea{padding:9px 11px;border:1px solid var(--border);border-radius:8px;background:#fff;outline:none;width:100%}
.field input:focus,.field select:focus,.field textarea:focus{border-color:var(--primary);box-shadow:0 0 0 3px rgba(15,118,110,.15)}
.field textarea{min-height:70px;resize:vertical}
.check{display:flex;align-items:center;gap:8px;flex-direction:row}
.check input{width:auto}
.btn{display:inline-flex;align-items:center;gap:6px;padding:9px 16px;border-radius:8px;border:1px solid transparent;cursor:pointer;background:var(--primary);color:#fff;font-weight:500;white-space:nowrap}
.btn:hover{background:var(--primary-dark)}
.btn:disabled{opacity:.6;cursor:not-allowed}
.btn.secondary{background:#fff;color:var(--text);border-color:var(--border)}
.btn.secondary:hover{background:var(--bg)}
.btn.danger{background:var(--danger)}
.btn.danger:hover{background:#991b1b}
.btn.sm{padding:5px 10px;font-size:13px}
.btn.block{width:100%;justify-content:center}
.toolbar{display:flex;gap:10px;align-items:center;flex-wrap:wrap;margin-bottom:14px}
.toolbar input,.toolbar select{padding:8px 11px;border:1px solid var(--border);border-radius:8px;background:#fff;outline:none}
.toolbar input:focus,.toolbar select:focus{border-color:var(--primary)}
.toolbar .grow{flex:1}
/* Tables */
.table-wrap{overflow-x:auto}
table{width:100%;border-collapse:collapse}
th,td{padding:10px 12px;text-align:right;border-bottom:1px solid var(--border);white-space:nowrap}
th{background:#f9fafb;color:var(--muted);font-weight:600;font-size:13px}
tbody tr:hover{background:#f9fafb}
td.actions{display:flex;gap:6px}
.empty{text-align:center;color:var(--muted);padding:30px}
.pager{display:flex;gap:8px;align-items:center;justify-content:flex-start;margin-top:12px;color:var(--muted);font-size:13px}
.badge{display:inline-block;padding:2px 10px;border-radius:999px;font-size:12px;font-weight:600}
.badge.green{background:var(--success-bg);color:var(--success)}
.badge.red{background:var(--danger-bg);color:var(--danger)}
.badge.blue{background:var(--info-bg);color:var(--info)}
.badge.gray{background:var(--gray-bg);color:#374151}
.badge.amber{background:var(--warn-bg);color:var(--warn)}
tr.st-present td:first-child{border-right:4px solid var(--success)}
tr.st-absent td:first-child{border-right:4px solid var(--danger)}
tr.st-leave td:first-child{border-right:4px solid var(--info)}
tr.st-gray td:first-child{border-right:4px solid #9ca3af}
tr.st-amber td:first-child{border-right:4px solid var(--warn)}
/* Alerts */
.alert{padding:12px 14px;border-radius:8px;margin-bottom:12px;border:1px solid transparent}
.alert.error{background:var(--danger-bg);color:var(--danger);border-color:#fecaca}
.alert.success{background:var(--success-bg);color:var(--success);border-color:#bbf7d0}
.alert .t{font-weight:700}
.alert ul{margin:6px 0 0;padding-right:18px}
#toast{position:fixed;bottom:20px;left:20px;z-index:200;display:flex;flex-direction:column;gap:8px}
.toast{padding:12px 16px;border-radius:8px;box-shadow:0 8px 24px rgba(0,0,0,.15);min-width:260px;max-width:420px;color:#fff;animation:fade .2s}
.toast.ok{background:var(--success)}.toast.err{background:var(--danger)}
.toast .t{font-weight:700}
@keyframes fade{from{opacity:0;transform:translateY(8px)}to{opacity:1;transform:none}}
/* Modal */
.modal-bg{position:fixed;inset:0;background:rgba(0,0,0,.45);display:flex;align-items:center;justify-content:center;z-index:100;padding:16px}
.modal{background:var(--surface);border-radius:14px;width:100%;max-width:560px;max-height:90vh;overflow:auto;box-shadow:0 20px 60px rgba(0,0,0,.3)}
.modal .mh{display:flex;align-items:center;justify-content:space-between;padding:16px 20px;border-bottom:1px solid var(--border)}
.modal .mh h3{margin:0}
.modal .mb{padding:20px}
.modal .mf{display:flex;justify-content:flex-start;gap:8px;padding:14px 20px;border-top:1px solid var(--border)}
.xbtn{background:none;border:none;font-size:20px;cursor:pointer;color:var(--muted)}
/* Tree */
.org-layout{display:grid;grid-template-columns:1fr 340px;gap:18px}
@media (max-width:900px){.org-layout{grid-template-columns:1fr}.sidebar{width:200px}}
.tree ul{list-style:none;margin:0;padding-right:20px}
.tree>ul{padding-right:0}
.tree li{margin:2px 0}
.tnode{display:flex;align-items:center;gap:6px;padding:6px 8px;border-radius:6px;cursor:pointer}
.tnode:hover{background:var(--bg)}
.tnode.sel{background:var(--primary-light);color:var(--primary-dark);font-weight:600}
.tnode .tg{width:18px;height:18px;display:inline-flex;align-items:center;justify-content:center;border-radius:4px;color:var(--muted);font-size:11px;cursor:pointer}
.tnode .tg:hover{background:var(--border)}
.tnode .tp{font-size:11px;color:var(--muted);background:var(--bg);padding:1px 6px;border-radius:4px}
.tnode .cd{font-size:12px;color:var(--muted)}
.multi{border:1px solid var(--border);border-radius:8px;max-height:220px;overflow:auto;padding:6px;background:#fff}
.multi label{display:flex;align-items:center;gap:8px;padding:5px 6px;border-radius:6px;cursor:pointer}
.multi label:hover{background:var(--bg)}
.grid2{display:grid;grid-template-columns:1fr 1fr;gap:18px}
@media (max-width:1000px){.grid2{grid-template-columns:1fr}}
.kv{display:flex;gap:16px;flex-wrap:wrap;color:var(--muted)}
.kv b{color:var(--text)}
.spinner{display:inline-block;width:16px;height:16px;border:2px solid rgba(255,255,255,.5);border-top-color:#fff;border-radius:50%;animation:spin .7s linear infinite}
@keyframes spin{to{transform:rotate(360deg)}}
.loading{text-align:center;color:var(--muted);padding:20px}
</style>
</head>
<body>

<div id="loginView" class="hidden">
  <div class="login-card">
    <div class="logo">HR</div>
    <h1>تسجيل الدخول</h1>
    <div class="sub">نظام الموارد البشرية</div>
    <div id="loginError"></div>
    <form id="loginForm" autocomplete="on">
      <div class="field"><label for="lCompany">رقم الشركة</label><input id="lCompany" value="C1" required /></div>
      <div class="field"><label for="lUser">اسم المستخدم</label><input id="lUser" required autocomplete="username" /></div>
      <div class="field"><label for="lPass">كلمة المرور</label><input id="lPass" type="password" required autocomplete="current-password" /></div>
      <button class="btn block" id="loginBtn" type="submit">دخول</button>
    </form>
  </div>
</div>

<div id="appView" class="hidden">
  <aside class="sidebar">
    <div class="brand"><div class="logo">HR</div><div class="name">الموارد البشرية</div></div>
    <nav id="nav">
      <a href="#/home" data-route="home"><span class="ic">▦</span>الرئيسية</a>
      <a href="#/org" data-route="org" data-perm="defs.org_nodes.read"><span class="ic">⛶</span>الهيكل الإداري</a>
      <a href="#/defs" data-route="defs" data-perm="defs.jobs.read"><span class="ic">☰</span>التعريفات</a>
      <a href="#/employees" data-route="employees" data-perm="employees.employees.read"><span class="ic">👥</span>الموظفون</a>
      <a href="#/attendance" data-route="attendance" data-perm="attendance.daily.read"><span class="ic">◷</span>الحضور</a>
    </nav>
    <div class="user">
      <div><div class="nm" id="uName"></div><div class="cc" id="uInfo"></div></div>
      <button class="btn secondary sm" id="logoutBtn" type="button">خروج</button>
    </div>
  </aside>
  <main class="main" id="main"></main>
</div>

<div id="modalHost"></div>
<div id="toast"></div>

<script>
(function(){
'use strict';
const API='/api/v1';
const $=(s,r)=>(r||document).querySelector(s);
const $$=(s,r)=>Array.from((r||document).querySelectorAll(s));
function esc(v){if(v===null||v===undefined)return '';return String(v).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));}
function el(tag,attrs,children){const e=document.createElement(tag);if(attrs)for(const k in attrs){if(k==='class')e.className=attrs[k];else if(k==='text')e.textContent=attrs[k];else if(k.startsWith('on'))e.addEventListener(k.slice(2),attrs[k]);else if(attrs[k]!==undefined&&attrs[k]!==null)e.setAttribute(k,attrs[k]);}if(children)for(const c of [].concat(children)){if(c===null||c===undefined)continue;e.appendChild(typeof c==='string'?document.createTextNode(c):c);}return e;}

/* ---------- Auth / storage ---------- */
const store={
  get access(){return sessionStorage.getItem('accessToken');},
  get refresh(){return sessionStorage.getItem('refreshToken');},
  get user(){try{return JSON.parse(sessionStorage.getItem('user')||'null');}catch(e){return null;}},
  set(d){sessionStorage.setItem('accessToken',d.accessToken||'');sessionStorage.setItem('refreshToken',d.refreshToken||'');if(d.user)sessionStorage.setItem('user',JSON.stringify(d.user));},
  clear(){sessionStorage.removeItem('accessToken');sessionStorage.removeItem('refreshToken');sessionStorage.removeItem('user');}
};
let currentUser=null;
function hasPerm(p){if(!currentUser)return false;if(currentUser.isAdmin)return true;return Array.isArray(currentUser.permissions)&&currentUser.permissions.includes(p);}

/* ---------- API ---------- */
class ApiError extends Error{constructor(p,status){super(p.title||'خطأ');this.title=p.title||'خطأ';this.detail=p.detail||'';this.status=status;this.errors=p.errors||null;}}
async function parseBody(res){const ct=res.headers.get('content-type')||'';const txt=await res.text();if(!txt)return null;if(ct.includes('json')){try{return JSON.parse(txt);}catch(e){return {title:'استجابة غير صالحة',detail:txt};}}return {title:'خطأ من الخادم',detail:txt};}
function problemFrom(res,body){const p=body&&typeof body==='object'?body:{};const title=p.title||defaultTitle(res.status);let detail=p.detail||'';if(!detail&&p.errors&&typeof p.errors==='object'){detail=Object.values(p.errors).flat().join('، ');}return new ApiError({title,detail,errors:p.errors},res.status);}
function defaultTitle(s){switch(s){case 400:return 'طلب غير صالح';case 401:return 'غير مصرح';case 403:return 'ليس لديك صلاحية';case 404:return 'غير موجود';case 409:return 'تعارض في البيانات';case 422:return 'بيانات غير صالحة';case 500:return 'خطأ داخلي في الخادم';default:return 'خطأ ('+s+')';}}
let refreshing=null;
async function tryRefresh(){if(refreshing)return refreshing;refreshing=(async()=>{const rt=store.refresh;if(!rt)return false;try{const res=await fetch(API+'/auth/refresh',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({refreshToken:rt})});if(!res.ok)return false;const body=await parseBody(res);const d=body&&body.data;if(!d||!d.accessToken)return false;store.set(d);if(d.user)currentUser=d.user;return true;}catch(e){return false;}finally{refreshing=null;}})();return refreshing;}
async function api(method,path,body,opts){opts=opts||{};const headers={'Accept':'application/json'};if(body!==undefined)headers['Content-Type']='application/json';if(store.access&&!opts.noAuth)headers['Authorization']='Bearer '+store.access;let res;try{res=await fetch(API+path,{method,headers,body:body!==undefined?JSON.stringify(body):undefined});}catch(e){throw new ApiError({title:'تعذر الاتصال بالخادم',detail:'تحقق من اتصال الشبكة ثم أعد المحاولة.'},0);}
  if(res.status===401&&!opts.noAuth&&!opts.retried){const ok=await tryRefresh();if(ok)return api(method,path,body,Object.assign({},opts,{retried:true}));logout();throw new ApiError({title:'انتهت الجلسة',detail:'يرجى تسجيل الدخول مرة أخرى.'},401);}
  const parsed=await parseBody(res);if(!res.ok)throw problemFrom(res,parsed);return parsed||{data:null};}
function qs(o){const p=new URLSearchParams();for(const k in o){if(o[k]!==undefined&&o[k]!==null&&o[k]!=='')p.set(k,o[k]);}const s=p.toString();return s?'?'+s:'';}

/* ---------- UI helpers ---------- */
function toast(msg,type,title){const t=el('div',{class:'toast '+(type||'ok')});if(title)t.appendChild(el('div',{class:'t',text:title}));t.appendChild(el('div',{text:msg}));$('#toast').appendChild(t);setTimeout(()=>t.remove(),4500);}
function showError(err,host){const title=err instanceof ApiError?err.title:'خطأ';const detail=err instanceof ApiError?err.detail:(err&&err.message)||'';if(host){host.innerHTML='';const a=el('div',{class:'alert error'},[el('div',{class:'t',text:title}),detail?el('div',{text:detail}):null]);if(err&&err.errors&&typeof err.errors==='object'){const ul=el('ul');for(const k in err.errors){const vals=[].concat(err.errors[k]);for(const v of vals)ul.appendChild(el('li',{text:(k?k+': ':'')+v}));}a.appendChild(ul);}host.appendChild(a);}else{toast(detail||title,'err',detail?title:undefined);}}
function alertBox(host,type,title,msg){host.innerHTML='';host.appendChild(el('div',{class:'alert '+type},[el('div',{class:'t',text:title}),msg?el('div',{text:msg}):null]));}
function busy(btn,on){if(!btn)return;btn.disabled=on;if(on){btn.dataset.txt=btn.textContent;btn.innerHTML='';btn.appendChild(el('span',{class:'spinner'}));}else if(btn.dataset.txt){btn.textContent=btn.dataset.txt;}}
function modal(title,bodyEl,buttons){const host=$('#modalHost');host.innerHTML='';const bg=el('div',{class:'modal-bg'});const m=el('div',{class:'modal'});const errHost=el('div');const close=()=>{host.innerHTML='';};m.appendChild(el('div',{class:'mh'},[el('h3',{text:title}),el('button',{class:'xbtn',type:'button',onclick:close,'aria-label':'إغلاق'},'×')]));const mb=el('div',{class:'mb'},[errHost,bodyEl]);m.appendChild(mb);const mf=el('div',{class:'mf'});for(const b of buttons){const btn=el('button',{class:'btn '+(b.cls||''),type:'button'},b.text);btn.addEventListener('click',async()=>{if(!b.onClick){close();return;}busy(btn,true);try{const r=await b.onClick(errHost);if(r!==false)close();}catch(e){showError(e,errHost);}finally{busy(btn,false);}});mf.appendChild(btn);}m.appendChild(mf);bg.appendChild(m);bg.addEventListener('click',e=>{if(e.target===bg)close();});host.appendChild(bg);return {close,errHost};}
function confirmModal(title,msg,onOk){return modal(title,el('p',{text:msg}),[{text:'نعم، تأكيد',cls:'danger',onClick:onOk},{text:'إلغاء',cls:'secondary'}]);}
function field(label,input){const f=el('div',{class:'field'});const id='f'+Math.random().toString(36).slice(2,8);input.id=id;f.appendChild(el('label',{for:id,text:label}));f.appendChild(input);return f;}
function inp(attrs){return el('input',attrs);}
function sel(options,value){const s=el('select');for(const o of options){const op=el('option',{value:o.value,text:o.label});if(String(o.value)===String(value))op.selected=true;s.appendChild(op);}return s;}
function table(cols,rows,rowClass){const wrap=el('div',{class:'table-wrap'});const t=el('table');const thead=el('thead');const tr=el('tr');for(const c of cols)tr.appendChild(el('th',{text:c.h}));thead.appendChild(tr);t.appendChild(thead);const tb=el('tbody');if(!rows||!rows.length){const r=el('tr');r.appendChild(el('td',{class:'empty',colspan:cols.length,text:'لا توجد بيانات'}));tb.appendChild(r);}else{for(const row of rows){const r=el('tr',{class:rowClass?rowClass(row):''});for(const c of cols){const td=el('td',{class:c.cls||''});const v=c.r(row);if(v instanceof Node)td.appendChild(v);else td.textContent=v===null||v===undefined?'':String(v);r.appendChild(td);}tb.appendChild(r);}}t.appendChild(tb);wrap.appendChild(t);return wrap;}
function fmtDate(v){if(!v)return '';const s=String(v);return s.length>=10?s.slice(0,10):s;}
function fmtTime(v){if(!v)return '—';const s=String(v);const m=s.match(/(\d{2}:\d{2})(:\d{2})?$/);if(s.includes('T')){const t=s.split('T')[1]||'';return t.slice(0,5);}return m?m[1]:s;}
function todayStr(){const d=new Date();return d.getFullYear()+'-'+String(d.getMonth()+1).padStart(2,'0')+'-'+String(d.getDate()).padStart(2,'0');}
function nowLocal(){const d=new Date();return todayStr()+'T'+String(d.getHours()).padStart(2,'0')+':'+String(d.getMinutes()).padStart(2,'0');}
function meta(res){return (res&&res.meta)||{};}
function total(res,fallback){const m=meta(res);const t=m.total??m.totalCount??m.count;return typeof t==='number'?t:fallback;}

/* ---------- Login / Logout ---------- */
function showLogin(){$('#appView').classList.add('hidden');$('#loginView').classList.remove('hidden');}
function showApp(){$('#loginView').classList.add('hidden');$('#appView').classList.remove('hidden');$('#uName').textContent=currentUser.displayName||currentUser.username||'';$('#uInfo').textContent=(currentUser.isAdmin?'مدير النظام':'مستخدم')+' · '+(currentUser.username||'');applyNavPerms();}
function applyNavPerms(){for(const a of $$('#nav a')){const p=a.dataset.perm;a.classList.toggle('hidden',!!p&&!hasPerm(p));}}
function logout(){store.clear();currentUser=null;location.hash='#/home';showLogin();}
$('#loginForm').addEventListener('submit',async e=>{e.preventDefault();const btn=$('#loginBtn');const errHost=$('#loginError');errHost.innerHTML='';busy(btn,true);try{const res=await api('POST','/auth/login',{companyCode:$('#lCompany').value.trim(),username:$('#lUser').value.trim(),password:$('#lPass').value},{noAuth:true});const d=res.data;if(!d||!d.accessToken)throw new ApiError({title:'استجابة غير متوقعة',detail:'لم يُرجع الخادم رمز الدخول.'},0);store.set(d);currentUser=d.user;$('#lPass').value='';showApp();route();}catch(err){showError(err,errHost);}finally{busy(btn,false);}});
$('#logoutBtn').addEventListener('click',logout);

/* ---------- Router ---------- */
const routes={home:renderHome,org:renderOrg,defs:renderDefs,employees:renderEmployees,attendance:renderAttendance};
const routePerm={org:'defs.org_nodes.read',defs:'defs.jobs.read',employees:'employees.employees.read',attendance:'attendance.daily.read'};
window.addEventListener('hashchange',route);
async function route(){if(!currentUser)return;let name=(location.hash||'#/home').replace(/^#\//,'').split('?')[0]||'home';if(!routes[name])name='home';if(routePerm[name]&&!hasPerm(routePerm[name])){name='home';}for(const a of $$('#nav a'))a.classList.toggle('active',a.dataset.route===name);const main=$('#main');main.innerHTML='';main.appendChild(el('div',{class:'loading',text:'جارٍ التحميل…'}));try{await routes[name](main);}catch(err){main.innerHTML='';const h=el('div');showError(err,h);main.appendChild(h);}}

/* ---------- Home ---------- */
async function renderHome(main){const head=el('div',{class:'page-head'},[el('h1',{text:'الرئيسية'})]);const cards=el('div',{class:'cards'});const empCard=stat('عدد الموظفين','…');const nodeCard=stat('عدد العقد في الهيكل','…');cards.appendChild(empCard.el);cards.appendChild(nodeCard.el);main.innerHTML='';main.appendChild(head);main.appendChild(cards);const welcome=el('div',{class:'card'},[el('h3',{text:'مرحباً '+(currentUser.displayName||currentUser.username||'')}),el('div',{class:'kv'},[el('span',{},['الشركة: ',el('b',{text:String(currentUser.companyId??'')})]),el('span',{},['الصلاحيات: ',el('b',{text:currentUser.isAdmin?'كاملة (مدير)':String((currentUser.permissions||[]).length)})])])]);main.appendChild(welcome);
  if(hasPerm('employees.employees.read')){api('GET','/employees'+qs({page:1,pageSize:1})).then(r=>{empCard.val.textContent=String(total(r,(r.data||[]).length));}).catch(e=>{empCard.val.textContent='—';empCard.val.title=e.title||'';});}else empCard.val.textContent='—';
  if(hasPerm('defs.org_nodes.read')){api('GET','/org-nodes/tree').then(r=>{nodeCard.val.textContent=String(countNodes(r.data||[]));}).catch(e=>{nodeCard.val.textContent='—';nodeCard.val.title=e.title||'';});}else nodeCard.val.textContent='—';}
function stat(lbl,val){const v=el('div',{class:'val',text:val});return {el:el('div',{class:'stat'},[el('div',{class:'lbl',text:lbl}),v]),val:v};}
function countNodes(list){let n=0;for(const x of list){n++;if(Array.isArray(x.children))n+=countNodes(x.children);}return n;}

/* ---------- Org tree ---------- */
const nodeTypes=[{value:'root',label:'جذر'},{value:'region',label:'منطقة'},{value:'general_admin',label:'إدارة عامة'},{value:'company',label:'شركة'},{value:'branch',label:'فرع'},{value:'department',label:'إدارة'},{value:'section',label:'قسم'}];
function nodeTypeLabel(t){const f=nodeTypes.find(x=>x.value===t);return f?f.label:(t||'');}
let orgSelected=null;const orgExpanded=new Set();
async function renderOrg(main){const res=await api('GET','/org-nodes/tree');const tree=res.data||[];const layout=el('div',{class:'org-layout'});const treeCard=el('div',{class:'card'});const treeHost=el('div',{class:'tree'});const sideCard=el('div',{class:'card'});
  const selInfo=el('div',{class:'kv'});const formErr=el('div');const fCode=inp({placeholder:'اختياري'});const fAr=inp({required:'required'});const fEn=inp();const fType=sel(nodeTypes,'department');const addBtn=el('button',{class:'btn',type:'button'},'إضافة العقدة');const delBtn=el('button',{class:'btn danger',type:'button'},'حذف العقدة المحددة');
  function renderSel(){selInfo.innerHTML='';if(!orgSelected){selInfo.appendChild(el('span',{text:'لم يتم تحديد عقدة. ستتم الإضافة في الجذر.'}));delBtn.disabled=true;}else{selInfo.appendChild(el('span',{},['المحددة: ',el('b',{text:orgSelected.nameAr||''})]));selInfo.appendChild(el('span',{},['النوع: ',el('b',{text:nodeTypeLabel(orgSelected.nodeType)})]));if(orgSelected.code)selInfo.appendChild(el('span',{},['الرمز: ',el('b',{text:orgSelected.code})]));delBtn.disabled=false;}}
  function buildList(nodes){const ul=el('ul');for(const n of nodes){const li=el('li');const kids=Array.isArray(n.children)?n.children:[];const open=orgExpanded.has(n.id);const row=el('div',{class:'tnode'+(orgSelected&&orgSelected.id===n.id?' sel':'')});const tg=el('span',{class:'tg',text:kids.length?(open?'▼':'◀'):'•'});if(kids.length)tg.addEventListener('click',e=>{e.stopPropagation();if(orgExpanded.has(n.id))orgExpanded.delete(n.id);else orgExpanded.add(n.id);draw();});row.appendChild(tg);row.appendChild(el('span',{text:n.nameAr||n.nameEn||''}));if(n.code)row.appendChild(el('span',{class:'cd',text:n.code}));row.appendChild(el('span',{class:'tp',text:nodeTypeLabel(n.nodeType)}));row.addEventListener('click',()=>{orgSelected=(orgSelected&&orgSelected.id===n.id)?null:n;draw();renderSel();});li.appendChild(row);if(kids.length&&open)li.appendChild(buildList(kids));ul.appendChild(li);}return ul;}
  function draw(){treeHost.innerHTML='';if(!tree.length)treeHost.appendChild(el('div',{class:'empty',text:'لا توجد عقد بعد. أضف الجذر من النموذج.'}));else treeHost.appendChild(buildList(tree));}
  if(!orgExpanded.size)for(const r of tree)orgExpanded.add(r.id);
  if(orgSelected){const found=findNode(tree,orgSelected.id);orgSelected=found||null;}
  draw();renderSel();
  const expandAll=el('button',{class:'btn secondary sm',type:'button',onclick:()=>{walk(tree,n=>orgExpanded.add(n.id));draw();}},'توسيع الكل');const collapseAll=el('button',{class:'btn secondary sm',type:'button',onclick:()=>{orgExpanded.clear();draw();}},'طي الكل');
  treeCard.appendChild(el('div',{class:'toolbar'},[el('h2',{text:'الشجرة'}),el('span',{class:'grow'}),expandAll,collapseAll]));treeCard.appendChild(treeHost);
  addBtn.addEventListener('click',async()=>{formErr.innerHTML='';if(!fAr.value.trim()){alertBox(formErr,'error','بيانات ناقصة','الاسم العربي مطلوب.');return;}busy(addBtn,true);try{await api('POST','/org-nodes',{parentId:orgSelected?orgSelected.id:null,code:fCode.value.trim()||null,nameAr:fAr.value.trim(),nameEn:fEn.value.trim(),nodeType:fType.value});toast('تمت إضافة العقدة بنجاح');if(orgSelected)orgExpanded.add(orgSelected.id);await renderOrg(main);}catch(e){showError(e,formErr);}finally{busy(addBtn,false);}});
  delBtn.addEventListener('click',()=>{if(!orgSelected)return;const n=orgSelected;confirmModal('حذف العقدة','هل أنت متأكد من حذف «'+(n.nameAr||'')+'»؟',async()=>{await api('DELETE','/org-nodes/'+encodeURIComponent(n.id));toast('تم حذف العقدة');orgSelected=null;await renderOrg(main);});});
  sideCard.appendChild(el('h2',{text:'إضافة عقدة'}));sideCard.appendChild(selInfo);sideCard.appendChild(el('hr',{style:'border:none;border-top:1px solid var(--border);margin:12px 0'}));sideCard.appendChild(formErr);sideCard.appendChild(field('الرمز',fCode));sideCard.appendChild(field('الاسم بالعربية *',fAr));sideCard.appendChild(field('الاسم بالإنجليزية',fEn));sideCard.appendChild(field('نوع العقدة',fType));sideCard.appendChild(el('div',{style:'display:flex;gap:8px;flex-wrap:wrap'},[addBtn,delBtn]));
  layout.appendChild(treeCard);layout.appendChild(sideCard);main.innerHTML='';main.appendChild(el('div',{class:'page-head'},[el('h1',{text:'الهيكل الإداري'}),el('span',{class:'kv'},['إجمالي العقد: ',el('b',{text:String(countNodes(tree))})])]));main.appendChild(layout);}
function findNode(list,id){for(const n of list){if(n.id===id)return n;const k=Array.isArray(n.children)?findNode(n.children,id):null;if(k)return k;}return null;}
function walk(list,fn){for(const n of list){fn(n);if(Array.isArray(n.children))walk(n.children,fn);}}
function flattenTree(list,depth,out){out=out||[];depth=depth||0;for(const n of list){out.push({id:n.id,label:'\u00A0\u00A0'.repeat(depth)+(n.nameAr||n.nameEn||''),nameAr:n.nameAr});if(Array.isArray(n.children))flattenTree(n.children,depth+1,out);}return out;}

/* ---------- Definitions ---------- */
const defKinds=[{value:'job-grades',label:'الدرجات الوظيفية'},{value:'jobs',label:'الوظائف'},{value:'cost-centers',label:'مراكز التكلفة'},{value:'employee-groups',label:'مجموعات الموظفين'},{value:'projects',label:'المشاريع'},{value:'banks',label:'البنوك'},{value:'nationalities',label:'الجنسيات'},{value:'work-natures',label:'طبيعة العمل'}];
const defsState={kind:'jobs',search:'',page:1,pageSize:20};
async function renderDefs(main){main.innerHTML='';const head=el('div',{class:'page-head'},[el('h1',{text:'التعريفات'})]);const card=el('div',{class:'card'});const kindSel=sel(defKinds,defsState.kind);const search=inp({placeholder:'بحث بالرمز أو الاسم…',value:defsState.search});const addBtn=el('button',{class:'btn',type:'button'},'+ إضافة');const errHost=el('div');const body=el('div');const pager=el('div',{class:'pager'});
  kindSel.addEventListener('change',()=>{defsState.kind=kindSel.value;defsState.page=1;load();});let st;search.addEventListener('input',()=>{clearTimeout(st);st=setTimeout(()=>{defsState.search=search.value.trim();defsState.page=1;load();},350);});
  addBtn.addEventListener('click',()=>defForm(null,load));
  card.appendChild(el('div',{class:'toolbar'},[kindSel,search,el('span',{class:'grow'}),addBtn]));card.appendChild(errHost);card.appendChild(body);card.appendChild(pager);main.appendChild(head);main.appendChild(card);
  async function load(){errHost.innerHTML='';body.innerHTML='';body.appendChild(el('div',{class:'loading',text:'جارٍ التحميل…'}));try{const res=await api('GET','/defs/'+defsState.kind+qs({search:defsState.search,page:defsState.page,pageSize:defsState.pageSize}));const rows=res.data||[];body.innerHTML='';body.appendChild(table([{h:'الرمز',r:x=>x.code},{h:'الاسم بالعربية',r:x=>x.nameAr},{h:'الاسم بالإنجليزية',r:x=>x.nameEn},{h:'ملاحظات',r:x=>x.notes},{h:'الحالة',r:x=>el('span',{class:'badge '+(x.isActive?'green':'gray'),text:x.isActive?'نشط':'غير نشط'})},{h:'',cls:'actions',r:x=>el('span',{style:'display:flex;gap:6px'},[el('button',{class:'btn secondary sm',type:'button',onclick:()=>defForm(x,load)},'تعديل'),el('button',{class:'btn danger sm',type:'button',onclick:()=>confirmModal('حذف التعريف','هل تريد حذف «'+(x.nameAr||'')+'»؟',async()=>{await api('DELETE','/defs/'+defsState.kind+'/'+encodeURIComponent(x.id));toast('تم الحذف');load();})},'حذف')])}],rows));renderPager(pager,res,rows.length,defsState,load);}catch(e){body.innerHTML='';showError(e,errHost);}}
  load();}
function renderPager(host,res,count,state,reload){host.innerHTML='';const t=total(res,null);const prev=el('button',{class:'btn secondary sm',type:'button',onclick:()=>{state.page=Math.max(1,state.page-1);reload();}},'السابق');const next=el('button',{class:'btn secondary sm',type:'button',onclick:()=>{state.page++;reload();}},'التالي');prev.disabled=state.page<=1;if(t!==null)next.disabled=state.page*state.pageSize>=t;else next.disabled=count<state.pageSize;host.appendChild(prev);host.appendChild(el('span',{text:'صفحة '+state.page+(t!==null?' · الإجمالي '+t:'')}));host.appendChild(next);}
function defForm(item,onDone){const fCode=inp({value:item?(item.code||''):''});const fAr=inp({value:item?(item.nameAr||''):''});const fEn=inp({value:item?(item.nameEn||''):''});const fNotes=el('textarea');fNotes.value=item?(item.notes||''):'';const fActive=inp({type:'checkbox'});fActive.checked=item?!!item.isActive:true;const body=el('div');body.appendChild(el('div',{class:'form-row'},[field('الرمز (اختياري)',fCode),field('الاسم بالإنجليزية',fEn)]));body.appendChild(field('الاسم بالعربية *',fAr));body.appendChild(field('ملاحظات',fNotes));const chk=el('div',{class:'field check'});chk.appendChild(fActive);chk.appendChild(el('label',{text:'نشط'}));body.appendChild(chk);const kindLabel=(defKinds.find(k=>k.value===defsState.kind)||{}).label||'';
  modal((item?'تعديل':'إضافة')+' — '+kindLabel,body,[{text:'حفظ',onClick:async(errHost)=>{if(!fAr.value.trim()){alertBox(errHost,'error','بيانات ناقصة','الاسم العربي مطلوب.');return false;}const payload={code:fCode.value.trim()||null,nameAr:fAr.value.trim(),nameEn:fEn.value.trim(),notes:fNotes.value.trim(),isActive:fActive.checked};if(item)await api('PUT','/defs/'+defsState.kind+'/'+encodeURIComponent(item.id),payload);else await api('POST','/defs/'+defsState.kind,payload);toast(item?'تم التعديل بنجاح':'تمت الإضافة بنجاح');onDone();}},{text:'إلغاء',cls:'secondary'}]);setTimeout(()=>fAr.focus(),30);}

/* ---------- Employees ---------- */
const empState={search:'',page:1,pageSize:20};
let orgOptionsCache=null,statusCache=null,jobsCache=null;
async function getOrgOptions(force){if(orgOptionsCache&&!force)return orgOptionsCache;try{const r=await api('GET','/org-nodes/tree');orgOptionsCache=flattenTree(r.data||[]);}catch(e){orgOptionsCache=[];}return orgOptionsCache;}
async function getStatuses(){if(statusCache)return statusCache;try{const r=await api('GET','/lookups'+qs({lookupType:'employee_status'}));statusCache=r.data||[];}catch(e){statusCache=[];}return statusCache;}
async function getJobs(){if(jobsCache)return jobsCache;try{const r=await api('GET','/defs/jobs'+qs({page:1,pageSize:500}));jobsCache=r.data||[];}catch(e){jobsCache=[];}return jobsCache;}
async function renderEmployees(main){main.innerHTML='';const head=el('div',{class:'page-head'},[el('h1',{text:'الموظفون'})]);const card=el('div',{class:'card'});const search=inp({placeholder:'بحث بالرقم أو الاسم…',value:empState.search});const addBtn=el('button',{class:'btn',type:'button'},'+ إضافة موظف');const errHost=el('div');const body=el('div');const pager=el('div',{class:'pager'});let st;search.addEventListener('input',()=>{clearTimeout(st);st=setTimeout(()=>{empState.search=search.value.trim();empState.page=1;load();},350);});addBtn.addEventListener('click',()=>empForm(load));
  card.appendChild(el('div',{class:'toolbar'},[search,el('span',{class:'grow'}),addBtn]));card.appendChild(errHost);card.appendChild(body);card.appendChild(pager);main.appendChild(head);main.appendChild(card);
  const [orgs,statuses,jobs]=await Promise.all([getOrgOptions(),getStatuses(),getJobs()]);const orgName=id=>{const o=orgs.find(x=>String(x.id)===String(id));return o?o.nameAr:(id??'');};const stName=id=>{const s=statuses.find(x=>String(x.id)===String(id));return s?s.nameAr:(id??'');};const jobName=id=>{const j=jobs.find(x=>String(x.id)===String(id));return j?j.nameAr:(id??'');};
  async function load(){errHost.innerHTML='';body.innerHTML='';body.appendChild(el('div',{class:'loading',text:'جارٍ التحميل…'}));try{const res=await api('GET','/employees'+qs({search:empState.search,page:empState.page,pageSize:empState.pageSize}));const rows=res.data||[];body.innerHTML='';body.appendChild(table([{h:'الرقم الوظيفي',r:x=>x.employeeNo},{h:'رقم البصمة',r:x=>x.fingerprintNo},{h:'الاسم',r:x=>x.fullNameAr},{h:'الجهة',r:x=>orgName(x.orgNodeId)},{h:'الوظيفة',r:x=>jobName(x.jobId)},{h:'الحالة',r:x=>el('span',{class:'badge blue',text:String(stName(x.statusId))})},{h:'تاريخ التعيين',r:x=>fmtDate(x.hireDate)},{h:'مباشرة العمل',r:x=>x.workStartDate?el('span',{class:'badge green',text:fmtDate(x.workStartDate)}):el('span',{class:'badge gray',text:'لم يباشر'})},{h:'',r:x=>el('button',{class:'btn secondary sm',type:'button',onclick:()=>startWorkForm(x,load)},'مباشرة العمل')}],rows));renderPager(pager,res,rows.length,empState,load);}catch(e){body.innerHTML='';showError(e,errHost);}}
  load();}
async function empForm(onDone){const orgs=await getOrgOptions(true);const statuses=await getStatuses();const fFirst=inp(),fMid=inp(),fLast=inp();const fGender=sel([{value:'M',label:'ذكر'},{value:'F',label:'أنثى'}],'M');const fOrg=sel(orgs.map(o=>({value:o.id,label:o.label})),'');const fHire=inp({type:'date',value:todayStr()});const fStatus=sel(statuses.map(s=>({value:s.id,label:s.nameAr})),'');const fFp=inp();const fMobile=inp({type:'tel',dir:'ltr'});const body=el('div');body.appendChild(el('div',{class:'form-row'},[field('الاسم الأول *',fFirst),field('اسم الأب',fMid),field('اسم العائلة *',fLast)]));body.appendChild(el('div',{class:'form-row'},[field('الجنس',fGender),field('الجهة الإدارية *',fOrg)]));body.appendChild(el('div',{class:'form-row'},[field('تاريخ التعيين *',fHire),field('الحالة *',fStatus)]));body.appendChild(el('div',{class:'form-row'},[field('رقم البصمة',fFp),field('الجوال',fMobile)]));if(!orgs.length)body.appendChild(el('div',{class:'alert error'},[el('div',{class:'t',text:'لا توجد جهات إدارية'}),el('div',{text:'أضف عقدة في الهيكل الإداري أولاً.'})]));if(!statuses.length)body.appendChild(el('div',{class:'alert error'},[el('div',{class:'t',text:'لا توجد حالات موظفين'}),el('div',{text:'تعذر تحميل قائمة الحالات.'})]));
  modal('إضافة موظف',body,[{text:'حفظ',onClick:async(errHost)=>{if(!fFirst.value.trim()||!fLast.value.trim()||!fOrg.value||!fHire.value||!fStatus.value){alertBox(errHost,'error','بيانات ناقصة','يرجى تعبئة الحقول المطلوبة (*).');return false;}await api('POST','/employees',{firstNameAr:fFirst.value.trim(),middleNameAr:fMid.value.trim(),lastNameAr:fLast.value.trim(),gender:fGender.value,orgNodeId:castId(fOrg.value),hireDate:fHire.value,statusId:castId(fStatus.value),fingerprintNo:fFp.value.trim()||null,mobile:fMobile.value.trim()||null});toast('تمت إضافة الموظف بنجاح');onDone();}},{text:'إلغاء',cls:'secondary'}]);setTimeout(()=>fFirst.focus(),30);}
function castId(v){if(v===''||v===null||v===undefined)return null;return /^-?\d+$/.test(String(v))?Number(v):v;}
function startWorkForm(emp,onDone){const fDate=inp({type:'date',value:fmtDate(emp.workStartDate)||todayStr()});const fNotes=el('textarea');const body=el('div');body.appendChild(el('p',{},['الموظف: ',el('b',{text:emp.fullNameAr||''}),' (',String(emp.employeeNo||''),')']));body.appendChild(field('تاريخ المباشرة *',fDate));body.appendChild(field('ملاحظات',fNotes));modal('مباشرة العمل',body,[{text:'تأكيد المباشرة',onClick:async(errHost)=>{if(!fDate.value){alertBox(errHost,'error','بيانات ناقصة','تاريخ المباشرة مطلوب.');return false;}await api('POST','/employees/'+encodeURIComponent(emp.id)+'/start-work',{startDate:fDate.value,notes:fNotes.value.trim()});toast('تم تسجيل مباشرة العمل');onDone();}},{text:'إلغاء',cls:'secondary'}]);}

/* ---------- Attendance ---------- */
const statusMap={present:{label:'حاضر',cls:'green',row:'st-present'},absent:{label:'غائب',cls:'red',row:'st-absent'},leave:{label:'إجازة',cls:'blue',row:'st-leave'},holiday:{label:'عطلة',cls:'gray',row:'st-gray'},holiday_work:{label:'عمل في عطلة',cls:'amber',row:'st-amber'},weekend:{label:'راحة أسبوعية',cls:'gray',row:'st-gray'},excused:{label:'مستأذن',cls:'amber',row:'st-amber'}};
function statusInfo(s){const k=String(s||'').toLowerCase().replace(/-/g,'_');if(statusMap[k])return statusMap[k];if(k==='holidaywork'||k==='work_on_holiday')return statusMap.holiday_work;if(k==='weekly_rest'||k==='rest')return statusMap.weekend;return {label:s||'',cls:'gray',row:''};}
async function renderAttendance(main){main.innerHTML='';main.appendChild(el('div',{class:'page-head'},[el('h1',{text:'الحضور والانصراف'})]));
  let employees=[],shifts=[];const loadErr=el('div');try{const er=await api('GET','/employees'+qs({page:1,pageSize:500}));employees=er.data||[];}catch(e){showError(e,loadErr);}try{const sr=await api('GET','/shifts');shifts=sr.data||[];}catch(e){showError(e,loadErr);}main.appendChild(loadErr);
  const empLabel=e=>(e.fullNameAr||'')+' ('+(e.employeeNo||'')+')';const empOptions=employees.map(e=>({value:e.id,label:empLabel(e)}));const shiftLabel=s=>s.nameAr||s.name||s.nameEn||s.code||('#'+s.id);
  const grid=el('div',{class:'grid2'});
  /* Shift assignment */
  const c1=el('div',{class:'card'});c1.appendChild(el('h2',{text:'ربط موظفين بفترة دوام'}));const c1Err=el('div');c1.appendChild(c1Err);const multi=el('div',{class:'multi'});const filter=inp({placeholder:'تصفية الموظفين…'});const checks=[];function drawMulti(){multi.innerHTML='';const q=filter.value.trim();let any=false;for(const e of employees){const lbl=empLabel(e);if(q&&!lbl.includes(q))continue;any=true;let cb=checks.find(c=>c.id===e.id);if(!cb){cb={id:e.id,el:inp({type:'checkbox'})};checks.push(cb);}multi.appendChild(el('label',{},[cb.el,el('span',{text:lbl})]));}if(!any)multi.appendChild(el('div',{class:'empty',text:'لا يوجد موظفون'}));}filter.addEventListener('input',drawMulti);drawMulti();const selAll=el('button',{class:'btn secondary sm',type:'button',onclick:()=>{for(const c of checks)c.el.checked=true;}},'تحديد الكل');const selNone=el('button',{class:'btn secondary sm',type:'button',onclick:()=>{for(const c of checks)c.el.checked=false;}},'إلغاء التحديد');c1.appendChild(el('div',{class:'toolbar'},[filter,selAll,selNone]));c1.appendChild(multi);const fShift=sel(shifts.map(s=>({value:s.id,label:shiftLabel(s)})),'');const fFrom=inp({type:'date',value:todayStr()});c1.appendChild(el('div',{class:'form-row',style:'margin-top:12px'},[field('الفترة *',fShift),field('ساري من *',fFrom)]));const applyBtn=el('button',{class:'btn',type:'button'},'تطبيق الربط');const applyRes=el('div');c1.appendChild(applyBtn);c1.appendChild(applyRes);
  applyBtn.addEventListener('click',async()=>{c1Err.innerHTML='';applyRes.innerHTML='';const ids=checks.filter(c=>c.el.checked).map(c=>c.id);if(!ids.length||!fShift.value||!fFrom.value){alertBox(c1Err,'error','بيانات ناقصة','حدد موظفاً واحداً على الأقل والفترة وتاريخ السريان.');return;}busy(applyBtn,true);try{const r=await api('POST','/shift-assignments/apply',{employeeIds:ids,shiftId:castId(fShift.value),effectiveFrom:fFrom.value});const d=r.data||{};const applied=Array.isArray(d.applied)?d.applied:[];const rejected=Array.isArray(d.rejected)?d.rejected:[];alertBox(applyRes,'success','تم التطبيق','تم الربط لعدد '+applied.length+' موظف، ورُفض '+rejected.length+'.');if(rejected.length){applyRes.appendChild(table([{h:'الموظف',r:x=>{const e=employees.find(z=>String(z.id)===String(x.employeeId));return e?empLabel(e):String(x.employeeId);}},{h:'سبب الرفض',r:x=>x.reason}],rejected));}}catch(e){showError(e,c1Err);}finally{busy(applyBtn,false);}});
  /* Manual punch */
  const c2=el('div',{class:'card'});c2.appendChild(el('h2',{text:'بصمة يدوية'}));const c2Err=el('div');c2.appendChild(c2Err);const pEmp=sel([{value:'',label:'— اختر موظفاً —'}].concat(empOptions),'');const pTime=inp({type:'datetime-local',value:nowLocal()});const pDir=sel([{value:'in',label:'دخول'},{value:'out',label:'خروج'}],'in');const pReason=inp({placeholder:'سبب البصمة اليدوية'});c2.appendChild(field('الموظف *',pEmp));c2.appendChild(el('div',{class:'form-row'},[field('وقت البصمة *',pTime),field('الاتجاه',pDir)]));c2.appendChild(field('السبب *',pReason));const punchBtn=el('button',{class:'btn',type:'button'},'تسجيل البصمة');c2.appendChild(punchBtn);punchBtn.addEventListener('click',async()=>{c2Err.innerHTML='';if(!pEmp.value||!pTime.value||!pReason.value.trim()){alertBox(c2Err,'error','بيانات ناقصة','الموظف ووقت البصمة والسبب مطلوبة.');return;}busy(punchBtn,true);try{let t=pTime.value;if(t.length===16)t+=':00';await api('POST','/punches/manual',{employeeId:castId(pEmp.value),punchTime:t,direction:pDir.value,reason:pReason.value.trim()});toast('تم تسجيل البصمة اليدوية');pReason.value='';}catch(e){showError(e,c2Err);}finally{busy(punchBtn,false);}});
  /* Process */
  c2.appendChild(el('hr',{style:'border:none;border-top:1px solid var(--border);margin:18px 0'}));c2.appendChild(el('h2',{text:'احتساب الحضور لمدى'}));const c3Err=el('div');c2.appendChild(c3Err);const prFrom=inp({type:'date',value:firstOfMonth()});const prTo=inp({type:'date',value:todayStr()});c2.appendChild(el('div',{class:'form-row'},[field('من *',prFrom),field('إلى *',prTo)]));const procBtn=el('button',{class:'btn',type:'button'},'احتساب');const procRes=el('div');c2.appendChild(procBtn);c2.appendChild(procRes);procBtn.addEventListener('click',async()=>{c3Err.innerHTML='';procRes.innerHTML='';if(!prFrom.value||!prTo.value){alertBox(c3Err,'error','بيانات ناقصة','حدد تاريخ البداية والنهاية.');return;}if(prFrom.value>prTo.value){alertBox(c3Err,'error','مدى غير صالح','تاريخ البداية بعد تاريخ النهاية.');return;}busy(procBtn,true);try{const r=await api('POST','/attendance/process',{from:prFrom.value,to:prTo.value});const d=r.data||{};procRes.appendChild(el('div',{class:'alert success'},[el('div',{class:'t',text:'تم الاحتساب'}),el('div',{class:'kv'},[el('span',{},['الأيام المعالجة: ',el('b',{text:String(d.processedDays??0)})]),el('span',{},['الموظفون: ',el('b',{text:String(d.employees??0)})]),el('span',{},['المتخطاة (يدوي): ',el('b',{text:String(d.skippedManual??0)})])])]));}catch(e){showError(e,c3Err);}finally{busy(procBtn,false);}});
  grid.appendChild(c1);grid.appendChild(c2);main.appendChild(grid);
  /* Daily */
  const c4=el('div',{class:'card'});c4.appendChild(el('h2',{text:'سجل الحضور اليومي'}));const c4Err=el('div');c4.appendChild(c4Err);const dEmp=sel([{value:'',label:'— اختر موظفاً —'}].concat(empOptions),'');const dFrom=inp({type:'date',value:firstOfMonth()});const dTo=inp({type:'date',value:todayStr()});const showBtn=el('button',{class:'btn',type:'button'},'عرض');const legend=el('div',{style:'display:flex;gap:8px;flex-wrap:wrap;margin-bottom:10px'});for(const k of ['present','absent','leave','holiday','holiday_work','weekend','excused'])legend.appendChild(el('span',{class:'badge '+statusMap[k].cls,text:statusMap[k].label}));c4.appendChild(el('div',{class:'toolbar'},[dEmp,dFrom,dTo,showBtn]));c4.appendChild(legend);const dBody=el('div');c4.appendChild(dBody);showBtn.addEventListener('click',async()=>{c4Err.innerHTML='';dBody.innerHTML='';if(!dEmp.value||!dFrom.value||!dTo.value){alertBox(c4Err,'error','بيانات ناقصة','اختر الموظف والمدى الزمني.');return;}busy(showBtn,true);try{const r=await api('GET','/attendance/daily'+qs({employeeId:dEmp.value,from:dFrom.value,to:dTo.value}));const rows=r.data||[];const sum=rows.reduce((a,x)=>{a.work+=x.workMinutes||0;a.late+=x.lateMin||0;a.early+=x.earlyLeaveMin||0;a.absent+=x.absentMin||0;a.ot+=x.overtimeMin||0;a.exc+=x.excusedMin||0;return a;},{work:0,late:0,early:0,absent:0,ot:0,exc:0});dBody.appendChild(el('div',{class:'kv',style:'margin-bottom:10px'},[el('span',{},['الأيام: ',el('b',{text:String(rows.length)})]),el('span',{},['العمل: ',el('b',{text:hm(sum.work)})]),el('span',{},['التأخير: ',el('b',{text:hm(sum.late)})]),el('span',{},['الخروج المبكر: ',el('b',{text:hm(sum.early)})]),el('span',{},['الغياب: ',el('b',{text:hm(sum.absent)})]),el('span',{},['الإضافي: ',el('b',{text:hm(sum.ot)})]),el('span',{},['الاستئذان: ',el('b',{text:hm(sum.exc)})])]));dBody.appendChild(table([{h:'التاريخ',r:x=>fmtDate(x.workDate)},{h:'اليوم',r:x=>dayName(x.workDate)},{h:'الحالة',r:x=>{const s=statusInfo(x.status);return el('span',{class:'badge '+s.cls,text:s.label});}},{h:'دخول',r:x=>fmtTime(x.inTime)},{h:'خروج',r:x=>fmtTime(x.outTime)},{h:'المطلوب',r:x=>hm(x.requiredMinutes)},{h:'العمل',r:x=>hm(x.workMinutes)},{h:'تأخير',r:x=>hm(x.lateMin)},{h:'خروج مبكر',r:x=>hm(x.earlyLeaveMin)},{h:'غياب',r:x=>hm(x.absentMin)},{h:'إضافي',r:x=>hm(x.overtimeMin)},{h:'استئذان',r:x=>hm(x.excusedMin)}],rows,x=>statusInfo(x.status).row));}catch(e){showError(e,c4Err);}finally{busy(showBtn,false);}});
  main.appendChild(c4);}
function hm(m){m=Number(m)||0;if(!m)return '0:00';const h=Math.floor(m/60),mm=m%60;return h+':'+String(mm).padStart(2,'0');}
function firstOfMonth(){const d=new Date();return d.getFullYear()+'-'+String(d.getMonth()+1).padStart(2,'0')+'-01';}
function dayName(v){const s=fmtDate(v);if(!s)return '';const d=new Date(s+'T00:00:00');if(isNaN(d.getTime()))return '';return ['الأحد','الاثنين','الثلاثاء','الأربعاء','الخميس','الجمعة','السبت'][d.getDay()];}

/* ---------- Boot ---------- */
async function boot(){if(store.access){currentUser=store.user;try{const me=await api('GET','/auth/me');if(me&&me.data){currentUser=me.data.user||me.data;sessionStorage.setItem('user',JSON.stringify(currentUser));}}catch(e){if(!currentUser){logout();return;}}if(currentUser){showApp();route();return;}}showLogin();}
boot();
})();
</script>
</body>
</html>
=== END FILE ===