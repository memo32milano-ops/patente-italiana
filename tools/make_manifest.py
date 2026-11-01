"""Writes Packages/manifest.json using the package versions that ship with the Unity editor image,
so the project can be built in the cloud without the original PC.
Usage: python3 make_manifest.py <dir with editor PackageManager folder copied out>"""
import json, os, sys, re
root = sys.argv[1]
builtin = os.path.join(root, 'BuiltInPackages')
deps = {}
for name in sorted(os.listdir(builtin)):
    pj = os.path.join(builtin, name, 'package.json')
    if not os.path.isfile(pj):
        continue
    try:
        d = json.load(open(pj, encoding='utf-8'))
    except Exception:
        continue
    n, v = d.get('name', name), d.get('version', '1.0.0')
    if n.startswith('com.unity.modules.') or n in ('com.unity.render-pipelines.universal', 'com.unity.ugui', 'com.unity.render-pipelines.core', 'com.unity.shadergraph'):
        deps[n] = v
# registry packages: take the version the editor recommends (editor manifest), else a safe default
wanted = {'com.unity.inputsystem': '1.14.0', 'com.unity.render-pipelines.universal': None, 'com.unity.ugui': '2.0.0'}
text = ''
for dp, _, files in os.walk(os.path.join(root, 'Editor')):
    for f in files:
        if f.endswith('.json'):
            try: text += open(os.path.join(dp, f), encoding='utf-8').read()
            except Exception: pass
for pkg, default in wanted.items():
    if pkg in deps:
        continue
    m = re.search(r'"' + re.escape(pkg) + r'"\s*:\s*\{[^{}]*?"(?:version|defaultVersion|recommended)"\s*:\s*"([^"]+)"', text)
    if m:
        deps[pkg] = m.group(1)
    elif default:
        deps[pkg] = default
os.makedirs('Packages', exist_ok=True)
json.dump({'dependencies': dict(sorted(deps.items()))}, open('Packages/manifest.json', 'w'), indent=2)
print(json.dumps(deps, indent=1))
