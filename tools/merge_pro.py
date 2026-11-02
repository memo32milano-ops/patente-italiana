"""Merge the translated/explained batches (sup/*.tsv) into the app text files for the professional courses.
usage: python3 tools/merge_pro.py <sup_dir> <Assets/PatenteArabo/Resources/PatenteArabo>
"""
import sys, json, glob, os
SUP, RES = sys.argv[1], sys.argv[2]
I18N = os.path.join(RES, 'i18n')

def read(path):
    d = {}
    for l in open(path, encoding='utf-8'):
        l = l.rstrip('\n')
        if not l.strip(): continue
        k, v = l.split('\t', 1)
        d[int(k)] = v.strip()
    return d

def dump(name, d):
    ids = sorted(d)
    json.dump({'ids': ids, 'tx': [d[i] for i in ids]}, open(os.path.join(I18N, name), 'w', encoding='utf-8'),
              ensure_ascii=False, separators=(',', ':'))
    print(name, len(ids))

for prefix, suffix, listato in (('c', '_c', 'listato_c.json'), ('q', '_cqc', 'listato_cqc.json')):
    want = {s['id'] for s in json.load(open(os.path.join(RES, 'data', listato), encoding='utf-8'))['stmts']}
    out = {'ar': {}, 'exit': {}, 'exar': {}}
    for kind in out:
        for f in sorted(glob.glob(os.path.join(SUP, f'{kind}_{prefix}[0-9][0-9].tsv'))):
            out[kind].update(read(f))
    for kind, d in out.items():
        miss = want - set(d)
        if miss: print(f'WARNING {prefix} {kind}: {len(miss)} statements without text, e.g. {sorted(miss)[:5]}')
        d = {k: v for k, v in d.items() if k in want}
        name = {'ar': f'q_ar{suffix}.json', 'exit': f'ex_it{suffix}.json', 'exar': f'ex_ar{suffix}.json'}[kind]
        dump(name, d)

g = os.path.join(SUP, 'glossary.json')
if os.path.exists(g):
    terms = json.load(open(g, encoding='utf-8'))['terms']
    os.makedirs(os.path.join(RES, 'data'), exist_ok=True)
    json.dump({'terms': terms}, open(os.path.join(RES, 'data', 'glossary_pro.json'), 'w', encoding='utf-8'), ensure_ascii=False, separators=(',', ':'))
    print('glossary_pro.json', len(terms))
    # tap-to-translate word maps (single words only; the study page looks up one word at a time)
    for lang in ('ar', 'en'):
        m = {t['it'].lower(): t[lang] for t in terms if ' ' not in t['it'].strip()}
        json.dump(m, open(os.path.join(I18N, f'gloss_{lang}_pro.json'), 'w', encoding='utf-8'), ensure_ascii=False, separators=(',', ':'))
        print(f'gloss_{lang}_pro.json', len(m))
