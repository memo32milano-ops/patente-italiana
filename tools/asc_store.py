"""Prepares the App Store page of Patente Italiana and sends version + in-app purchases to Apple review.
Runs on Codemagic (integration env vars APP_STORE_CONNECT_ISSUER_ID / _KEY_IDENTIFIER / _PRIVATE_KEY).

  python3 tools/asc_store.py [--build N] [--no-submit]

Idempotent: every run brings the page to the state described here.
Steps: category, age rating, content rights, price (free) and availability, localized name/subtitle/privacy URL,
version 1.1.0 texts in 42 languages, screenshots (store/ios/shots), review contact (copied from the account's other app),
review screenshots + review notes for in-app purchases and subscriptions, build, then review submission.
App Privacy ("nutrition label") is not in the API: it must be published once in App Store Connect.
"""
import glob, hashlib, json, os, sys, time
import jwt, requests

BUNDLE = 'com.memo.patentemondo'
VERSION = '1.1.0'
API = 'https://api.appstoreconnect.apple.com'
PRIVACY = 'https://memo32milano-ops.github.io/patente-italiana/privacy'
SUPPORT = 'https://memo32milano-ops.github.io/patente-italiana/'
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ARGS = sys.argv[1:]
PROBLEMS = []


def env(*names):
    for n in names:
        if os.environ.get(n): return os.environ[n]
    return None


def key():
    raw = env('ASC_KEY_P8', 'APP_STORE_CONNECT_PRIVATE_KEY').strip().strip('"').replace('\\n', '\n')
    if raw.startswith('@file:'): raw = open(os.path.expanduser(raw[6:])).read()
    if 'BEGIN' not in raw:
        body = ''.join(raw.split())
        raw = '-----BEGIN PRIVATE KEY-----\n' + '\n'.join(body[i:i + 64] for i in range(0, len(body), 64)) + '\n-----END PRIVATE KEY-----\n'
    return raw


KEY = key()


def token():
    now = int(time.time())
    return jwt.encode({'iss': env('ASC_ISSUER_ID', 'APP_STORE_CONNECT_ISSUER_ID'), 'iat': now, 'exp': now + 1100, 'aud': 'appstoreconnect-v1'},
                      KEY, algorithm='ES256', headers={'kid': env('ASC_KEY_ID', 'APP_STORE_CONNECT_KEY_IDENTIFIER'), 'typ': 'JWT'})


def call(method, path, body=None, params=None, ok404=False, quiet=False):
    url = path if path.startswith('http') else API + path
    for attempt in range(5):
        r = requests.request(method, url, params=params, json=body,
                             headers={'Authorization': 'Bearer ' + token(), 'Content-Type': 'application/json'})
        if r.status_code in (429, 500, 502, 503): time.sleep(4 * (attempt + 1)); continue
        if r.status_code == 404 and ok404: return None
        if r.status_code >= 400:
            msg = f'{method} {path} -> {r.status_code} {r.text[:700]}'
            if not quiet: print('ERROR', msg)
            PROBLEMS.append(msg)
            return None
        return r.json() if r.text else {}
    return None


def pages(path, params=None):
    out, p = [], dict(params or {}, limit=200)
    while path:
        r = call('GET', path, params=p)
        if not r: break
        out += r.get('data', [])
        nxt = r.get('links', {}).get('next')
        path, p = (nxt, None) if nxt else (None, None)
    return out


def has_data(r):
    """An uploaded, processed file (the API answers 200 with data null when there is none)."""
    d = (r or {}).get('data')
    if not d: return False
    st = ((d.get('attributes') or {}).get('assetDeliveryState') or {}).get('state')
    return st != 'FAILED'


def rel(t, i): return {'data': {'type': t, 'id': i}}


