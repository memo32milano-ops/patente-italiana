import asyncio, base64, os
from playwright.async_api import async_playwright
D = os.path.dirname(os.path.abspath(__file__))
icon = base64.b64encode(open(f'{D}/icon512.png','rb').read()).decode()

CSS = """
@font-face{font-family:P;src:url(Poppins-Regular.ttf)}
@font-face{font-family:P;src:url(Poppins-Medium.ttf);font-weight:500}
@font-face{font-family:P;src:url(Poppins-Bold.ttf);font-weight:700}
@font-face{font-family:A;src:url(Amiri-Regular.ttf)}
@font-face{font-family:A;src:url(Amiri-Bold.ttf);font-weight:700}
*{box-sizing:border-box;margin:0;padding:0}
body{width:1080px;height:1920px;font-family:P,A,'Noto Sans CJK SC',sans-serif;color:#eaf0f8;
 background:radial-gradient(1200px 900px at 50% -10%,#1d4f9a 0%,#0c1421 60%);overflow:hidden;position:relative}
.flag{position:absolute;top:0;left:0;right:0;height:14px;display:flex}
.flag i{flex:1}.flag i:nth-child(1){background:#009246}.flag i:nth-child(2){background:#fff}.flag i:nth-child(3){background:#ce2b37}
.cap{padding:110px 80px 0;text-align:center}
.cap h1{font-size:82px;line-height:1.1;font-weight:700;color:#fff;letter-spacing:-1px}
.cap h1 b{color:#ffd24a}
.cap p{font-size:40px;color:#a8bad4;margin-top:26px;line-height:1.35}
.phone{position:absolute;left:70px;right:70px;top:560px;bottom:-60px;background:#0c1421;border-radius:64px 64px 0 0;
 border:10px solid #22344f;border-bottom:0;box-shadow:0 30px 120px rgba(0,0,0,.6);overflow:hidden}
.bar{height:120px;background:#111d30;border-bottom:2px solid #20304a;display:flex;align-items:center;padding:0 38px;gap:22px}
.bar .t{font-size:38px;font-weight:700;flex:1}
.ib{width:76px;height:76px;border-radius:38px;background:#1e2d46;display:flex;align-items:center;justify-content:center;font-size:36px}
.card{background:#16233a;border:2px solid #223452;border-radius:28px;padding:30px 34px;margin:26px 30px}
.hl{border-color:#2f80ed}
.muted{color:#96a6be}.gold{color:#f2c94c}.green{color:#6edc96}.red{color:#ff828c}
.btn{height:118px;border-radius:24px;display:flex;align-items:center;justify-content:center;font-size:42px;font-weight:700;color:#fff}
.ar{font-family:A;direction:rtl;text-align:right;font-size:42px;line-height:1.5}
.chip{display:inline-block;padding:14px 26px;border-radius:40px;background:#1e2d46;margin:9px;font-size:33px;border:2px solid #2a3d5e}
.pb{height:16px;border-radius:8px;background:#22344f;overflow:hidden}.pb i{display:block;height:100%;background:linear-gradient(90deg,#2f80ed,#6edc96)}
"""
STOP = """<svg width="300" height="380" viewBox="0 0 300 380"><rect x="20" y="0" width="260" height="380" rx="14" fill="#fff" stroke="#333" stroke-width="4"/>
<polygon points="95,30 205,30 260,85 260,195 205,250 95,250 40,195 40,85" fill="#c8102e" stroke="#fff" stroke-width="8"/>
<text x="150" y="162" font-family="Arial" font-weight="700" font-size="66" fill="#fff" text-anchor="middle">STOP</text>
<rect x="45" y="275" width="210" height="85" rx="8" fill="#fff" stroke="#111" stroke-width="5"/>
<text x="150" y="335" font-family="Arial" font-weight="700" font-size="52" fill="#111" text-anchor="middle">320 m</text></svg>"""
FLAG = '<div class="flag"><i></i><i></i><i></i></div>'

def page(cap, sub, body):
    return f"<html><head><style>{CSS}</style></head><body>{FLAG}<div class='cap'><h1>{cap}</h1><p>{sub}</p></div><div class='phone'>{body}</div></body></html>"

