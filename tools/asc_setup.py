"""App Store Connect setup for Patente Italiana (run by .github/workflows/asc-setup.yml).
Creates (if missing) the in-app purchases and subscriptions with the same ids as Google Play,
Italian prices as base (Apple equalizes the other countries), localizations it/en/ar, and the 3-day free trials.
env: ASC_KEY_ID, ASC_ISSUER_ID, ASC_KEY_P8. Safe to run again: existing items are skipped.
"""
import os, time, json, sys
import jwt, requests

BUNDLE = 'com.memo.patentemondo'
API = 'https://api.appstoreconnect.apple.com'


def token():
    now = int(time.time())
    return jwt.encode({'iss': os.environ['ASC_ISSUER_ID'], 'iat': now, 'exp': now + 1100, 'aud': 'appstoreconnect-v1'},
                      os.environ['ASC_KEY_P8'], algorithm='ES256', headers={'kid': os.environ['ASC_KEY_ID'], 'typ': 'JWT'})


def call(method, path, body=None, params=None):
    for attempt in range(4):
        r = requests.request(method, API + path, params=params, json=body,
                             headers={'Authorization': 'Bearer ' + token(), 'Content-Type': 'application/json'})
        if r.status_code == 429: time.sleep(5 * (attempt + 1)); continue
        if r.status_code >= 400:
            print('ERROR', method, path, r.status_code, r.text[:800]); sys.stdout.flush()
            return None
        return r.json() if r.text else {}
    return None


def all_pages(path, params=None):
    out, p = [], dict(params or {}, limit=200)
    while path:
        r = call('GET', path, params=p)
        if not r: break
        out += r.get('data', [])
        nxt = r.get('links', {}).get('next')
        path, p = (nxt.replace(API, ''), None) if nxt else (None, None)
    return out


L = {  # product id: (it, en, ar) names
    'premium_lifetime': ('Premium a vita', 'Lifetime Premium', 'النسخة الكاملة مدى الحياة'),
    'pass_3_months': ('Premium 3 mesi', 'Premium 3 months', 'النسخة الكاملة 3 شهور'),
    'addon_exams': ('Esami illimitati', 'Unlimited exams', 'امتحانات بلا حدود'),
    'addon_drive': ('Guida 3D illimitata', 'Unlimited 3D driving', 'قيادة 3D بلا حدود'),
    'addon_listen': ('Esame in ascolto', 'Listening exam', 'امتحان الاستماع'),
    'addon_gold': ('Profilo oro', 'Gold profile', 'البروفايل الذهبي'),
    'premium_monthly': ('Premium mensile', 'Monthly Premium', 'النسخة الكاملة شهري'),
    'pro_monthly': ('Patenti professionali mensile', 'Professional licences monthly', 'الرخص المهنية شهري'),
    'pro_yearly': ('Patenti professionali annuale', 'Professional licences yearly', 'الرخص المهنية سنوي'),
}
D = {
    'premium_lifetime': ('Tutta la patente B per sempre', 'All of licence B, forever', 'كل رخصة B للأبد'),
    'pass_3_months': ('Tutta la patente B per 90 giorni', 'All of licence B for 90 days', 'كل رخصة B لمدة 90 يوم'),
    'addon_exams': ('Simulazioni d\'esame senza limiti', 'Exam simulations without limits', 'امتحانات تجريبية بلا حدود'),
    'addon_drive': ('Guida 3D senza limiti', '3D driving without limits', 'قيادة 3D بلا حدود'),
    'addon_listen': ('Domande lette ad alta voce', 'Questions read aloud', 'الأسئلة بتتقري بصوت عالي'),
    'addon_gold': ('Cornice oro e scudo serie', 'Gold frame and streak shield', 'إطار ذهبي وحماية السلسلة'),
    'premium_monthly': ('Tutta la patente B, mese per mese', 'All of licence B, month by month', 'كل رخصة B شهر بشهر'),
    'pro_monthly': ('C/CE, CQC Merci e CQC Persone', 'C/CE, CQC Goods and Passengers', 'C/CE و CQC بضائع وركاب'),
    'pro_yearly': ('C/CE, CQC Merci e CQC Persone', 'C/CE, CQC Goods and Passengers', 'C/CE و CQC بضائع وركاب'),
}
LOCALES = ('it', 'en-US', 'ar-SA')