def upload_file(create_path, create_body, path):
    """Reserve, upload the parts, commit (same flow for screenshots and review screenshots)."""
    data = open(path, 'rb').read()
    r = call('POST', create_path, create_body(os.path.basename(path), len(data)))
    if not r: return False
    obj = r['data']
    for op in obj['attributes']['uploadOperations']:
        part = data[op['offset']: op['offset'] + op['length']]
        requests.request(op['method'], op['url'], data=part, headers={h['name']: h['value'] for h in op['requestHeaders']}).raise_for_status()
    call('PATCH', f"{create_path}/{obj['id']}", {'data': {'type': obj['type'], 'id': obj['id'],
         'attributes': {'uploaded': True, 'sourceFileChecksum': hashlib.md5(data).hexdigest()}}})
    return True


META = json.load(open(os.path.join(ROOT, 'store/appstore/metadata.json'), encoding='utf-8'))
NOTES = """Patente Italiana is a study app for the Italian driving licence theory exams (licence B, and licence C/CE and CQC for professional drivers).
No account or login is needed: open the app, choose a language, then use Study, Exam, Review and the other tiles.
The questions are the public lists published by the Italian Ministry of Transport (Portale dell'Automobilista); the app is not official and says so on the store page and inside the app (Settings > Information).
Translations: Italian and Arabic texts are bundled; all other languages, English included, are machine-translated on the device with Google ML Kit (the language model downloads once, then it works offline).
In-app purchases: Premium for licence B (lifetime, 90-day pass (non-renewing), monthly subscription with 3-day free trial for new subscribers, single add-ons) and a separate subscription for the professional licences (C/CE, CQC). Restore purchases is in Settings and on each purchase screen. Terms of Use and Privacy Policy links are on the purchase screens.
To see the professional section: on the home screen tap "C/CE" or "CQC Merci" in the row under the title.
The app has no ads and no tracking. The exam screen intentionally reproduces the layout of the real ministry exam for practice; it is labelled "Simulazione non ufficiale".
Contact: memo32milano@gmail.com"""


