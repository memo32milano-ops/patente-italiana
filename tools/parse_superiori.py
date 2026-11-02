"""Parse the official MIT question lists (DomandeSuperiori.pdf, DomandeCqcNuovoListato.pdf) into app JSON + figure pack.

usage: python3 -I tools/parse_superiori.py <sources/listati> <out_dir>
Outputs: listato_c.json, listato_cqc.json, figures_sup.bytes, report.txt
"""
import sys, re, json, hashlib, struct, io
from collections import OrderedDict, defaultdict
import pdfplumber
from PIL import Image

SRC, OUT = sys.argv[1], sys.argv[2]

HEAD = re.compile(r'^Quesito n° (\d+) - (.*)$')
STMT = re.compile(r'^(\d{4,6}) (.*?)\s*(VERO|FALSO)?$')

def parse(pdf_path):
    quesiti = []  # dict(q, title, stmts:[{id,it,v,img}])
    cur = None; in_head = False; last = None
    figs = {}
    with pdfplumber.open(pdf_path) as pdf:
        for p in pdf.pages:
            imgs = [im for im in p.images if im['x0'] > 400]
            rows = []  # (top, stmt)
            for l in p.extract_text_lines():
                t = l['text'].strip()
                if t.startswith('Ministero delle Infrastrutture'):
                    continue
                m = HEAD.match(t)
                if m:
                    cur = {'q': int(m.group(1)), 'title': m.group(2), 'stmts': []}
                    quesiti.append(cur); in_head = True; last = None
                    continue
                if t.startswith('Numero Testo domanda') or t == 'domanda':
                    in_head = False; continue
                if in_head:
                    cur['title'] += ' ' + t; continue
                m = STMT.match(t)
                if m and l['x0'] < 40:
                    s = {'id': int(m.group(1)), 'it': m.group(2).strip(), 'v': None, 'img': None}
                    if m.group(3): s['v'] = 1 if m.group(3) == 'VERO' else 0
                    cur['stmts'].append(s); last = s
                    rows.append((l['top'], s))
                    continue
                if last is not None:
                    tt = t
                    mm = re.match(r'^(.*?)\s*(VERO|FALSO)$', tt)
                    if mm:
                        tt = mm.group(1); last['v'] = 1 if mm.group(2) == 'VERO' else 0
                    if tt: last['it'] += ' ' + tt
            # images: attach to the row whose top is closest above the image top (+ tolerance)
            for im in imgs:
                cand = [r for r in rows if r[0] <= im['top'] + 6]
                if not cand: continue
                s = max(cand, key=lambda r: r[0])[1]
                data = im['stream'].get_data()
                filt = str(im['stream'].get('Filter'))
                if 'DCT' not in filt:
                    try:
                        img = p.crop((im['x0'], im['top'], im['x1'], im['bottom'])).to_image(resolution=150).original
                        b = io.BytesIO(); img.convert('RGB').save(b, 'JPEG', quality=85); data = b.getvalue()
                    except Exception:
                        continue
                key = hashlib.md5(data).hexdigest()[:12]
                figs[key] = data; s['img'] = key
    return quesiti, figs

def clean_title(t):
    t = re.sub(r'\s+', ' ', t).strip()
    m = re.search(r'\(([\d ]+)\)\s*$', t)
    codes = [int(x) for x in m.group(1).split()] if m else []
    if m: t = t[:m.start()].strip()
    return t, codes

report = []
allfigs = {}

# ---------------- patenti superiori (C/CE) ----------------
qs, figs = parse(f'{SRC}/dl_13_DomandeSuperiori.pdf')
chap_ids = OrderedDict(); stmts = []; bad = 0
for q in qs:
    title, codes = clean_title(q['title'])
    if codes and 3 not in codes:   # 3 = categoria C (C/CE); no codes = all categories
        continue
    if title not in chap_ids: chap_ids[title] = len(chap_ids) + 1
    c = chap_ids[title]
    for s in q['stmts']:
        if s['v'] is None: bad += 1; continue
        stmts.append({'id': s['id'], 'q': q['q'], 'c': c, 'it': re.sub(r'\s+', ' ', s['it']).strip(), 'v': s['v'],
                      'f': s['img'] or '', 't': [], 'k': [x for x in codes]})
        if s['img']: allfigs[s['img']] = figs[s['img']]
counts = defaultdict(int)
for s in stmts: counts[s['c']] += 1
# exam quotas: 40 statements spread proportionally (largest remainder), min 1 per chapter
tot = sum(counts.values()); n = len(chap_ids)
raw = {c: 40 * counts[c] / tot for c in counts}
quota = {c: max(1, int(raw[c])) for c in counts}
while sum(quota.values()) < 40:
    c = max(counts, key=lambda c: raw[c] - quota[c]); quota[c] += 1