ONE_TIME = [('premium_lifetime', 'NON_CONSUMABLE', '9.99'), ('pass_3_months', 'NON_RENEWING_SUBSCRIPTION', '4.99'),
            ('addon_exams', 'NON_CONSUMABLE', '1.99'), ('addon_drive', 'NON_CONSUMABLE', '1.99'),
            ('addon_listen', 'NON_CONSUMABLE', '1.99'), ('addon_gold', 'NON_CONSUMABLE', '0.99')]
GROUPS = [('Premium patente B', [('premium_monthly', 'ONE_MONTH', '2.99', True)]),
          ('Patenti professionali', [('pro_monthly', 'ONE_MONTH', '3.99', True), ('pro_yearly', 'ONE_YEAR', '24.99', False)])]


def main():
    apps = call('GET', '/v1/apps', params={'filter[bundleId]': BUNDLE})
    if not apps or not apps['data']:
        print('App not found in App Store Connect: create it first (My Apps > + > New App, bundle id ' + BUNDLE + ')'); sys.exit(1)
    app = apps['data'][0]['id']
    print('app', app, apps['data'][0]['attributes']['name'])
    territories = [t['id'] for t in all_pages('/v1/territories')]

    # ---------------- one-time purchases
    existing = {i['attributes']['productId']: i['id'] for i in all_pages(f'/v1/apps/{app}/inAppPurchasesV2')}
    for pid, kind, price in ONE_TIME:
        if pid in existing: print('exists', pid); continue
        r = call('POST', '/v2/inAppPurchases', {'data': {'type': 'inAppPurchases', 'attributes': {
            'name': L[pid][1], 'productId': pid, 'inAppPurchaseType': kind, 'familySharable': False},
            'relationships': {'app': {'data': {'type': 'apps', 'id': app}}}}})
        if not r: continue
        iid = r['data']['id']
        for loc, i in zip(LOCALES, range(3)):
            call('POST', '/v1/inAppPurchaseLocalizations', {'data': {'type': 'inAppPurchaseLocalizations', 'attributes': {
                'locale': loc, 'name': L[pid][i][:30], 'description': D[pid][i][:45]},
                'relationships': {'inAppPurchaseV2': {'data': {'type': 'inAppPurchases', 'id': iid}}}}})
        pts = all_pages(f'/v2/inAppPurchases/{iid}/pricePoints', {'filter[territory]': 'ITA'})
        pp = next((p['id'] for p in pts if p['attributes']['customerPrice'] == price), None)
        if pp:
            call('POST', '/v1/inAppPurchasePriceSchedules', {'data': {'type': 'inAppPurchasePriceSchedules', 'relationships': {
                'inAppPurchase': {'data': {'type': 'inAppPurchases', 'id': iid}},
                'baseTerritory': {'data': {'type': 'territories', 'id': 'ITA'}},
                'manualPrices': {'data': [{'type': 'inAppPurchasePrices', 'id': '${p1}'}]}}},
                'included': [{'type': 'inAppPurchasePrices', 'id': '${p1}', 'attributes': {'startDate': None},
                              'relationships': {'inAppPurchasePricePoint': {'data': {'type': 'inAppPurchasePricePoints', 'id': pp}}}}]})
        else: print('no price point', pid, price)
        call('POST', '/v1/inAppPurchaseAvailabilities', {'data': {'type': 'inAppPurchaseAvailabilities', 'attributes': {'availableInNewTerritories': True},
            'relationships': {'inAppPurchase': {'data': {'type': 'inAppPurchases', 'id': iid}},
                              'availableTerritories': {'data': [{'type': 'territories', 'id': t} for t in territories]}}}})
        print('created', pid, kind, price)

    # ---------------- subscriptions
    groups = {g['attributes']['referenceName']: g['id'] for g in all_pages(f'/v1/apps/{app}/subscriptionGroups')}
    for gname, subs in GROUPS:
        gid = groups.get(gname)
        if not gid:
            r = call('POST', '/v1/subscriptionGroups', {'data': {'type': 'subscriptionGroups', 'attributes': {'referenceName': gname},
                     'relationships': {'app': {'data': {'type': 'apps', 'id': app}}}}})
            if not r: continue
            gid = r['data']['id']
            for loc in LOCALES:
                call('POST', '/v1/subscriptionGroupLocalizations', {'data': {'type': 'subscriptionGroupLocalizations', 'attributes': {'locale': loc, 'name': gname},
                     'relationships': {'subscriptionGroup': {'data': {'type': 'subscriptionGroups', 'id': gid}}}}})
        have = {s['attributes']['productId'] for s in all_pages(f'/v1/subscriptionGroups/{gid}/subscriptions')}
        for level, (pid, period, price, trial) in enumerate(subs, 1):
            if pid in have: print('exists', pid); continue
            r = call('POST', '/v1/subscriptions', {'data': {'type': 'subscriptions', 'attributes': {
                'name': L[pid][1], 'productId': pid, 'subscriptionPeriod': period, 'groupLevel': level, 'familySharable': False},
                'relationships': {'group': {'data': {'type': 'subscriptionGroups', 'id': gid}}}}})
            if not r: continue
            sid = r['data']['id']
            for loc, i in zip(LOCALES, range(3)):
                call('POST', '/v1/subscriptionLocalizations', {'data': {'type': 'subscriptionLocalizations', 'attributes': {
                    'locale': loc, 'name': L[pid][i][:30], 'description': D[pid][i][:45]},
                    'relationships': {'subscription': {'data': {'type': 'subscriptions', 'id': sid}}}}})
            call('POST', '/v1/subscriptionAvailabilities', {'data': {'type': 'subscriptionAvailabilities', 'attributes': {'availableInNewTerritories': True},
                'relationships': {'subscription': {'data': {'type': 'subscriptions', 'id': sid}},
                                  'availableTerritories': {'data': [{'type': 'territories', 'id': t} for t in territories]}}}})
            pts = all_pages(f'/v1/subscriptions/{sid}/pricePoints', {'filter[territory]': 'ITA'})
            base = next((p['id'] for p in pts if p['attributes']['customerPrice'] == price), None)
            if not base: print('no price point', pid, price); continue
            eq = all_pages(f'/v1/subscriptionPricePoints/{base}/equalizations')
            points = [(base, 'ITA')] + [(p['id'], p['relationships']['territory']['data']['id']) for p in eq
                                         if 'relationships' in p and p['relationships'].get('territory', {}).get('data')]
            if len(points) == 1:   # equalizations without relationship data: read territory with include
                eq = call('GET', f'/v1/subscriptionPricePoints/{base}/equalizations', params={'include': 'territory', 'limit': 200}) or {}
                points = [(base, 'ITA')] + [(p['id'], p['relationships']['territory']['data']['id']) for p in eq.get('data', [])]
            for ppid, terr in points:
                call('POST', '/v1/subscriptionPrices', {'data': {'type': 'subscriptionPrices', 'attributes': {'startDate': None, 'preserveCurrentPrice': False},
                    'relationships': {'subscription': {'data': {'type': 'subscriptions', 'id': sid}},
                                      'subscriptionPricePoint': {'data': {'type': 'subscriptionPricePoints', 'id': ppid}},
                                      'territory': {'data': {'type': 'territories', 'id': terr}}}}})
            if trial:
                for _, terr in points:
                    call('POST', '/v1/subscriptionIntroductoryOffers', {'data': {'type': 'subscriptionIntroductoryOffers', 'attributes': {
                        'duration': 'THREE_DAYS', 'offerMode': 'FREE_TRIAL', 'numberOfPeriods': 1},
                        'relationships': {'subscription': {'data': {'type': 'subscriptions', 'id': sid}},
                                          'territory': {'data': {'type': 'territories', 'id': terr}}}}})
            print('created', pid, period, price, 'territories', len(points), 'trial' if trial else '')


if __name__ == '__main__':
    main()