def main():
    apps = call('GET', '/v1/apps', params={'filter[bundleId]': BUNDLE})
    if not apps or not apps['data']: sys.exit('app not found')
    app = apps['data'][0]['id']; print('app', app)

    # ---------------- app-level: content rights, category, age rating, localized name/subtitle/privacy
    call('PATCH', f'/v1/apps/{app}', {'data': {'type': 'apps', 'id': app, 'attributes': {'contentRightsDeclaration': 'USES_THIRD_PARTY_CONTENT'}}})
    infos = pages(f'/v1/apps/{app}/appInfos')
    info = next((i for i in infos if i['attributes'].get('appStoreState') not in ('READY_FOR_SALE', 'REPLACED_WITH_NEW_INFO')), infos[0])
    call('PATCH', f"/v1/appInfos/{info['id']}", {'data': {'type': 'appInfos', 'id': info['id'],
         'relationships': {'primaryCategory': rel('appCategories', 'EDUCATION'), 'secondaryCategory': rel('appCategories', 'REFERENCE')}}})
    ard = call('GET', f"/v1/appInfos/{info['id']}/ageRatingDeclaration")
    if ard and ard.get('data'):
        # 2025 questionnaire: frequency questions take 'NONE', yes/no questions take false; learn the rest from Apple's errors
        STR = {'alcoholTobaccoOrDrugUseOrReferences', 'contests', 'gamblingSimulated', 'gunsOrOtherWeapons', 'horrorOrFearThemes',
               'matureOrSuggestiveThemes', 'medicalOrTreatmentInformation', 'profanityOrCrudeHumor', 'sexualContentGraphicAndNudity',
               'sexualContentOrNudity', 'violenceCartoonOrFantasy', 'violenceRealistic', 'violenceRealisticProlongedGraphicOrSadistic'}
        SKIP = {'kidsAgeBand', 'ageRatingOverride', 'ageRatingOverrideV2', 'koreaAgeRatingOverride', 'developerAgeRatingInfoUrl', 'gracRatingClassificationNumber'}
        new = {}
        for k, v in ard['data']['attributes'].items():
            if k in SKIP: continue
            new[k] = 'NONE' if k in STR or (isinstance(v, str) and v) else False
        url = f"/v1/ageRatingDeclarations/{ard['data']['id']}"
        for attempt in range(5):
            body = json.dumps({'data': {'type': 'ageRatingDeclarations', 'id': ard['data']['id'], 'attributes': new}})
            res = requests.patch(API + url, data=body, headers={'Authorization': 'Bearer ' + token(), 'Content-Type': 'application/json'})
            if res.ok: break
            errs = res.json().get('errors', [])
            fixed = False
            for e in errs:
                ptr = (e.get('source') or {}).get('pointer', '').split('/')[-1]; det = e.get('detail', '')
                if ptr in new:
                    if 'Expected a BOOLEAN' in det: new[ptr] = False
                    elif 'Expected a STRING' in det: new[ptr] = 'NONE'
                    else: new.pop(ptr)
                    fixed = True
            if not fixed: break
        print('age rating', 'ok' if res.ok else 'FAILED ' + res.text[:400], new)
        if not res.ok: PROBLEMS.append('age rating: ' + res.text[:300])
    have = {l['attributes']['locale']: l for l in pages(f"/v1/appInfos/{info['id']}/appInfoLocalizations")}
    for loc, m in META.items():
        attrs = {'name': m['name'], 'privacyPolicyUrl': PRIVACY}
        if m.get('subtitle'): attrs['subtitle'] = m['subtitle']
        if loc in have:
            r = call('PATCH', f"/v1/appInfoLocalizations/{have[loc]['id']}", {'data': {'type': 'appInfoLocalizations', 'id': have[loc]['id'], 'attributes': attrs}}, quiet=True)
        else:
            r = call('POST', '/v1/appInfoLocalizations', {'data': {'type': 'appInfoLocalizations', 'attributes': dict(attrs, locale=loc),
                     'relationships': {'appInfo': rel('appInfos', info['id'])}}}, quiet=True)
        if not r and loc != 'it':   # a localized name already taken by another app: keep the main name there
            attrs['name'] = META['it']['name']
            if loc in have: call('PATCH', f"/v1/appInfoLocalizations/{have[loc]['id']}", {'data': {'type': 'appInfoLocalizations', 'id': have[loc]['id'], 'attributes': attrs}})
            else: call('POST', '/v1/appInfoLocalizations', {'data': {'type': 'appInfoLocalizations', 'attributes': dict(attrs, locale=loc), 'relationships': {'appInfo': rel('appInfos', info['id'])}}})
    print('app info localizations', len(META))

    # ---------------- price: free, available everywhere
    pts = pages(f'/v1/apps/{app}/appPricePoints', {'filter[territory]': 'USA'})
    free = next((p['id'] for p in pts if float(p['attributes']['customerPrice']) == 0), None)
    if free:
        call('POST', '/v1/appPriceSchedules', {'data': {'type': 'appPriceSchedules', 'relationships': {
            'app': rel('apps', app), 'baseTerritory': rel('territories', 'USA'),
            'manualPrices': {'data': [{'type': 'appPrices', 'id': '${p0}'}]}}},
            'included': [{'type': 'appPrices', 'id': '${p0}', 'attributes': {'startDate': None},
                          'relationships': {'appPricePoint': rel('appPricePoints', free)}}]}, quiet=True)
    terr = [t['id'] for t in pages('/v1/territories')]
    inc = [{'type': 'territoryAvailabilities', 'id': f'${{t{i}}}', 'attributes': {'available': True},
            'relationships': {'territory': rel('territories', t)}} for i, t in enumerate(terr)]
    call('POST', '/v2/appAvailabilities', {'data': {'type': 'appAvailabilities', 'attributes': {'availableInNewTerritories': True},
         'relationships': {'app': rel('apps', app), 'territoryAvailabilities': {'data': [{'type': 'territoryAvailabilities', 'id': x['id']} for x in inc]}}},
         'included': inc}, quiet=True)
    print('price free, territories', len(terr))

    # ---------------- version
    vers = pages(f'/v1/apps/{app}/appStoreVersions', {'filter[platform]': 'IOS'})
    ver = next((v for v in vers if v['attributes']['appStoreState'] in ('PREPARE_FOR_SUBMISSION', 'DEVELOPER_REJECTED', 'REJECTED', 'METADATA_REJECTED')), None)
    if not ver:
        print('no editable version, states:', [(v['attributes']['versionString'], v['attributes']['appStoreState']) for v in vers]); return
    vid = ver['id']
    call('PATCH', f'/v1/appStoreVersions/{vid}', {'data': {'type': 'appStoreVersions', 'id': vid, 'attributes': {
         'versionString': VERSION, 'copyright': '2026 Venti Studios', 'releaseType': 'AFTER_APPROVAL'}}})
    vlocs = {l['attributes']['locale']: l for l in pages(f'/v1/appStoreVersions/{vid}/appStoreVersionLocalizations')}
    for loc, m in META.items():
        attrs = {'description': m['description'], 'supportUrl': SUPPORT, 'marketingUrl': SUPPORT}
        if m.get('keywords'): attrs['keywords'] = m['keywords']
        if m.get('promo'): attrs['promotionalText'] = m['promo']
        if loc in vlocs:
            call('PATCH', f"/v1/appStoreVersionLocalizations/{vlocs[loc]['id']}", {'data': {'type': 'appStoreVersionLocalizations', 'id': vlocs[loc]['id'], 'attributes': attrs}})
        else:
            r = call('POST', '/v1/appStoreVersionLocalizations', {'data': {'type': 'appStoreVersionLocalizations', 'attributes': dict(attrs, locale=loc),
                     'relationships': {'appStoreVersion': rel('appStoreVersions', vid)}}})
            if r: vlocs[loc] = r['data']
    print('version texts', len(vlocs))

    # ---------------- screenshots (main language; the other languages show the same ones)
    shots = sorted(glob.glob(os.path.join(ROOT, 'store/ios/shots/*.png')))
    if shots and 'it' in vlocs:
        lid = vlocs['it']['id']
        sets = pages(f'/v1/appStoreVersionLocalizations/{lid}/appScreenshotSets')
        s = next((x for x in sets if x['attributes']['screenshotDisplayType'] == 'APP_IPHONE_67'), None)
        if s:
            for old in pages(f"/v1/appScreenshotSets/{s['id']}/appScreenshots"): call('DELETE', f"/v1/appScreenshots/{old['id']}")
        else:
            s = call('POST', '/v1/appScreenshotSets', {'data': {'type': 'appScreenshotSets', 'attributes': {'screenshotDisplayType': 'APP_IPHONE_67'},
                     'relationships': {'appStoreVersionLocalization': rel('appStoreVersionLocalizations', lid)}}})['data']
        for p in shots:
            upload_file('/v1/appScreenshots', lambda n, sz: {'data': {'type': 'appScreenshots', 'attributes': {'fileName': n, 'fileSize': sz},
                        'relationships': {'appScreenshotSet': rel('appScreenshotSets', s['id'])}}}, p)
        print('screenshots', len(shots))

    # ---------------- review contact (same person as the account's other apps) + notes
    contact = None
    for other in pages('/v1/apps'):
        if other['id'] == app: continue
        for v in pages(f"/v1/apps/{other['id']}/appStoreVersions", {'limit': 5}):
            d = call('GET', f"/v1/appStoreVersions/{v['id']}/appStoreReviewDetail", ok404=True, quiet=True)
            if d and d.get('data') and d['data']['attributes'].get('contactPhone'):
                contact = d['data']['attributes']; break
        if contact: break
    attrs = {'demoAccountRequired': False, 'notes': NOTES}
    if contact:
        for k in ('contactFirstName', 'contactLastName', 'contactPhone', 'contactEmail'): attrs[k] = contact.get(k)
    cur = call('GET', f'/v1/appStoreVersions/{vid}/appStoreReviewDetail', ok404=True, quiet=True)
    if cur and cur.get('data'):
        call('PATCH', f"/v1/appStoreReviewDetails/{cur['data']['id']}", {'data': {'type': 'appStoreReviewDetails', 'id': cur['data']['id'], 'attributes': attrs}})
    else:
        call('POST', '/v1/appStoreReviewDetails', {'data': {'type': 'appStoreReviewDetails', 'attributes': attrs,
             'relationships': {'appStoreVersion': rel('appStoreVersions', vid)}}})
    print('review details', 'contact copied' if contact else 'NO CONTACT FOUND')

    # ---------------- in-app purchases + subscriptions: review screenshot, notes
    pay_b = os.path.join(ROOT, 'store/ios/review/paywall.png')
    pay_pro = os.path.join(ROOT, 'store/ios/review/propaywall.png')
    # addon_gold belongs to the online challenge, which this build does not include: not sent to review
    iaps = [i for i in pages(f'/v1/apps/{app}/inAppPurchasesV2') if i['attributes']['productId'] != 'addon_gold']
    for i in iaps:
        iid = i['id']
        call('PATCH', f'/v2/inAppPurchases/{iid}', {'data': {'type': 'inAppPurchases', 'id': iid, 'attributes': {
             'reviewNote': 'Shown on the Premium screen (home > Premium button, or Settings > Premium). Unlocks licence B content as described.'}}})
        if os.path.exists(pay_b) and not has_data(call('GET', f'/v2/inAppPurchases/{iid}/appStoreReviewScreenshot', ok404=True, quiet=True)):
            upload_file('/v1/inAppPurchaseAppStoreReviewScreenshots', lambda n, sz: {'data': {'type': 'inAppPurchaseAppStoreReviewScreenshots',
                        'attributes': {'fileName': n, 'fileSize': sz}, 'relationships': {'inAppPurchaseV2': rel('inAppPurchases', iid)}}}, pay_b)
    subs = []
    for g in pages(f'/v1/apps/{app}/subscriptionGroups'):
        for sub in pages(f"/v1/subscriptionGroups/{g['id']}/subscriptions"):
            subs.append(sub)
            sid = sub['id']; pro = 'pro' in sub['attributes']['productId']
            call('PATCH', f'/v1/subscriptions/{sid}', {'data': {'type': 'subscriptions', 'id': sid, 'attributes': {
                 'reviewNote': ('Professional licences screen: home > "C/CE" in the course row > PRO button.' if pro else
                                'Premium screen: home > Premium button.') + ' Auto-renewing; 3-day free trial for new subscribers on the monthly plans.'}}})
            shot = pay_pro if pro else pay_b
            if os.path.exists(shot) and not has_data(call('GET', f'/v1/subscriptions/{sid}/appStoreReviewScreenshot', ok404=True, quiet=True)):
                upload_file('/v1/subscriptionAppStoreReviewScreenshots', lambda n, sz: {'data': {'type': 'subscriptionAppStoreReviewScreenshots',
                            'attributes': {'fileName': n, 'fileSize': sz}, 'relationships': {'subscription': rel('subscriptions', sid)}}}, shot)
    # every subscription needs prices; the monthly plans promise a 3-day free trial in the app, so it must exist on the store
    for sub in subs:
        sid = sub['id']; pid = sub['attributes']['productId']
        prices = pages(f'/v1/subscriptions/{sid}/prices', {'include': 'territory'})
        terrs = sorted({(p.get('relationships', {}).get('territory', {}).get('data') or {}).get('id') for p in prices} - {None})
        offers = pages(f'/v1/subscriptions/{sid}/introductoryOffers')
        if 'monthly' in pid and not offers and terrs:
            for terr in terrs:
                call('POST', '/v1/subscriptionIntroductoryOffers', {'data': {'type': 'subscriptionIntroductoryOffers', 'attributes': {
                    'duration': 'THREE_DAYS', 'offerMode': 'FREE_TRIAL', 'numberOfPeriods': 1},
                    'relationships': {'subscription': rel('subscriptions', sid), 'territory': rel('territories', terr)}}}, quiet=True)
            offers = pages(f'/v1/subscriptions/{sid}/introductoryOffers')
        print('subscription', pid, 'prices', len(terrs), 'territories, free-trial offers', len(offers))
        if not terrs: PROBLEMS.append(f'{pid}: no prices')
    print('purchases', len(iaps), 'subscriptions', len(subs), [s['attributes']['productId'] + ':' + s['attributes'].get('state', '') for s in subs])
    print('iap states', [i['attributes']['productId'] + ':' + i['attributes'].get('state', '') for i in iaps])

    # ---------------- build
    want = next((a.split('=', 1)[1] for a in ARGS if a.startswith('--build=')), None)
    build = None
    for t in range(60):
        bs = pages('/v1/builds', {'filter[app]': app, 'sort': '-uploadedDate', 'limit': 20})
        cand = [b for b in bs if (not want or b['attributes']['version'] == want)]
        if cand:
            b = cand[0]; st = b['attributes']['processingState']
            print('build', b['attributes']['version'], st)
            if st == 'VALID': build = b; break
            if st in ('FAILED', 'INVALID'): print('BUILD INVALID: check the e-mail from Apple'); return
        time.sleep(30)
    if not build: print('build not ready'); return
    call('PATCH', f"/v1/builds/{build['id']}", {'data': {'type': 'builds', 'id': build['id'], 'attributes': {'usesNonExemptEncryption': False}}}, quiet=True)
    call('PATCH', f'/v1/appStoreVersions/{vid}/relationships/build', {'data': {'type': 'builds', 'id': build['id']}})
    print('build attached', build['attributes']['version'])

    if '--no-submit' in ARGS:
        print('not submitting (--no-submit)'); return

    # ---------------- submission: purchases go with the first version
    for i in iaps:
        call('POST', '/v1/inAppPurchaseSubmissions', {'data': {'type': 'inAppPurchaseSubmissions', 'relationships': {'inAppPurchaseV2': rel('inAppPurchases', i['id'])}}}, quiet=True)
    for sub in subs:
        call('POST', '/v1/subscriptionSubmissions', {'data': {'type': 'subscriptionSubmissions', 'relationships': {'subscription': rel('subscriptions', sub['id'])}}}, quiet=True)
    open_subs = [s for s in pages(f'/v1/apps/{app}/reviewSubmissions', {'filter[platform]': 'IOS'}) if s['attributes']['state'] in ('READY_FOR_REVIEW', 'UNRESOLVED_ISSUES')]
    rs = open_subs[0] if open_subs else (call('POST', '/v1/reviewSubmissions', {'data': {'type': 'reviewSubmissions', 'attributes': {'platform': 'IOS'},
                                                                                    'relationships': {'app': rel('apps', app)}}}) or {}).get('data')
    if not rs: print('could not create review submission'); return
    call('POST', '/v1/reviewSubmissionItems', {'data': {'type': 'reviewSubmissionItems', 'relationships': {
         'reviewSubmission': rel('reviewSubmissions', rs['id']), 'appStoreVersion': rel('appStoreVersions', vid)}}}, quiet=True)
    r = call('PATCH', f"/v1/reviewSubmissions/{rs['id']}", {'data': {'type': 'reviewSubmissions', 'id': rs['id'], 'attributes': {'submitted': True}}})
    print('SUBMITTED FOR REVIEW' if r else 'SUBMISSION FAILED (see errors above)')


if __name__ == '__main__':
    try:
        main()
    finally:
        print('\n==== problems ({}) ===='.format(len(PROBLEMS)))
        for p in PROBLEMS: print('-', p[:500])