while sum(quota.values()) > 40:
    c = max((c for c in counts if quota[c] > 1), key=lambda c: quota[c] - raw[c]); quota[c] -= 1
C1_TOPICS = ('Nozioni sulla costruzione', 'Lubrificazione', 'Nozioni su costruzione, montaggio', 'Freno e acceleratore', 'Metodi per individuare', 'Manutenzione dei veicoli', 'Responsabilità del conducente in merito')
chapters = [{'n': c, 'it': t, 'count': counts[c], 'exam': quota[c], 'c1': 1 if t.startswith(C1_TOPICS) else 0} for t, c in chap_ids.items()]
json.dump({'version': 'MIT listato patenti superiori (DomandeSuperiori.pdf, Portale dell\'Automobilista) - categoria C/CE',
           'chapters': chapters, 'stmts': stmts}, open(f'{OUT}/listato_c.json', 'w'), ensure_ascii=False, separators=(',', ':'))
report.append(f'C: quesiti(all)={len(qs)} chapters={n} stmts={len(stmts)} missing_answer={bad} with_fig={sum(1 for s in stmts if s["f"])} c1holder(code4)={sum(1 for s in stmts if 4 in s["k"])}')
for ch in chapters: report.append(f'  C{ch["n"]:2d} {ch["count"]:4d} exam={ch["exam"]} c1={ch["c1"]}  {ch["it"][:90]}')

# ---------------- CQC (nuovo listato) ----------------
CQC_TITLES = {
 1: 'Caratteristiche della catena cinematica (trasmissione)',
 2: 'Caratteristiche tecniche e funzionamento dei dispositivi di sicurezza',
 3: 'Ottimizzazione del consumo di carburante',
 4: 'Rischi della circolazione e situazioni di emergenza',
 5: 'Rischi della strada e infortuni sul lavoro',
 6: 'Contesto sociale e normativa: tempi di guida e di riposo, tachigrafo',
 7: 'Prevenzione della criminalità e del traffico di clandestini',
 8: 'Immagine dell\'azienda e logistica sostenibile',
 9: 'Prevenzione dei rischi fisici',
 10: 'Idoneità fisica e mentale',
 11: 'Caricamento del veicolo nel rispetto della sicurezza (merci)',
 12: 'Normativa sul trasporto di merci',
 13: 'Mercato dell\'autotrasporto merci e transizione ecologica',
 14: 'Sicurezza e comfort dei passeggeri',
 15: 'Caricamento del veicolo e normativa sul trasporto di persone',
 16: 'Mercato del trasporto di persone',
}
qs, figs = parse(f'{SRC}/dl_11_DomandeCqcNuovoListato.pdf')
stmts = []; bad = 0; counts = defaultdict(int)
for q in qs:
    m = re.match(r'(\d+)##tipologia', q['title'].strip())
    c = int(m.group(1))
    for s in q['stmts']:
        if s['v'] is None: bad += 1; continue
        stmts.append({'id': s['id'], 'q': q['q'], 'c': c, 'it': re.sub(r'\s+', ' ', s['it']).strip(), 'v': s['v'], 'f': s['img'] or '', 't': []})
        counts[c] += 1
        if s['img']: allfigs[s['img']] = figs[s['img']]
def sec(c): return 0 if c <= 10 else (1 if c <= 13 else 2)
chapters = [{'n': c, 'it': CQC_TITLES[c], 'count': counts[c], 'exam': 4 if c <= 10 else 10, 'sec': sec(c)} for c in sorted(counts)]
json.dump({'version': 'MIT nuovo listato CQC (DomandeCqcNuovoListato.pdf, Portale dell\'Automobilista; circ. 31895/2021 e 33787/2021)',
           'chapters': chapters, 'stmts': stmts}, open(f'{OUT}/listato_cqc.json', 'w'), ensure_ascii=False, separators=(',', ':'))
report.append(f'CQC: quesiti={len(qs)} stmts={len(stmts)} missing_answer={bad} with_fig={sum(1 for s in stmts if s["f"])}')
for ch in chapters: report.append(f'  Q{ch["n"]:2d} sec{ch["sec"]} {ch["count"]:4d}  {ch["it"]}')

# ---------------- figure pack (same format as figures.bytes) ----------------
with open(f'{OUT}/figures_sup.bytes', 'wb') as f:
    f.write(struct.pack('<i', len(allfigs)))
    for k, d in allfigs.items():
        kb = k.encode('ascii'); f.write(struct.pack('<i', len(kb))); f.write(kb); f.write(struct.pack('<i', len(d))); f.write(d)
report.append(f'figures: {len(allfigs)} unique, {sum(len(d) for d in allfigs.values())//1024} KB')
open(f'{OUT}/report.txt', 'w').write('\n'.join(report) + '\n')
print('\n'.join(report))