S = {}
S['1_study'] = page("Studia nella <b>tua lingua</b>", "Supera l'esame in italiano", f"""
<div class='bar'><div class='ib'>‹</div><div class='t'>Capitolo 5 · Precedenza</div><div class='ib'>🔊</div></div>
<div class='card hl' style='display:flex;gap:30px;align-items:center'>{STOP}
<div style='flex:1'><div style='font-size:40px;font-weight:600;line-height:1.35'>Il segnale raffigurato preannuncia un segnale di <u>STOP</u> a 320 metri</div></div></div>
<div class='card'><div class='muted' style='font-size:28px;margin-bottom:6px'>العربية</div>
<div class='ar'>الإشارة المصوّرة تنبّه مسبقًا إلى إشارة "قف" (STOP) على بُعد 320 مترًا</div></div>
<div style='display:flex;gap:24px;margin:10px 30px'><div class='btn' style='flex:1;background:#27ae60;box-shadow:0 0 0 6px #6edc96'>VERO ✓</div><div class='btn' style='flex:1;background:#24364f'>FALSO</div></div>
<div class='card' style='border-color:#27ae60'><div class='green' style='font-size:34px;font-weight:700'>Giusto! +10 XP</div>
<div style='font-size:32px;margin-top:10px;line-height:1.4'>La distanza scritta sul pannello indica che lo STOP si trova a 320 metri.</div>
<div class='ar' style='font-size:36px;margin-top:10px' >صح: المسافة المكتوبة على اللوحة تعني أن إشارة STOP على بعد 320 مترًا.</div></div>""")

boxes = ''.join(f"<div style='width:58px;height:58px;border-radius:12px;display:flex;align-items:center;justify-content:center;font-size:24px;font-weight:600;background:{'#27ae60' if i<11 else ('#2f80ed' if i==11 else '#1e2d46')}'>{i+1}</div>" for i in range(30))
S['2_exam'] = page("Simulazione d'esame <b>come quella vera</b>", "30 domande · 20 minuti · massimo 3 errori", f"""
<div class='bar'><div class='t'>Esame · domanda 12/30</div><div class='gold' style='font-size:40px;font-weight:700'>⏱ 14:32</div></div>
<div style='display:flex;flex-wrap:wrap;gap:12px;padding:26px 30px 0'>{boxes}</div>
<div class='card hl' style='margin-top:30px'><div style='font-size:44px;font-weight:600;line-height:1.4'>In autostrada è vietato fare inversione del senso di marcia, anche utilizzando i varchi dello spartitraffico</div></div>
<div class='card'><div class='muted' style='font-size:28px'>English</div><div style='font-size:36px;line-height:1.4;margin-top:6px'>On the motorway it is forbidden to make a U-turn, even using the gaps in the central reservation</div></div>
<div style='display:flex;gap:24px;margin:20px 30px'><div class='btn' style='flex:1;background:#2f80ed'>VERO</div><div class='btn' style='flex:1;background:#24364f'>FALSO</div></div>
<div style='text-align:center;font-size:32px' class='muted'>Errori: <span class='green'>0 / 3</span></div>""")

langs = "Italiano English العربية Français Español Română Shqip Українська 中文 বাংলা اردو हिन्दी Català Русский Deutsch Português Polski Türkçe فارسی தமிழ் తెలుగు मराठी ગુજરાતી ಕನ್ನಡ 日本語 한국어 Tiếng_Việt ไทย Indonesia Melayu Filipino Kiswahili Ελληνικά Български Hrvatski Čeština Slovenčina Slovenščina Magyar Nederlands ქართული עברית Македонски Беларуская Lietuvių Latviešu Eesti Svenska Dansk Suomi Norsk".split()
chips = ''.join(f"<span class='chip'{' style=\"border-color:#2f80ed;background:#17325c\"' if i==2 else ''}>{l.replace('_',' ')}</span>" for i,l in enumerate(langs))
S['3_langs'] = page("<b>50 lingue</b>, una patente", "Domande, spiegazioni e teoria tradotte", f"""
<div class='bar'><div class='ib'>‹</div><div class='t'>Lingua</div></div>
<div style='padding:24px 22px;text-align:center'>{chips}</div>""")

S['4_home'] = page("Ogni giorno <b>più vicino</b> alla patente", "Piano di studio, serie e quiz del giorno", f"""
<div style='display:flex;align-items:center;gap:24px;padding:36px 40px 10px'><img src='data:image/png;base64,{icon}' style='width:110px;height:110px;border-radius:26px'>
<div><div style='font-size:46px;font-weight:700'>Patente Italiana</div><div class='muted' style='font-size:30px'>Quiz patente B</div></div></div>
<div class='card hl'><div style='display:flex;justify-content:space-between;align-items:center'><div><div class='muted' style='font-size:30px'>Pronto per l'esame</div>
<div style='font-size:96px;font-weight:700' class='green'>82%</div><div style='font-size:32px'>Quasi pronto! 40 domande al giorno</div></div>
<div style='font-size:120px'>🎯</div></div><div class='pb' style='margin-top:20px'><i style='width:82%'></i></div></div>
<div style='display:flex'><div class='card' style='flex:1;margin-right:12px'><div style='font-size:60px'>🔥</div><div style='font-size:52px;font-weight:700' class='gold'>12</div><div class='muted' style='font-size:28px'>giorni di fila</div></div>
<div class='card' style='flex:1;margin-left:12px'><div style='font-size:60px'>⭐</div><div style='font-size:52px;font-weight:700'>4.350</div><div class='muted' style='font-size:28px'>XP · Livello 9</div></div></div>
<div class='card' style='background:linear-gradient(120deg,#2f80ed,#1d4f9a);border:0'><div style='font-size:42px;font-weight:700'>Quiz del giorno</div><div style='font-size:30px;opacity:.85'>10 domande · nuove ogni giorno</div></div>
<div style='display:flex'><div class='card' style='flex:1;margin-right:12px'><div style='font-size:36px;font-weight:700'>⚡ Sfida 60s</div></div><div class='card' style='flex:1;margin-left:12px'><div style='font-size:36px;font-weight:700'>🪤 Trabocchetti</div></div></div>""")

chs = [("Definizioni stradali e di traffico",92),("Segnali di pericolo",85),("Segnali di divieto",77),("Segnali di obbligo",64),("Segnali di precedenza",58),("Segnaletica orizzontale",41),("Semafori e agenti del traffico",30),("Segnali di indicazione",12)]
rows = ''.join(f"<div class='card' style='margin:14px 30px;padding:24px 30px'><div style='display:flex;justify-content:space-between;font-size:34px;font-weight:600'><span>{i+1}. {n}</span><span class='muted'>{p}%</span></div><div class='pb' style='margin-top:16px'><i style='width:{p}%'></i></div></div>" for i,(n,p) in enumerate(chs))
S['5_chapters'] = page("Teoria e <b>25 capitoli</b> ufficiali", "Tutte le 6.327 affermazioni del Ministero", f"""
<div class='bar'><div class='ib'>‹</div><div class='t'>Capitoli</div><div class='ib'>📖</div></div>{rows}""")

FG = f"""<html><head><style>{CSS}
body{{width:1024px;height:500px;background:linear-gradient(115deg,#0c1421 0%,#14305e 55%,#1d4f9a 100%)}}</style></head><body>{FLAG}
<div style='position:absolute;left:60px;top:80px;width:600px'>
<div style='font-size:76px;font-weight:700;line-height:1.05;color:#fff'>Patente<br>Italiana</div>
<div style='font-size:34px;color:#ffd24a;font-weight:600;margin-top:18px'>Quiz patente B in 50 lingue</div>
<div style='font-size:26px;color:#a8bad4;margin-top:14px'>Simulazione d'esame · Spiegazioni<br>Guida 3D · Ripasso degli errori</div></div>
<img src='data:image/png;base64,{icon}' style='position:absolute;right:70px;top:70px;width:360px;height:360px;border-radius:80px;box-shadow:0 20px 80px rgba(0,0,0,.55)'>
</body></html>"""

async def main():
    async with async_playwright() as p:
        b = await p.chromium.launch()
        for name, html in list(S.items()) + [('feature', FG)]:
            open(f'{D}/{name}.html','w').write(html)
            w,h = (1024,500) if name=='feature' else (1080,1920)
            pg = await b.new_page(viewport={'width':w,'height':h})
            await pg.goto(f'file://{D}/{name}.html'); await pg.wait_for_timeout(600)
            await pg.screenshot(path=f'{D}/{name}.png')
        await b.close()
asyncio.run(main())
